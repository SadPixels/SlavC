using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using SlavLang.Compiler;
using SlavLang.Pack.Format;
using SlavLang.Pack.Writer;

namespace SlavLang.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 2;
        }

        return args[0] switch
        {
            "вѣсть" or "--вѣсть" => Version(),
            "здравіе" => Doctor(),
            "зрѣти" when args.Length == 2 => Inspect(args[1]),
            "увѣрити" when args.Length == 2 => Verify(args[1]),
            "явити" when args.Length >= 2 => Reveal(args[1]),
            "сътворити" when args.Length >= 2 => Build(args[1], args[2..]),
            "бѣжати" when args.Length >= 2 => RunProgram(args[1], args[2..]),
            _ => UnknownCommand(),
        };
    }

    private static int Build(string sourcePath, string[] arguments)
    {
        if (!sourcePath.EndsWith(".slav", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(sourcePath))
        {
            throw new ArgumentException(
                $"SLAVC1002: Писаніе «{sourcePath}» не обрѣтеся или не имать окончанія .slav.");
        }

        var options = BuildOptions.Parse(sourcePath, arguments);
        var artifact = new SlavCompiler().Compile(new CompilationRequest(
            sourcePath,
            Path.GetFileNameWithoutExtension(sourcePath),
            options.Debug,
            options.References));
        PrintDiagnostics(artifact.Diagnostics);
        if (artifact.Diagnostics.Any(static value => value.Severity == SlavDiagnosticSeverity.Error))
        {
            return 1;
        }

        if (options.EmitIntermediate)
        {
            File.WriteAllText(
                Path.ChangeExtension(options.OutputPath, ".generated.cs"),
                artifact.GeneratedSource);
        }

        var assemblyName = Path.GetFileNameWithoutExtension(sourcePath) + ".dll";
        var entries = new List<SlavPackInputEntry>
        {
            new(
                assemblyName,
                SlavPackEntryKind.MainAssembly,
                artifact.AssemblyIdentity,
                null,
                artifact.MainAssemblyBytes),
        };
        if (artifact.PortablePdbBytes is not null && (options.Debug || options.KeepPdb))
        {
            entries.Add(new(
                Path.ChangeExtension(assemblyName, ".pdb"),
                SlavPackEntryKind.PortablePdb,
                null,
                null,
                artifact.PortablePdbBytes));
        }

        foreach (var reference in options.References)
        {
            var bytes = File.ReadAllBytes(reference);
            entries.Add(new(
                Path.GetFileName(reference),
                SlavPackEntryKind.ManagedAssembly,
                SlavCompiler.ReadAssemblyIdentity(bytes),
                null,
                bytes));
        }

        new SlavPackWriter().Write(new SlavPackWriteRequest(
            options.HostTemplate,
            options.OutputPath,
            Path.GetFileNameWithoutExtension(sourcePath),
            assemblyName,
            options.Rid,
            entries,
            Compress: !options.NoCompress,
            Force: options.Force,
            CompilerVersion: GetVersion()));
        var actualOutput = NormalizeOutput(options.OutputPath, options.Rid);
        Console.WriteLine(
            $"Сътворено: {Path.GetFullPath(actualOutput)} ({options.Rid}, {new FileInfo(actualOutput).Length} баитовъ)");
        return 0;
    }

    private static int Reveal(string sourcePath)
    {
        var source = File.ReadAllText(sourcePath);
        var (generated, diagnostics) = SlavSourceEmitter.Emit(source, Path.GetFullPath(sourcePath));
        PrintDiagnostics(diagnostics);
        Console.WriteLine(generated);
        return diagnostics.Any(static value => value.Severity == SlavDiagnosticSeverity.Error) ? 1 : 0;
    }

    private static int RunProgram(string sourcePath, string[] arguments)
    {
        var separator = Array.IndexOf(arguments, "--");
        var buildArguments = separator < 0 ? arguments : arguments[..separator];
        var programArguments = separator < 0 ? [] : arguments[(separator + 1)..];
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "SlavLang", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var output = Path.Combine(temporaryDirectory, "program");
            buildArguments = [.. buildArguments, "--изходъ", output, "--переписати"];
            var buildExitCode = Build(sourcePath, buildArguments);
            if (buildExitCode != 0)
            {
                return buildExitCode;
            }

            var executable = NormalizeOutput(output, RuntimeInformation.RuntimeIdentifier);
            using var process = Process.Start(new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                Arguments = string.Join(" ", programArguments.Select(QuoteArgument)),
            }) ?? throw new InvalidOperationException("SLAVC1003: Не можахъ пустити сътворену программу.");
            process.WaitForExit();
            return process.ExitCode;
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static int Version()
    {
        Console.WriteLine(GetVersion());
        return 0;
    }

    private static int Doctor()
    {
        Console.WriteLine($"съставникъ: {GetVersion()}");
        Console.WriteLine("ѩзыкъ: 0.1");
        Console.WriteLine("цѣльная-среда: net10.0");
        Console.WriteLine($"платформа-съставника: {RuntimeInformation.RuntimeIdentifier}");
        Console.WriteLine($"строй-slavpack: {SlavPackVersion.Current}");
        Console.WriteLine($"чинъ-запуска: {SlavPackConstants.RuntimeHostProtocol}");
        Console.WriteLine($"въстроены-съсылки: {ReferencePackLoader.Load().Count}");
        return 0;
    }

    private static int Inspect(string path)
    {
        using var reader = SlavPackReader.OpenExecutable(path);
        var manifest = reader.Manifest;
        Console.WriteLine($"{manifest.ApplicationName} ({manifest.TargetRid}, {manifest.TargetFramework})");
        Console.WriteLine($"входъ: {manifest.EntryAssembly}");
        foreach (var entry in manifest.Entries)
        {
            Console.WriteLine(
                $"{TranslateEntryKind(entry.Kind),-24} {entry.OriginalLength,10} " +
                $"{TranslateCompression(entry.Compression),-6} {entry.Name}");
        }

        return 0;
    }

    private static int Verify(string path)
    {
        using var reader = SlavPackReader.OpenExecutable(path);
        reader.VerifyAllEntries();
        Console.WriteLine($"ИСПРАВЕНЪ: {Path.GetFullPath(path)}");
        return 0;
    }

    private static void PrintDiagnostics(IEnumerable<SlavDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            Console.Error.WriteLine(
                $"{diagnostic.File}({diagnostic.Line},{diagnostic.Column}): " +
                $"{TranslateSeverity(diagnostic.Severity)} {diagnostic.Code}: {diagnostic.Message}");
        }
    }

    private static int UnknownCommand()
    {
        Console.Error.WriteLine("SLAVC1001: Невѣдома или неполна повелѣнь.");
        PrintUsage();
        return 2;
    }

    private static string GetVersion()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        return informationalVersion?.Split('+', 2)[0]
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "0.1.0";
    }

    private static string TranslateSeverity(SlavDiagnosticSeverity severity) => severity switch
    {
        SlavDiagnosticSeverity.Error => "погрѣхъ",
        SlavDiagnosticSeverity.Warning => "остереженіе",
        _ => "вѣдомость",
    };

    private static string TranslateEntryKind(SlavPackEntryKind kind) => kind switch
    {
        SlavPackEntryKind.MainAssembly => "главна-съборка",
        SlavPackEntryKind.ManagedAssembly => "управима-съборка",
        SlavPackEntryKind.PortablePdb => "отладочны-знамѧна",
        SlavPackEntryKind.Resource => "источникъ",
        SlavPackEntryKind.SatelliteAssembly => "спутна-съборка",
        SlavPackEntryKind.NativeLibrary => "родна-книжица",
        _ => kind.ToString(),
    };

    private static string TranslateCompression(SlavPackCompression compression) =>
        compression == SlavPackCompression.None ? "нѣтъ" : "brotli";

    private static string NormalizeOutput(string path, string rid) =>
        rid.StartsWith("win-", StringComparison.Ordinal) &&
        !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? path + ".exe"
            : path;

    private static string QuoteArgument(string value) =>
        value.Contains(' ') ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : value;

    private static void PrintUsage() =>
        Console.WriteLine(
            "Употреба: slavc <сътворити|бѣжати|явити|зрѣти|увѣрити|здравіе|вѣсть> [реченія]");

    private sealed record BuildOptions(
        string OutputPath,
        string Rid,
        string HostTemplate,
        bool Debug,
        bool NoCompress,
        bool KeepPdb,
        bool EmitIntermediate,
        bool Force,
        IReadOnlyList<string> References)
    {
        public static BuildOptions Parse(string sourcePath, string[] arguments)
        {
            var output = Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(sourcePath))!,
                Path.GetFileNameWithoutExtension(sourcePath));
            var rid = RuntimeInformation.RuntimeIdentifier;
            string? host = Environment.GetEnvironmentVariable("SLAVLANG_HOST_TEMPLATE");
            var debug = false;
            var noCompress = false;
            var keepPdb = false;
            var emitIntermediate = false;
            var force = false;
            var references = new List<string>();
            for (var index = 0; index < arguments.Length; index++)
            {
                string NextValue() => ++index < arguments.Length
                    ? arguments[index]
                    : throw new ArgumentException("SLAVC1004: Не дано значеніе къ знамені.");
                switch (arguments[index])
                {
                    case "-и" or "--изходъ":
                        output = NextValue();
                        break;
                    case "-ц" or "--цѣль":
                        rid = NextValue();
                        break;
                    case "-о" or "--образъ":
                        var configuration = NextValue();
                        debug = configuration.ToLowerInvariant() switch
                        {
                            "испытъ" => true,
                            "выпускъ" => false,
                            _ => throw new ArgumentException(
                                "SLAVC1006: Образъ да будет «испытъ» или «выпускъ»."),
                        };
                        break;
                    case "--съсылка":
                        references.Add(NextValue());
                        break;
                    case "--основа":
                        host = NextValue();
                        break;
                    case "--безъ-сжатия":
                        noCompress = true;
                        break;
                    case "--сохранити-отладку":
                        keepPdb = true;
                        break;
                    case "--явити-кодъ":
                        emitIntermediate = true;
                        break;
                    case "--переписати":
                        force = true;
                        break;
                    case "--повторяемо" or "--подробно":
                        break;
                    default:
                        throw new ArgumentException($"SLAVC1005: Невѣдомо знамѧ «{arguments[index]}».");
                }
            }

            host ??= Path.Combine(
                AppContext.BaseDirectory,
                "host-templates",
                rid,
                rid.StartsWith("win-", StringComparison.Ordinal)
                    ? "SlavLang.RuntimeHost.exe"
                    : "SlavLang.RuntimeHost");
            if (!File.Exists(host))
            {
                host = ExtractEmbeddedHost(rid);
            }

            return new(output, rid, host, debug, noCompress, keepPdb, emitIntermediate, force, references);
        }

        private static string ExtractEmbeddedHost(string rid)
        {
            var suffix = rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : string.Empty;
            var resourceName = $"SlavLang.HostTemplates.{rid}{suffix}";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                throw new FileNotFoundException(
                    $"SLAVC2004: Основа запуска для «{rid}» не въложена. " +
                    "Поставь SLAVLANG_HOST_TEMPLATE или полный slavc.");
            }

            var directory = Path.Combine(Path.GetTempPath(), "SlavLang", "host-templates");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"runtime-host-{GetVersion()}-{rid}{suffix}");
            if (File.Exists(path))
            {
                return path;
            }

            var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
            try
            {
                using (var output = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    stream.CopyTo(output);
                    output.Flush(flushToDisk: true);
                }

                File.Move(temporaryPath, path, overwrite: false);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Иный съставникъ прежде извлѣче основу запуска.
            }
            finally
            {
                File.Delete(temporaryPath);
            }

            return path;
        }
    }
}
