using System.Reflection.PortableExecutable;
using RusLang.Compiler;
using Xunit;

namespace RusLang.Compiler.Tests;

public sealed class CompilerTests : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "RusLangCompilerTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CyrillicProgramCompilesToManagedExecutable()
    {
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "Пример.rus");
        File.WriteAllText(source, "печать \"Привет\"\n");

        var artifact = new RusCompiler().Compile(
            new CompilationRequest(source, "Пример", Debug: false, References: []),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            artifact.Diagnostics,
            static value => value.Severity == RusDiagnosticSeverity.Error);
        using var stream = new MemoryStream(artifact.MainAssemblyBytes);
        using var reader = new PEReader(stream);
        Assert.True(reader.HasMetadata);
        Assert.StartsWith("Пример, Version=", artifact.AssemblyIdentity, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownConstructReportsRusSourceLocation()
    {
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "bad.rus");
        File.WriteAllText(source, "\nнеизвестно\n");

        var artifact = new RusCompiler().Compile(
            new CompilationRequest(source, "bad", Debug: false, References: []),
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(artifact.Diagnostics);
        Assert.Equal("RUS1001", diagnostic.Code);
        Assert.Equal(2, diagnostic.Line);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
