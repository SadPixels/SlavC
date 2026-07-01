using System.Text;
using System.Text.RegularExpressions;

namespace SlavLang.Compiler;

public static partial class SlavSourceEmitter
{
    private enum BlockKind
    {
        EntryPoint,
        If,
        While,
        For,
    }

    private sealed record OpenBlock(BlockKind Kind, int Line, bool HasElse = false);

    public static (string Source, IReadOnlyList<SlavDiagnostic> Diagnostics) Emit(
        string source,
        string sourcePath)
    {
        if (source.StartsWith("#csharp", StringComparison.Ordinal))
        {
            var newline = source.IndexOf('\n');
            return (newline < 0 ? string.Empty : source[(newline + 1)..], []);
        }

        var diagnostics = new List<SlavDiagnostic>();
        var body = new StringBuilder();
        var blocks = new Stack<OpenBlock>();
        var variables = new HashSet<string>(StringComparer.Ordinal);
        var invokedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasEntryPoint = false;
        var insideEntryPoint = false;
        var lines = source.ReplaceLineEndings("\n").Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var raw = lines[index];
            var line = raw.Trim();
            var lineNumber = index + 1;
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (TryReadArgument(line, "възвати", out var module))
            {
                if (hasEntryPoint || !IdentifierPattern().IsMatch(module))
                {
                    AddDiagnostic(diagnostics, sourcePath, lineNumber, raw, "SLAV1007",
                        "Модуль призывается до «Кнѧзя»: възвати Имя");
                    continue;
                }

                invokedModules.Add(CanonicalizeModuleName(module));
                continue;
            }

            if (line.Equals("Кнѧзь", StringComparison.OrdinalIgnoreCase))
            {
                if (hasEntryPoint)
                {
                    AddDiagnostic(diagnostics, sourcePath, lineNumber, raw, "SLAV1008",
                        "В программе может быть только один «Кнѧзь»");
                    continue;
                }

                hasEntryPoint = true;
                insideEntryPoint = true;
                blocks.Push(new OpenBlock(BlockKind.EntryPoint, lineNumber));
                continue;
            }

            if (!insideEntryPoint)
            {
                AddDiagnostic(diagnostics, sourcePath, lineNumber, raw, "SLAV1009",
                    "Исполняемая команда должна находиться между «Кнѧзь» и его «коньць»");
                continue;
            }

            if (ContainsForbiddenSyntax(line, out var forbidden))
            {
                AddDiagnostic(diagnostics, sourcePath, lineNumber, raw, "SLAV1010",
                    $"Символ «{forbidden}» запрещён: используйте словесные операторы SlavLang");
                continue;
            }

            if (UsesSvarogSyntax(line)
                && !RequireModule(
                    invokedModules,
                    "Сварог",
                    diagnostics,
                    sourcePath,
                    lineNumber,
                    raw,
                    "работы с рядами"))
            {
                continue;
            }

            AppendLineDirective(body, sourcePath, lineNumber);

            if (TryReadArgument(line, "речи", out var printValue))
            {
                if (!RequireModule(
                        invokedModules, "Ярило", diagnostics, sourcePath, lineNumber, raw, "речи"))
                {
                    continue;
                }

                body.Append("Console.WriteLine(")
                    .Append(ToOutputExpression(printValue, variables))
                    .AppendLine(");");
            }
            else if (TryReadArgument(line, "плачь", out var errorValue))
            {
                if (!RequireModule(
                        invokedModules, "Ярило", diagnostics, sourcePath, lineNumber, raw, "плачь"))
                {
                    continue;
                }

                body.Append("Console.Error.WriteLine(")
                    .Append(ToOutputExpression(errorValue, variables))
                    .AppendLine(");");
            }
            else if (TryReadArgument(line, "исходъ", out var exitCode))
            {
                body.Append("return ").Append(TranslateExpression(exitCode)).AppendLine(";");
            }
            else if (TryReadArgument(line, "да", out var declaration))
            {
                var match = DeclarationPattern().Match(declaration);
                if (!match.Success)
                {
                    AddDiagnostic(diagnostics, sourcePath, lineNumber, raw, "SLAV1002",
                        "Ожидалось объявление вида: да имя єсть выражение");
                    continue;
                }

                var name = match.Groups["name"].Value;
                variables.Add(name);
                body.Append("var ").Append(name).Append(" = ")
                    .Append(TranslateExpression(match.Groups["expression"].Value))
                    .AppendLine(";");
            }
            else if (TryReadArgument(line, "аще", out var condition))
            {
                blocks.Push(new OpenBlock(BlockKind.If, lineNumber));
                body.Append("if (").Append(TranslateExpression(condition)).AppendLine(")");
                body.AppendLine("{");
            }
            else if (line.Equals("инако", StringComparison.OrdinalIgnoreCase))
            {
                if (blocks.Count == 0 || blocks.Peek().Kind != BlockKind.If || blocks.Peek().HasElse)
                {
                    AddDiagnostic(diagnostics, sourcePath, lineNumber, raw, "SLAV1003",
                        "«инако» должно находиться внутри незавершённого блока «аще»");
                    continue;
                }

                var openIf = blocks.Pop();
                blocks.Push(openIf with { HasElse = true });
                body.AppendLine("}");
                body.AppendLine("else");
                body.AppendLine("{");
            }
            else if (TryReadArgument(line, "дондеже", out condition))
            {
                blocks.Push(new OpenBlock(BlockKind.While, lineNumber));
                body.Append("while (").Append(TranslateExpression(condition)).AppendLine(")");
                body.AppendLine("{");
            }
            else if (TryReadArgument(line, "за", out var forClause))
            {
                var match = ForPattern().Match(forClause);
                if (!match.Success)
                {
                    AddDiagnostic(diagnostics, sourcePath, lineNumber, raw, "SLAV1004",
                        "Ожидался цикл вида: за имя отъ начало до граница");
                    continue;
                }

                var name = match.Groups["name"].Value;
                var start = TranslateExpression(match.Groups["start"].Value);
                var end = TranslateExpression(match.Groups["end"].Value);
                blocks.Push(new OpenBlock(BlockKind.For, lineNumber));
                variables.Add(name);
                body.Append("for (var ").Append(name).Append(" = ").Append(start)
                    .Append("; ").Append(name).Append(" < ").Append(end)
                    .Append("; ").Append(name).AppendLine("++)");
                body.AppendLine("{");
            }
            else if (line.Equals("коньць", StringComparison.OrdinalIgnoreCase))
            {
                if (blocks.Count == 0)
                {
                    AddDiagnostic(diagnostics, sourcePath, lineNumber, raw, "SLAV1005",
                        "Лишнее слово «коньць»: открытого блока нет");
                    continue;
                }

                var closedBlock = blocks.Pop();
                if (closedBlock.Kind == BlockKind.EntryPoint)
                {
                    insideEntryPoint = false;
                }
                else
                {
                    body.AppendLine("}");
                }
            }
            else if (line.Equals("прервати", StringComparison.OrdinalIgnoreCase))
            {
                body.AppendLine("break;");
            }
            else if (line.Equals("продолжити", StringComparison.OrdinalIgnoreCase))
            {
                body.AppendLine("continue;");
            }
            else
            {
                if (TryTranslateMutation(line, out var mutation))
                {
                    body.AppendLine(mutation);
                    continue;
                }

                var assignment = AssignmentPattern().Match(line);
                if (assignment.Success)
                {
                    body.Append(TranslateTarget(assignment.Groups["target"].Value)).Append(" = ")
                        .Append(TranslateExpression(assignment.Groups["expression"].Value))
                        .AppendLine(";");
                    continue;
                }

                AddDiagnostic(diagnostics, sourcePath, lineNumber, raw, "SLAV1001",
                    $"Неизвестная конструкция: {line}");
            }
        }

        if (!hasEntryPoint)
        {
            diagnostics.Add(new SlavDiagnostic(
                "SLAV1011",
                SlavDiagnosticSeverity.Error,
                sourcePath,
                1,
                1,
                "Точка входа «Кнѧзь» не найдена"));
        }

        foreach (var block in blocks)
        {
            diagnostics.Add(new SlavDiagnostic(
                "SLAV1006",
                SlavDiagnosticSeverity.Error,
                sourcePath,
                block.Line,
                1,
                "Блок не закрыт словом «коньць»"));
        }

        var generated = $$"""
            using System;
            using System.Collections.Generic;

            internal static class SlavLangProgram
            {
                private static int длъгота<T>(T[] значения) => значения.Length;

                private static string съчѧти<T>(string разделитель, IEnumerable<T> значения) =>
                    string.Join(разделитель, значения);

                private static int Main(string[] args)
                {
            {{Indent(body.ToString(), 8)}}
                    return 0;
                }
            }
            """;
        return (generated, diagnostics);
    }

    private static bool TryReadArgument(string line, string keyword, out string argument)
    {
        if (line.StartsWith(keyword + " ", StringComparison.OrdinalIgnoreCase))
        {
            argument = line[(keyword.Length + 1)..].Trim();
            return true;
        }

        argument = string.Empty;
        return false;
    }

    private static string ToOutputExpression(string text, IReadOnlySet<string> variables)
    {
        var value = text.Trim();
        if (LooksLikeExpression(value, variables))
        {
            return TranslateExpression(value);
        }

        return $"\"{EscapeText(value)}\"";
    }

    private static bool LooksLikeExpression(string value, IReadOnlySet<string> variables)
    {
        if (value.Length == 0)
        {
            return false;
        }

        if (value[0] == '"' || char.IsDigit(value[0]) || value[0] == '-')
        {
            return true;
        }

        var firstWord = IdentifierAtStartPattern().Match(value).Value;
        return variables.Contains(firstWord)
            || firstWord.Equals("рядъ", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("длъгота", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("съчѧти", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("истинно", StringComparison.OrdinalIgnoreCase)
            || firstWord.Equals("лъжа", StringComparison.OrdinalIgnoreCase)
            || value.Contains(" по ", StringComparison.OrdinalIgnoreCase)
            || value.Contains('+')
            || value.Contains('*')
            || value.Contains('/');
    }

    private static string TranslateExpression(string expression)
    {
        var value = expression.Trim();
        if (value.StartsWith("рядъ ", StringComparison.OrdinalIgnoreCase))
        {
            var elements = SplitByAnd(value["рядъ ".Length..]);
            return $"new[] {{ {string.Join(", ", elements.Select(TranslateExpression))} }}";
        }

        var join = JoinPattern().Match(value);
        if (join.Success)
        {
            return $"съчѧти({TranslateExpression(join.Groups["separator"].Value)}, " +
                $"{join.Groups["values"].Value})";
        }

        value = IndexAccessPattern().Replace(
            value,
            static match => $"{match.Groups["array"].Value}[{match.Groups["index"].Value}]");
        value = LengthPattern().Replace(
            value,
            static match => $"длъгота({match.Groups["values"].Value})");
        value = ReplaceExpressionPhrases(value);

        var result = new StringBuilder(value.Length);
        var inString = false;
        var escaped = false;
        for (var index = 0; index < value.Length;)
        {
            var character = value[index];
            if (inString)
            {
                result.Append(character);
                index++;
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
                result.Append(character);
                index++;
                continue;
            }

            if (char.IsLetter(character) || character == '_')
            {
                var start = index++;
                while (index < value.Length
                       && (char.IsLetterOrDigit(value[index]) || value[index] == '_'))
                {
                    index++;
                }

                var word = value[start..index];
                result.Append(word.ToLowerInvariant() switch
                {
                    "истинно" => "true",
                    "лъжа" => "false",
                    "и" => "&&",
                    "либо" => "||",
                    "не" => "!",
                    "плюсъ" => "+",
                    "минусъ" => "-",
                    "множити" => "*",
                    "раздѣлити" => "/",
                    "останъкъ" => "%",
                    "паче" => ">",
                    "менши" => "<",
                    _ => word,
                });
                continue;
            }

            result.Append(character);
            index++;
        }

        return result.ToString();
    }

    private static string TranslateTarget(string target)
    {
        var index = IndexTargetPattern().Match(target);
        return index.Success
            ? $"{index.Groups["array"].Value}[{index.Groups["index"].Value}]"
            : target;
    }

    private static bool TryTranslateMutation(string line, out string source)
    {
        var unary = UnaryMutationPattern().Match(line);
        if (unary.Success)
        {
            var target = TranslateTarget(unary.Groups["target"].Value);
            var operation = unary.Groups["operation"].Value.ToLowerInvariant() switch
            {
                "плюсъ плюсъ" or "възрастити" => "++",
                _ => "--",
            };
            source = $"{target}{operation};";
            return true;
        }

        var compound = CompoundAssignmentPattern().Match(line);
        if (compound.Success)
        {
            var target = TranslateTarget(compound.Groups["target"].Value);
            var operation = Regex.Replace(
                compound.Groups["operation"].Value.ToLowerInvariant(),
                @"\s+",
                " ") switch
            {
                "плюсъ єсть" or "приложити" => "+=",
                "минусъ єсть" or "отъяти" => "-=",
                "множити єсть" or "множити на" => "*=",
                "раздѣлити єсть" or "раздѣлити на" => "/=",
                _ => "%=",
            };
            source = $"{target} {operation} {TranslateExpression(compound.Groups["expression"].Value)};";
            return true;
        }

        source = string.Empty;
        return false;
    }

    private static string ReplaceExpressionPhrases(string value)
    {
        var result = new StringBuilder(value.Length);
        var segmentStart = 0;
        for (var index = 0; index < value.Length;)
        {
            if (value[index] != '"')
            {
                index++;
                continue;
            }

            result.Append(ReplaceExpressionPhrasesInSegment(value[segmentStart..index]));
            var stringStart = index++;
            var escaped = false;
            while (index < value.Length)
            {
                var character = value[index++];
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    break;
                }
            }

            result.Append(value[stringStart..index]);
            segmentStart = index;
        }

        result.Append(ReplaceExpressionPhrasesInSegment(value[segmentStart..]));
        return result.ToString();
    }

    private static string ReplaceExpressionPhrasesInSegment(string value) =>
        ExpressionPhrasePattern().Replace(
            value,
            static match => Regex.Replace(match.Value.ToLowerInvariant(), @"\s+", " ") switch
            {
                "не єсть" or "се не" => "!=",
                "воистину" => "==",
                "не менши" => ">=",
                "не паче" => "<=",
                _ => match.Value,
            });

    private static IReadOnlyList<string> SplitByAnd(string value)
    {
        var parts = new List<string>();
        var start = 0;
        var inString = false;
        var escaped = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }

            if (index > 0
                && index + 2 < value.Length
                && char.IsWhiteSpace(value[index - 1])
                && (character is 'и' or 'И')
                && char.IsWhiteSpace(value[index + 1]))
            {
                parts.Add(value[start..index].Trim());
                start = index + 1;
            }
        }

        parts.Add(value[start..].Trim());
        return parts;
    }

    private static bool ContainsForbiddenSyntax(string value, out char forbidden)
    {
        var inString = false;
        var escaped = false;
        foreach (var character in value)
        {
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
            }
            else if (character is
                     '(' or ')' or '[' or ']' or '{' or '}' or ',' or
                     '=' or '!' or '<' or '>' or '+' or '-' or '*' or '/' or '%')
            {
                forbidden = character;
                return true;
            }
        }

        forbidden = default;
        return false;
    }

    private static bool UsesSvarogSyntax(string value)
    {
        var inString = false;
        var escaped = false;
        for (var index = 0; index < value.Length;)
        {
            var character = value[index];
            if (inString)
            {
                index++;
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
                index++;
                continue;
            }

            if (char.IsLetter(character) || character == '_')
            {
                var start = index++;
                while (index < value.Length
                       && (char.IsLetterOrDigit(value[index]) || value[index] == '_'))
                {
                    index++;
                }

                var word = value[start..index];
                if (word.Equals("рядъ", StringComparison.OrdinalIgnoreCase)
                    || word.Equals("длъгота", StringComparison.OrdinalIgnoreCase)
                    || word.Equals("съчѧти", StringComparison.OrdinalIgnoreCase)
                    || word.Equals("по", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            index++;
        }

        return false;
    }

    private static bool RequireModule(
        IReadOnlySet<string> modules,
        string module,
        ICollection<SlavDiagnostic> diagnostics,
        string sourcePath,
        int lineNumber,
        string raw,
        string construct)
    {
        if (modules.Contains(module))
        {
            return true;
        }

        AddDiagnostic(
            diagnostics,
            sourcePath,
            lineNumber,
            raw,
            "SLAV1012",
            $"Для «{construct}» добавьте перед «Кнѧзем»: възвати " +
            $"{(module == "Сварог" ? "Сварога" : module)}");
        return false;
    }

    private static string CanonicalizeModuleName(string module) =>
        module.Equals("Сварога", StringComparison.OrdinalIgnoreCase)
            ? "Сварог"
            : module;

    private static void AppendLineDirective(StringBuilder body, string sourcePath, int lineNumber) =>
        body.Append("#line ").Append(lineNumber).Append(" \"")
            .Append(EscapePath(sourcePath)).AppendLine("\"");

    private static void AddDiagnostic(
        ICollection<SlavDiagnostic> diagnostics,
        string sourcePath,
        int lineNumber,
        string raw,
        string code,
        string message) =>
        diagnostics.Add(new SlavDiagnostic(
            code,
            SlavDiagnosticSeverity.Error,
            sourcePath,
            lineNumber,
            Math.Max(1, raw.Length - raw.TrimStart().Length + 1),
            message));

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

    [GeneratedRegex(
        @"^(?<name>[\p{L}_][\p{L}\p{Nd}_]*)\s+(?:єсть|се)\s+(?<expression>.+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex DeclarationPattern();

    [GeneratedRegex(
        @"^(?<target>[\p{L}_][\p{L}\p{Nd}_]*(?:\s+по\s+[\p{L}\p{Nd}_]+)?)\s+" +
        @"(?:єсть|се)\s+(?<expression>.+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex AssignmentPattern();

    [GeneratedRegex(
        @"^(?<name>[\p{L}_][\p{L}\p{Nd}_]*)\s+отъ\s+(?<start>.+?)\s+до\s+(?<end>.+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex ForPattern();

    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{Nd}_]*")]
    private static partial Regex IdentifierAtStartPattern();

    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{Nd}_]*$")]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex(
        "^съчѧти\\s+(?<separator>\"(?:\\\\.|[^\"])*\"|[\\p{L}\\p{Nd}_]+)\\s+и\\s+" +
        "(?<values>[\\p{L}_][\\p{L}\\p{Nd}_]*)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex JoinPattern();

    [GeneratedRegex(
        @"\bдлъгота\s+(?<values>[\p{L}_][\p{L}\p{Nd}_]*)",
        RegexOptions.IgnoreCase)]
    private static partial Regex LengthPattern();

    [GeneratedRegex(
        @"(?<array>[\p{L}_][\p{L}\p{Nd}_]*)\s+по\s+(?<index>[\p{L}\p{Nd}_]+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex IndexAccessPattern();

    [GeneratedRegex(
        @"^(?<array>[\p{L}_][\p{L}\p{Nd}_]*)\s+по\s+(?<index>[\p{L}\p{Nd}_]+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex IndexTargetPattern();

    [GeneratedRegex(
        @"^(?<target>[\p{L}_][\p{L}\p{Nd}_]*(?:\s+по\s+[\p{L}\p{Nd}_]+)?)\s+" +
        @"(?<operation>плюсъ\s+плюсъ|минусъ\s+минусъ|възрастити|умалити)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex UnaryMutationPattern();

    [GeneratedRegex(
        @"^(?<target>[\p{L}_][\p{L}\p{Nd}_]*(?:\s+по\s+[\p{L}\p{Nd}_]+)?)\s+" +
        @"(?<operation>плюсъ\s+єсть|минусъ\s+єсть|множити\s+єсть|раздѣлити\s+єсть|" +
        @"останъкъ\s+єсть|приложити|отъяти|множити\s+на|раздѣлити\s+на|" +
        @"останъкъ\s+отъ)\s+(?<expression>.+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex CompoundAssignmentPattern();

    [GeneratedRegex(
        @"\b(?:не\s+єсть|се\s+не|воистину|не\s+менши|не\s+паче)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ExpressionPhrasePattern();
}
