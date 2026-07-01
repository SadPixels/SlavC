using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using RusLang.Compiler;
using RusLang.Pack.Format;
using RusLang.Pack.Writer;

namespace RusLang.Cli;

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
            "version" or "--version" => Version(),
            "doctor" => Doctor(),
            "inspect" when args.Length == 2 => Inspect(args[1]),
            "verify" when args.Length == 2 => Verify(args[1]),
            "reveal" when args.Length >= 2 => Reveal(args[1]),
            "build" when args.Length >= 2 => Build(args[1], args[2..]),
            "run" when args.Length >= 2 => RunProgram(args[1], args[2..]),
            _ => UnknownCommand(),
        };
    }

    private static int Build(string sourcePath, string[] arguments)
    {
        if (!sourcePath.EndsWith(".rus", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(sourcePath))
        {
            throw new ArgumentException($"RUSC1002: Source file '{sourcePath}' was not found or is not .rus.");
        }

        var options = BuildOptions.Parse(sourcePath, arguments);
        var artifact = new RusCompiler().Compile(new CompilationRequest(
            sourcePath,
            Path.GetFileNameWithoutExtension(sourcePath),
            options.Debug,
            options.References));
        PrintDiagnostics(artifact.Diagnostics);
        if (artifact.Diagnostics.Any(static value => value.Severity == RusDiagnosticSeverity.Error))
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
        var entries = new List<RusPackInputEntry>
        {
            new(
                assemblyName,
                RusPackEntryKind.MainAssembly,
                artifact.AssemblyIdentity,
                null,
                artifact.MainAssemblyBytes),
        };
        if (artifact.PortablePdbBytes is not null && (options.Debug || options.KeepPdb))
        {
            entries.Add(new(
                Path.ChangeExtension(assemblyName, ".pdb"),
                RusPackEntryKind.PortablePdb,
                null,
                null,
                artifact.PortablePdbBytes));
        }

        foreach (var reference in options.References)
        {
            var bytes = File.ReadAllBytes(reference);
            entries.Add(new(
                Path.GetFileName(reference),
                RusPackEntryKind.ManagedAssembly,
                RusCompiler.ReadAssemblyIdentity(bytes),
                null,
                bytes));
        }

        new RusPackWriter().Write(new RusPackWriteRequest(
            options.HostTemplate,
            options.OutputPath,
            Path.GetFileNameWithoutExtension(sourcePath),
            assemblyName,
            options.Rid,
            entries,
            Compress: !options.NoCompress,
            Force: options.Force));
        var actualOutput = NormalizeOutput(options.OutputPath, options.Rid);
        Console.WriteLine(
            $"Создано: {Path.GetFullPath(actualOutput)} ({options.Rid}, {new FileInfo(actualOutput).Length} байт)");
        return 0;
    }

    private static int Reveal(string sourcePath)
    {
        var source = File.ReadAllText(sourcePath);
        var (generated, diagnostics) = RusSourceEmitter.Emit(source, Path.GetFullPath(sourcePath));
        PrintDiagnostics(diagnostics);
        Console.WriteLine(generated);
        return diagnostics.Any(static value => value.Severity == RusDiagnosticSeverity.Error) ? 1 : 0;
    }

    private static int RunProgram(string sourcePath, string[] arguments)
    {
        var separator = Array.IndexOf(arguments, "--");
        var buildArguments = separator < 0 ? arguments : arguments[..separator];
        var programArguments = separator < 0 ? [] : arguments[(separator + 1)..];
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "RusLang", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var output = Path.Combine(temporaryDirectory, "program");
            buildArguments = [.. buildArguments, "-o", output, "--force"];
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
            }) ?? throw new InvalidOperationException("RUSC1003: Failed to start compiled program.");
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
        Console.WriteLine($"compiler: {GetVersion()}");
        Console.WriteLine("language: 0.1");
        Console.WriteLine("tfm: net10.0");
        Console.WriteLine($"compiler-rid: {RuntimeInformation.RuntimeIdentifier}");
        Console.WriteLine($"ruspack: {RusPackVersion.Current}");
        Console.WriteLine($"host-protocol: {RusPackConstants.RuntimeHostProtocol}");
        Console.WriteLine($"reference-assemblies: {ReferencePackLoader.Load().Count}");
        return 0;
    }

    private static int Inspect(string path)
    {
        using var reader = RusPackReader.OpenExecutable(path);
        var manifest = reader.Manifest;
        Console.WriteLine($"{manifest.ApplicationName} ({manifest.TargetRid}, {manifest.TargetFramework})");
        Console.WriteLine($"entry: {manifest.EntryAssembly}");
        foreach (var entry in manifest.Entries)
        {
            Console.WriteLine($"{entry.Kind,-18} {entry.OriginalLength,10} {entry.Compression,-6} {entry.Name}");
        }

        return 0;
    }

    private static int Verify(string path)
    {
        using var reader = RusPackReader.OpenExecutable(path);
        reader.VerifyAllEntries();
        Console.WriteLine($"OK: {Path.GetFullPath(path)}");
        return 0;
    }

    private static void PrintDiagnostics(IEnumerable<RusDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            Console.Error.WriteLine(
                $"{diagnostic.File}({diagnostic.Line},{diagnostic.Column}): " +
                $"{diagnostic.Severity.ToString().ToLowerInvariant()} {diagnostic.Code}: {diagnostic.Message}");
        }
    }

    private static int UnknownCommand()
    {
        Console.Error.WriteLine("RUSC1001: Unknown or incomplete command.");
        PrintUsage();
        return 2;
    }

    private static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";

    private static string NormalizeOutput(string path, string rid) =>
        rid.StartsWith("win-", StringComparison.Ordinal) &&
        !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? path + ".exe"
            : path;

    private static string QuoteArgument(string value) =>
        value.Contains(' ') ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : value;

    private static void PrintUsage() =>
        Console.WriteLine(
            "Usage: rusc <build|run|reveal|inspect|verify|doctor|version> [arguments]");

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
            string? host = Environment.GetEnvironmentVariable("RUSLANG_HOST_TEMPLATE");
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
                    : throw new ArgumentException("RUSC1004: Missing option value.");
                switch (arguments[index])
                {
                    case "-o" or "--output": output = NextValue(); break;
                    case "-r" or "--rid": rid = NextValue(); break;
                    case "-c" or "--configuration":
                        debug = string.Equals(NextValue(), "debug", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "--reference": references.Add(NextValue()); break;
                    case "--host-template": host = NextValue(); break;
                    case "--no-compress": noCompress = true; break;
                    case "--keep-pdb": keepPdb = true; break;
                    case "--emit-intermediate": emitIntermediate = true; break;
                    case "--force": force = true; break;
                    case "--deterministic" or "--verbose": break;
                    default: throw new ArgumentException($"RUSC1005: Unknown option '{arguments[index]}'.");
                }
            }

            host ??= Path.Combine(
                AppContext.BaseDirectory,
                "host-templates",
                rid,
                rid.StartsWith("win-", StringComparison.Ordinal)
                    ? "RusLang.RuntimeHost.exe"
                    : "RusLang.RuntimeHost");
            if (!File.Exists(host))
            {
                host = ExtractEmbeddedHost(rid);
            }

            return new(output, rid, host, debug, noCompress, keepPdb, emitIntermediate, force, references);
        }

        private static string ExtractEmbeddedHost(string rid)
        {
            var suffix = rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : string.Empty;
            var resourceName = $"RusLang.HostTemplates.{rid}{suffix}";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                throw new FileNotFoundException(
                    $"RUSC2004: Host template for '{rid}' is not bundled. " +
                    "Set RUSLANG_HOST_TEMPLATE or install rusc-full.");
            }

            var directory = Path.Combine(Path.GetTempPath(), "RusLang", "host-templates");
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
                // Another compiler process won the atomic extraction race.
            }
            finally
            {
                File.Delete(temporaryPath);
            }

            return path;
        }
    }
}
