using System.Text;

namespace RusLang.Compiler;

public static class RusSourceEmitter
{
    public static (string Source, IReadOnlyList<RusDiagnostic> Diagnostics) Emit(
        string source,
        string sourcePath)
    {
        if (source.StartsWith("#csharp", StringComparison.Ordinal))
        {
            var newline = source.IndexOf('\n');
            return (newline < 0 ? string.Empty : source[(newline + 1)..], []);
        }

        var diagnostics = new List<RusDiagnostic>();
        var body = new StringBuilder();
        var lines = source.ReplaceLineEndings("\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var raw = lines[index];
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            body.Append("#line ").Append(index + 1).Append(" \"")
                .Append(EscapePath(sourcePath)).AppendLine("\"");
            if (line.StartsWith("печать ", StringComparison.OrdinalIgnoreCase))
            {
                body.Append("Console.WriteLine(").Append(ToExpression(line[7..])).AppendLine(");");
            }
            else if (line.StartsWith("ошибка ", StringComparison.OrdinalIgnoreCase))
            {
                body.Append("Console.Error.WriteLine(").Append(ToExpression(line[7..])).AppendLine(");");
            }
            else if (line.StartsWith("выход ", StringComparison.OrdinalIgnoreCase))
            {
                body.Append("return ").Append(line[6..].Trim()).AppendLine(";");
            }
            else
            {
                diagnostics.Add(new RusDiagnostic(
                    "RUS1001",
                    RusDiagnosticSeverity.Error,
                    sourcePath,
                    index + 1,
                    Math.Max(1, raw.Length - raw.TrimStart().Length + 1),
                    $"Неизвестная конструкция: {line}"));
            }
        }

        var generated = $$"""
            using System;
            using System.Threading.Tasks;

            internal static class RusLangProgram
            {
                private static int Main(string[] args)
                {
            {{Indent(body.ToString(), 8)}}
                    return 0;
                }
            }
            """;
        return (generated, diagnostics);
    }

    private static string ToExpression(string text)
    {
        var value = text.Trim();
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value
            : $"\"{EscapeText(value)}\"";
    }

    private static string EscapePath(string path) =>
        path.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string EscapeText(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Indent(string value, int spaces)
    {
        var prefix = new string(' ', spaces);
        return string.Join(Environment.NewLine, value.Split('\n').Select(line => prefix + line));
    }
}
