using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace SlavLang.Compiler;

public sealed class SlavCompiler
{
    public CompilationArtifact Compile(CompilationRequest request, CancellationToken cancellationToken = default)
    {
        var sourceText = File.ReadAllText(request.SourcePath, Encoding.UTF8);
        var (generatedSource, frontendDiagnostics) =
            SlavSourceEmitter.Emit(sourceText, Path.GetFullPath(request.SourcePath));
        if (frontendDiagnostics.Any(static value => value.Severity == SlavDiagnosticSeverity.Error))
        {
            return new([], null, frontendDiagnostics, string.Empty, generatedSource);
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(
            generatedSource,
            new CSharpParseOptions(LanguageVersion.CSharp14),
            path: request.SourcePath,
            encoding: Encoding.UTF8,
            cancellationToken: cancellationToken);
        var references = ReferencePackLoader.Load().ToList();
        foreach (var path in request.References)
        {
            references.Add(MetadataReference.CreateFromFile(Path.GetFullPath(path)));
        }

        var compilation = CSharpCompilation.Create(
            SanitizeAssemblyName(request.AssemblyName),
            [syntaxTree],
            references,
            new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                optimizationLevel: request.Debug ? OptimizationLevel.Debug : OptimizationLevel.Release,
                checkOverflow: true,
                deterministic: true,
                nullableContextOptions: NullableContextOptions.Enable,
                warningLevel: 9999));
        using var pe = new MemoryStream();
        using var pdb = request.Debug ? new MemoryStream() : null;
        var result = compilation.Emit(
            pe,
            pdbStream: pdb,
            options: request.Debug
                ? new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb)
                : new EmitOptions(),
            cancellationToken: cancellationToken);
        var diagnostics = result.Diagnostics
            .Where(static value => value.Severity != DiagnosticSeverity.Hidden)
            .Select(value => TranslateDiagnostic(value, request.SourcePath))
            .ToArray();
        if (!result.Success)
        {
            return new([], null, diagnostics, string.Empty, generatedSource);
        }

        var peBytes = pe.ToArray();
        return new(
            peBytes,
            pdb?.ToArray(),
            diagnostics,
            ReadAssemblyIdentity(peBytes),
            generatedSource);
    }

    public static string ReadAssemblyIdentity(ReadOnlyMemory<byte> bytes)
    {
        using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var definition = reader.GetAssemblyDefinition();
        var name = reader.GetString(definition.Name);
        var culture = definition.Culture.IsNil ? "neutral" : reader.GetString(definition.Culture);
        var publicKeyToken = definition.PublicKey.IsNil
            ? "null"
            : Convert.ToHexStringLower(
                System.Security.Cryptography.SHA1.HashData(reader.GetBlobBytes(definition.PublicKey))[^8..]
                    .Reverse()
                    .ToArray());
        return $"{name}, Version={definition.Version}, Culture={culture}, PublicKeyToken={publicKeyToken}";
    }

    private static SlavDiagnostic TranslateDiagnostic(Diagnostic diagnostic, string fallbackPath)
    {
        var span = diagnostic.Location.GetLineSpan();
        return new SlavDiagnostic(
            diagnostic.Id,
            diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => SlavDiagnosticSeverity.Error,
                DiagnosticSeverity.Warning => SlavDiagnosticSeverity.Warning,
                _ => SlavDiagnosticSeverity.Info,
            },
            string.IsNullOrEmpty(span.Path) ? fallbackPath : span.Path,
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            diagnostic.GetMessage(System.Globalization.CultureInfo.GetCultureInfo("ru-RU")));
    }

    private static string SanitizeAssemblyName(string value)
    {
        var sanitized = new string(value.Select(character =>
            char.IsLetterOrDigit(character) || character is '_' or '.'
                ? character
                : '_').ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "SlavLangProgram" : sanitized;
    }
}
