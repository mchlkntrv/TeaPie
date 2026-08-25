using System.Text.RegularExpressions;

namespace TeaPie.Templating;

internal sealed partial class LoopBodyMasker : ILoopBodyMasker
{
    public string Mask(string body, string loopVariableName)
    {
        var rawRanges = RawBlockRegex().Matches(body);

        return TokenRegex().Replace(body, match =>
        {
            if (IsWithinAnyRawBlock(match.Index, rawRanges))
            {
                return match.Value;
            }

            var inner = match.Groups[1].Value.Trim();
            return BelongsToLoopScope(inner, loopVariableName)
                ? match.Value
                : $"{{% raw %}}{match.Value}{{% endraw %}}";
        });
    }

    private static bool IsWithinAnyRawBlock(int index, MatchCollection rawRanges)
    {
        foreach (Match range in rawRanges)
        {
            if (index >= range.Index && index < range.Index + range.Length)
            {
                return true;
            }
        }

        return false;
    }

    private static bool BelongsToLoopScope(string expression, string loopVariableName)
        => StartsWithIdentifier(expression, loopVariableName) || StartsWithIdentifier(expression, "forloop");

    private static bool StartsWithIdentifier(string expression, string identifier)
    {
        if (!expression.StartsWith(identifier, StringComparison.Ordinal))
        {
            return false;
        }

        return expression.Length == identifier.Length || expression[identifier.Length] is '.' or ' ' or '|' or '\t';
    }

    [GeneratedRegex("\\{\\{((?:\"[^\"]*\"|[^{}\"])*)\\}\\}")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\{%-?\s*raw\s*-?%\}.*?\{%-?\s*endraw\s*-?%\}", RegexOptions.Singleline)]
    private static partial Regex RawBlockRegex();
}
