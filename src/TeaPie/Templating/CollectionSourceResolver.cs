using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TeaPie.Variables;

namespace TeaPie.Templating;

internal sealed partial class CollectionSourceResolver(IVariables variables) : ICollectionSourceResolver
{
    public LoopSource Resolve(string sourceExpression)
    {
        var rangeMatch = NumericRangeRegex().Match(sourceExpression);
        if (rangeMatch.Success)
        {
            var lower = int.Parse(rangeMatch.Groups[1].Value);
            var upper = int.Parse(rangeMatch.Groups[2].Value);
            return new LoopSource(null, Math.Max(0, upper - lower + 1));
        }

        var trimmed = sourceExpression.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '(' && trimmed[^1] == ')')
        {
            return ResolveInlineLiteralList(trimmed[1..^1]);
        }

        if (trimmed.Length > 0 && (trimmed[0] == '(' || trimmed[^1] == ')'))
        {
            throw new InvalidOperationException(
                $"Templating error: inline collection '{trimmed}' has unbalanced parentheses.");
        }

        if (!variables.ContainsVariable(sourceExpression))
        {
            throw new InvalidOperationException(
                $"Templating error: collection variable '{sourceExpression}' referenced in a '{{% for %}}' loop was not found.");
        }

        var value = variables.GetVariable<object?>(sourceExpression);
        if (value is not IEnumerable enumerable)
        {
            throw new InvalidOperationException(
                $"Templating error: variable '{sourceExpression}' referenced in a '{{% for %}}' loop must be a collection.");
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
                    $"Templating error: inline collection '({inner})' has an empty item — remove the extra comma.")
                : ParseLiteralToken(token))
            .ToList();

        return new LoopSource(items, items.Count);
    }

    // Splits on top-level commas only, treating a double-quoted span as opaque so a comma
    // inside a quoted literal (e.g. "a,b") is not mistaken for an item separator. Unlike a
    // single alternation regex, this preserves empty slots between two commas so
    // (1,,3) and (1, , 3) are both caught as an explicit error instead of one of them
    // silently disappearing.
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
            $"Templating error: literal '{token}' inside an inline collection is not a valid quoted string, number, " +
            "or boolean. Use a double-quoted string (\"value\"), a number, or a lowercase boolean (true/false).");
    }

    [GeneratedRegex(@"^\(\s*(\d+)\s*\.\.\s*(\d+)\s*\)$")]
    private static partial Regex NumericRangeRegex();
}
