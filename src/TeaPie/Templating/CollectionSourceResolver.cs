using System.Collections;
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

    [GeneratedRegex(@"^\(\s*(\d+)\s*\.\.\s*(\d+)\s*\)$")]
    private static partial Regex NumericRangeRegex();
}
