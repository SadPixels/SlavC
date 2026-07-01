using System.Reflection.PortableExecutable;
using SlavLang.Compiler;
using Xunit;

namespace SlavLang.Compiler.Tests;

public sealed class CompilerTests : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "SlavLangCompilerTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CyrillicProgramCompilesToManagedExecutable()
    {
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "Пример.slav");
        File.WriteAllText(
            source,
            "възвати Ярило\nКнѧзь\nречи \"Здравъ\"\nконьць\n");

        var artifact = new SlavCompiler().Compile(
            new CompilationRequest(source, "Пример", Debug: false, References: []),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            artifact.Diagnostics,
            static value => value.Severity == SlavDiagnosticSeverity.Error);
        using var stream = new MemoryStream(artifact.MainAssemblyBytes);
        using var reader = new PEReader(stream);
        Assert.True(reader.HasMetadata);
        Assert.StartsWith("Пример, Version=", artifact.AssemblyIdentity, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownConstructReportsSlavSourceLocation()
    {
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "bad.slav");
        File.WriteAllText(source, "Кнѧзь\nнеизвестно\nконьць\n");

        var artifact = new SlavCompiler().Compile(
            new CompilationRequest(source, "bad", Debug: false, References: []),
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(artifact.Diagnostics);
        Assert.Equal("SLAV1001", diagnostic.Code);
        Assert.Equal(2, diagnostic.Line);
    }

    [Fact]
    public void SortingProgramCompiles()
    {
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "sort.slav");
        File.WriteAllText(
            source,
            """
            възвати Сварога
            възвати Ярило
            Кнѧзь
                да числа єсть рядъ 3 и 1 и 2
                за ходъ отъ 0 до длъгота числа минусъ 1
                    за указъ отъ 0 до длъгота числа минусъ ходъ минусъ 1
                        да слѣдъ єсть указъ плюсъ 1
                        аще числа по указъ паче числа по слѣдъ
                            да врѣмѧнно се числа по указъ
                            числа по указъ єсть числа по слѣдъ
                            числа по слѣдъ се врѣмѧнно
                        коньць
                    коньць
                коньць
                речи съчѧти " " и числа
            коньць
            """);

        var artifact = new SlavCompiler().Compile(
            new CompilationRequest(source, "sort", Debug: false, References: []),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            artifact.Diagnostics,
            static value => value.Severity == SlavDiagnosticSeverity.Error);
        Assert.NotEmpty(artifact.MainAssemblyBytes);
    }

    [Fact]
    public void UnclosedEntryPointReportsOpeningLine()
    {
        var (source, diagnostics) = SlavSourceEmitter.Emit(
            "възвати Ярило\nКнѧзь\n    речи \"да\"\n",
            "test.slav");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SLAV1006", diagnostic.Code);
        Assert.Equal(2, diagnostic.Line);
        Assert.Contains("Console.WriteLine(\"да\")", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("да числа єсть [1 и 2]")]
    [InlineData("речи длъгота(числа)")]
    [InlineData("да числа єсть рядъ 1, 2")]
    [InlineData("да ответ = 42")]
    [InlineData("аще a == b")]
    public void ClassicPunctuationIsRejected(string command)
    {
        var (_, diagnostics) = SlavSourceEmitter.Emit(
            $"възвати Сварога\nвъзвати Ярило\nКнѧзь\n{command}\nконьць\n",
            "test.slav");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SLAV1010", diagnostic.Code);
    }

    [Theory]
    [InlineData("речи \"свет\"", "Ярило")]
    [InlineData("да числа єсть рядъ 1 и 2", "Сварога")]
    public void StandardConstructRequiresItsInvokedModule(string command, string module)
    {
        var (_, diagnostics) = SlavSourceEmitter.Emit(
            $"Кнѧзь\n{command}\nконьць\n",
            "test.slav");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SLAV1012", diagnostic.Code);
        Assert.Contains($"възвати {module}", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a воистину b", "if (a == b)")]
    [InlineData("a не єсть b", "if (a != b)")]
    [InlineData("a се не b", "if (a != b)")]
    [InlineData("a не менши b", "if (a >= b)")]
    [InlineData("a не паче b", "if (a <= b)")]
    public void WordComparisonIsTranslated(string condition, string expected)
    {
        var (source, diagnostics) = SlavSourceEmitter.Emit(
            $"Кнѧзь\nда a єсть 2\nда b се 1\nаще {condition}\nконьць\nконьць\n",
            "test.slav");

        Assert.Empty(diagnostics);
        Assert.Contains(expected, source, StringComparison.Ordinal);
    }

    [Fact]
    public void WordMutationsCompile()
    {
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "mutations.slav");
        File.WriteAllText(
            source,
            """
            Кнѧзь
                да число єсть 10
                число плюсъ єсть 5
                число минусъ єсть 2
                число множити на 3
                число раздѣлити єсть 2
                число останъкъ єсть 4
                число плюсъ плюсъ
                число умалити
            коньць
            """);

        var artifact = new SlavCompiler().Compile(
            new CompilationRequest(source, "mutations", Debug: false, References: []),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            artifact.Diagnostics,
            static value => value.Severity == SlavDiagnosticSeverity.Error);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
