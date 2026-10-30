using System.Globalization;
using System.Text;

namespace TeaPie.Templating;

internal sealed record ForTagSource(
    string CollectionExpression,
    string FluidOptions,
    bool IsReversed,
    string? OffsetExpression)
{
    public string ToFluidSource(string collectionExpression) => collectionExpression + FluidOptions;
}

internal static class ForTagSourceParser
{
    private const string ReversedOption = "reversed";
    private const string LimitOption = "limit:";
    private const string OffsetOption = "offset:";
    private const string ContinueOffset = "continue";
    private const string SupportedOptions = "'limit:', 'offset:' and 'reversed'";

    public static bool IsValidOptionValue(decimal value, int maxOptionValue)
        => value == decimal.Truncate(value) && value >= 0 && value <= maxOptionValue;

    public static ForTagSource Parse(string sourceExpression, int maxOptionValue)
    {
        EnsureNoFilters(sourceExpression);

        var tokens = SplitTopLevelTokens(sourceExpression);
        if (tokens.Count == 0)
        {
            throw new InvalidOperationException("Templating error: '{% for %}' tag is missing a collection.");
        }

        var options = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var isReversed = false;
        string? offsetExpression = null;

        for (var i = 1; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (token == ReversedOption)
            {
                EnsureNotDuplicate(seen, token, sourceExpression);
                isReversed = true;
                options.Append(' ').Append(ReversedOption);
                continue;
            }

            var name = token.StartsWith(LimitOption, StringComparison.Ordinal) ? LimitOption
                : token.StartsWith(OffsetOption, StringComparison.Ordinal) ? OffsetOption
                : throw new InvalidOperationException(
                    $"Templating error: unsupported option '{token}' in the '{{% for %}}' source " +
                    $"'{sourceExpression}'. Supported options are {SupportedOptions}.");

            EnsureNotDuplicate(seen, name, sourceExpression);

            var value = token[name.Length..];
            if (value.Length == 0)
            {
                value = i + 1 < tokens.Count
                    ? tokens[++i]
                    : throw new InvalidOperationException(
                        $"Templating error: option '{name}' in the '{{% for %}}' source '{sourceExpression}' " +
                        "is missing a value.");
            }

            if (name == OffsetOption && value == ContinueOffset)
            {
                throw new InvalidOperationException(
                    "Templating error: 'offset: continue' is not supported in the '{% for %}' source " +
                    $"'{sourceExpression}'. Use a number or a variable.");
            }

            options.Append(' ').Append(name).Append(' ').Append(value);

            EnsureValidLiteralOptionValue(name, value, sourceExpression, maxOptionValue);
            if (name == OffsetOption)
            {
                offsetExpression = value;
            }
        }

        return new ForTagSource(tokens[0], options.ToString(), isReversed, offsetExpression);
    }

    private static void EnsureValidLiteralOptionValue(
        string name, string value, string sourceExpression, int maxOptionValue)
    {
        if (char.IsLetter(value[0]) || value[0] == '_')
        {
            return;
        }

        if (!decimal.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            || !IsValidOptionValue(parsed, maxOptionValue))
        {
            throw new InvalidOperationException(
                $"Templating error: option '{name}' in the '{{% for %}}' source '{sourceExpression}' must be a " +
                $"whole number from 0 to {maxOptionValue} or a variable, but is '{value}'.");
        }
    }

    private static void EnsureNoFilters(string sourceExpression)
    {
        if (IndexOfTopLevel(sourceExpression, '|') >= 0)
        {
            throw new InvalidOperationException(
                $"Templating error: filters are not supported in the '{{% for %}}' source '{sourceExpression}'. " +
                $"A '{{% for %}}' tag only accepts a collection followed by the options {SupportedOptions}.");
        }
    }

    private static void EnsureNotDuplicate(HashSet<string> seen, string option, string sourceExpression)
    {
        if (!seen.Add(option))
        {
            throw new InvalidOperationException(
                $"Templating error: option '{option}' is specified more than once in the '{{% for %}}' source " +
                $"'{sourceExpression}'.");
        }
    }

    private static int IndexOfTopLevel(string text, char target)
    {
        var depth = 0;
        char? quote = null;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (quote is not null)
            {
                quote = ch == quote ? null : quote;
            }
            else if (ch is '"' or '\'')
            {
                quote = ch;
            }
            else if (ch is '(' or '[')
            {
                depth++;
            }
            else if (ch is ')' or ']')
            {
                depth--;
            }
            else if (ch == target && depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static List<string> SplitTopLevelTokens(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        char? quote = null;

        foreach (var ch in text)
        {
            if (quote is not null)
            {
                quote = ch == quote ? null : quote;
            }
            else if (ch is '"' or '\'')
            {
                quote = ch;
            }
            else if (ch is '(' or '[')
            {
                depth++;
            }
            else if (ch is ')' or ']')
            {
                depth--;
            }
            else if (char.IsWhiteSpace(ch) && depth == 0)
            {
                FlushToken(tokens, current);
                continue;
            }

            current.Append(ch);
        }

        FlushToken(tokens, current);
        return tokens;
    }

    private static void FlushToken(List<string> tokens, StringBuilder current)
    {
        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
            current.Clear();
        }
    }
}
