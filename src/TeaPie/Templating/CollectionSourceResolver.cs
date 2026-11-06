using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TeaPie.Variables;
using static TeaPie.Templating.TemplatingMessageText;

namespace TeaPie.Templating;

internal sealed partial class CollectionSourceResolver(IVariables variables) : ICollectionSourceResolver
{
    public LoopSource Resolve(string sourceExpression, IReadOnlyDictionary<string, object?> model)
    {
        var rangeMatch = NumericRangeRegex().Match(sourceExpression);
        if (rangeMatch.Success)
        {
            return ResolveNumericRange(sourceExpression, rangeMatch);
        }

        var trimmed = sourceExpression.Trim();
        if (UnsupportedRangeRegex().IsMatch(trimmed))
        {
            throw new InvalidOperationException(
                $"Templating error: numeric range '{Shorten(trimmed)}' is not supported: both bounds must be whole numbers " +
                $"from 0 to {int.MaxValue} written directly in the file, such as (1..5). A variable, an expression, " +
                "a negative or a decimal number cannot be a bound.");
        }

        if (trimmed.Length >= 2 && trimmed[0] == '(' && trimmed[^1] == ')')
        {
            return ResolveInlineLiteralList(trimmed[1..^1]);
        }

        if (trimmed.Length > 0 && (trimmed[0] == '(' || trimmed[^1] == ')'))
        {
            throw new InvalidOperationException(
                $"Templating error: inline collection '{Shorten(trimmed)}' has unbalanced parentheses.");
        }

        return ResolveNamedVariable(sourceExpression, model);
    }

    private static LoopSource ResolveNumericRange(string sourceExpression, Match rangeMatch)
    {
        if (!int.TryParse(rangeMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lower) ||
            !int.TryParse(rangeMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var upper))
        {
            throw new InvalidOperationException(
                $"Templating error: numeric range '{Shorten(sourceExpression)}' has a bound that is too large to " +
                $"represent as a 32-bit integer (maximum {int.MaxValue}). Reduce the range so both bounds fit.");
        }

        var count = (long)upper - lower + 1;
        return new LoopSource(null, (int)Math.Clamp(count, 0, int.MaxValue));
    }

    private LoopSource ResolveNamedVariable(string sourceExpression, IReadOnlyDictionary<string, object?> model)
    {
        if (!variables.ContainsVariable(sourceExpression))
        {
            throw new InvalidOperationException(
                $"Templating error: collection variable '{Shorten(sourceExpression)}' referenced in a '{{% for %}}' " +
                "loop was not found.");
        }

        if (!model.TryGetValue(sourceExpression, out var value))
        {
            throw new InvalidOperationException(
                $"Templating error: variable '{Shorten(sourceExpression)}' is tagged as '{Constants.SecretVariableTag}' and " +
                "cannot be used as a '{% for %}' loop source, because its values would appear in the expanded " +
                "requests file and in logs.");
        }

        if (value is not IEnumerable enumerable || value is string)
        {
            var actual = value is null ? ", but it is null" : string.Empty;
            throw new InvalidOperationException(
                $"Templating error: variable '{Shorten(sourceExpression)}' referenced in a '{{% for %}}' loop must be a " +
                $"collection{actual}.");
        }

        var materialized = enumerable.Cast<object?>().ToList();
        return new LoopSource(materialized, materialized.Count);
    }

    private static LoopSource ResolveInlineLiteralList(string inner)
    {
        if (string.IsNullOrWhiteSpace(inner))
        {
            return new LoopSource(new List<object>(), 0);
        }

        var items = SplitTopLevelItems(inner)
            .Select(raw => raw.Trim())
            .Select(token => token.Length == 0
                ? throw new InvalidOperationException(
                    $"Templating error: inline collection '({Shorten(inner)})' has an empty item — remove the extra comma.")
                : ParseLiteralToken(token))
            .ToList();

        return new LoopSource(items, items.Count);
    }

    private static List<string> SplitTopLevelItems(string inner)
    {
        List<string> items = [];
        var current = new StringBuilder();
        var insideQuotes = false;

        foreach (var ch in inner)
        {
            if (ch == '"')
            {
                insideQuotes = !insideQuotes;
                current.Append(ch);
            }
            else if (ch == ',' && !insideQuotes)
            {
                items.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        items.Add(current.ToString());
        return items;
    }

    private static object ParseLiteralToken(string rawToken)
    {
        var token = rawToken.Trim();

        if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
        {
            return token[1..^1];
        }

        if (token is "true" or "false")
        {
            return bool.Parse(token);
        }

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return intValue;
        }

        if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            return longValue;
        }

        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }

        throw new InvalidOperationException(
            $"Templating error: literal '{Shorten(token)}' inside an inline collection is not a valid quoted string, number, " +
            "or boolean. Use a double-quoted string (\"value\"), a number, or a lowercase boolean (true/false).");
    }

    [GeneratedRegex(@"^\(\s*(\d+)\s*\.\.\s*(\d+)\s*\)$", RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex NumericRangeRegex();

    [GeneratedRegex(@"^\([^,()""']*\.\.[^,()""']*\)$", RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex UnsupportedRangeRegex();
}
