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
        File.WriteAllText(
            source,
            "призвать Ярило\nЦарь\nпечать \"Привет\"\nконец\n");

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
        File.WriteAllText(source, "Царь\nнеизвестно\nконец\n");

        var artifact = new RusCompiler().Compile(
            new CompilationRequest(source, "bad", Debug: false, References: []),
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(artifact.Diagnostics);
        Assert.Equal("RUS1001", diagnostic.Code);
        Assert.Equal(2, diagnostic.Line);
    }

    [Fact]
    public void SortingProgramCompiles()
    {
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "sort.rus");
        File.WriteAllText(
            source,
            """
            призвать Сварога
            призвать Ярило
            Царь
                пусть числа есть ряд 3 и 1 и 2
                для проход от 0 до длина числа минус 1
                    для индекс от 0 до длина числа минус проход минус 1
                        пусть следующий есть индекс плюс 1
                        если числа по индекс больше числа по следующий
                            пусть временное это числа по индекс
                            числа по индекс есть числа по следующий
                            числа по следующий это временное
                        конец
                    конец
                конец
                печать соединить " " и числа
            конец
            """);

        var artifact = new RusCompiler().Compile(
            new CompilationRequest(source, "sort", Debug: false, References: []),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            artifact.Diagnostics,
            static value => value.Severity == RusDiagnosticSeverity.Error);
        Assert.NotEmpty(artifact.MainAssemblyBytes);
    }

    [Fact]
    public void UnclosedEntryPointReportsOpeningLine()
    {
        var (source, diagnostics) = RusSourceEmitter.Emit(
            "призвать Ярило\nЦарь\n    печать \"да\"\n",
            "test.rus");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("RUS1006", diagnostic.Code);
        Assert.Equal(2, diagnostic.Line);
        Assert.Contains("Console.WriteLine(\"да\")", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("пусть числа есть [1 и 2]")]
    [InlineData("печать длина(числа)")]
    [InlineData("пусть числа есть ряд 1, 2")]
    [InlineData("пусть ответ = 42")]
    [InlineData("если a == b")]
    public void ClassicPunctuationIsRejected(string command)
    {
        var (_, diagnostics) = RusSourceEmitter.Emit(
            $"призвать Сварога\nпризвать Ярило\nЦарь\n{command}\nконец\n",
            "test.rus");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("RUS1010", diagnostic.Code);
    }

    [Theory]
    [InlineData("печать \"свет\"", "Ярило")]
    [InlineData("пусть числа есть ряд 1 и 2", "Сварога")]
    public void StandardConstructRequiresItsInvokedModule(string command, string module)
    {
        var (_, diagnostics) = RusSourceEmitter.Emit(
            $"Царь\n{command}\nконец\n",
            "test.rus");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("RUS1012", diagnostic.Code);
        Assert.Contains($"призвать {module}", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a точно b", "if (a == b)")]
    [InlineData("a бля буду b", "if (a == b)")]
    [InlineData("a не есть b", "if (a != b)")]
    [InlineData("a это не b", "if (a != b)")]
    [InlineData("a не меньше b", "if (a >= b)")]
    [InlineData("a не больше b", "if (a <= b)")]
    public void WordComparisonIsTranslated(string condition, string expected)
    {
        var (source, diagnostics) = RusSourceEmitter.Emit(
            $"Царь\nпусть a есть 2\nпусть b это 1\nесли {condition}\nконец\nконец\n",
            "test.rus");

        Assert.Empty(diagnostics);
        Assert.Contains(expected, source, StringComparison.Ordinal);
    }

    [Fact]
    public void WordMutationsCompile()
    {
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "mutations.rus");
        File.WriteAllText(
            source,
            """
            Царь
                пусть число есть 10
                число плюс есть 5
                число минус есть 2
                число умножить на 3
                число разделить есть 2
                число остаток есть 4
                число плюс плюс
                число уменьшить
            конец
            """);

        var artifact = new RusCompiler().Compile(
            new CompilationRequest(source, "mutations", Debug: false, References: []),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            artifact.Diagnostics,
            static value => value.Severity == RusDiagnosticSeverity.Error);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
