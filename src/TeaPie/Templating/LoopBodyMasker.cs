using System.Text.RegularExpressions;

namespace TeaPie.Templating;

internal sealed partial class LoopBodyMasker : ILoopBodyMasker
{
    public string Mask(string body, string loopVariableName)
    {
        var rawRanges = RawBlockRegex().Matches(body);
        var tagRanges = TagRegionRegex().Matches(body);
        var assignedNames = FindAssignedNames(body, rawRanges);

        return TokenRegex().Replace(body, match =>
        {
            if (IsWithinAnyRange(match.Index, rawRanges) || IsWithinAnyRange(match.Index, tagRanges))
            {
                return match.Value;
            }

            var inner = match.Groups[1].Value.Trim();
            return BelongsToLoopScope(inner, loopVariableName, assignedNames)
                ? match.Value
                : $"{{% raw %}}{match.Value}{{% endraw %}}";
        });
    }

    private static HashSet<string> FindAssignedNames(string body, MatchCollection rawRanges)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in AssignTargetRegex().Matches(body))
        {
            if (!IsWithinAnyRange(match.Index, rawRanges))
            {
                names.Add(match.Groups[1].Value);
            }
        }

        return names;
    }

    private static bool IsWithinAnyRange(int index, MatchCollection ranges)
    {
        foreach (Match range in ranges)
        {
            if (index >= range.Index && index < range.Index + range.Length)
            {
                return true;
            }
        }

        return false;
    }

    private static bool BelongsToLoopScope(string expression, string loopVariableName, HashSet<string> assignedNames)
        => StartsWithIdentifier(expression, loopVariableName)
            || StartsWithIdentifier(expression, "forloop")
            || assignedNames.Any(name => StartsWithIdentifier(expression, name));

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

    [GeneratedRegex(@"\{%-?\s*assign\s+([A-Za-z_][A-Za-z0-9_]*)\s*=")]
    private static partial Regex AssignTargetRegex();

    [GeneratedRegex(@"\{%-?.*?-?%\}", RegexOptions.Singleline)]
    private static partial Regex TagRegionRegex();
}
