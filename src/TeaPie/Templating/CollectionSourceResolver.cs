using System.Collections;
using System.Globalization;
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
        var items = LiteralItemRegex().Matches(inner)
            .Select(match => ParseLiteralToken(match.Value))
            .ToList();

        return new LoopSource(items, items.Count);
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

        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }

        throw new InvalidOperationException(
            $"Templating error: literal '{token}' inside an inline collection is not a valid quoted string, number, or boolean.");
    }

    [GeneratedRegex(@"^\(\s*(\d+)\s*\.\.\s*(\d+)\s*\)$")]
    private static partial Regex NumericRangeRegex();

    [GeneratedRegex("\"[^\"]*\"|[^,]+")]
    private static partial Regex LiteralItemRegex();
}
