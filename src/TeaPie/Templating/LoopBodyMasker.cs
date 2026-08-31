using System.Text.RegularExpressions;

namespace TeaPie.Templating;

internal sealed partial class LoopBodyMasker : ILoopBodyMasker
{
    private readonly record struct AssignOccurrence(string Name, int Position, int? EnclosingBlockIndex);

    public IReadOnlyList<TextEdit> FindMaskEdits(string content, IReadOnlyList<LoopBlock> blocks)
    {
        var rawRanges = RawBlockRegex().Matches(content);
        var tagRanges = TagRegionRegex().Matches(content);
        var assignOccurrences = FindAssignOccurrences(content, blocks, rawRanges);
        var edits = new List<TextEdit>();

        foreach (Match match in TokenRegex().Matches(content))
        {
            if (IsWithinAnyRange(match.Index, rawRanges) || IsWithinAnyRange(match.Index, tagRanges))
            {
                continue;
            }

            var inner = match.Groups[1].Value.Trim();
            var enclosingBlockIndex = FindEnclosingBlockIndex(match.Index, blocks);

            if (!BelongsToScope(inner, match.Index, enclosingBlockIndex, blocks, assignOccurrences))
            {
                edits.Add(new TextEdit(match.Index, match.Length, $"{{% raw %}}{match.Value}{{% endraw %}}"));
            }
        }

        return edits;
    }

    public IReadOnlySet<string> FindTopLevelAssignTargetNames(string content, IReadOnlyList<LoopBlock> blocks)
    {
        var rawRanges = RawBlockRegex().Matches(content);
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var occurrence in FindAssignOccurrences(content, blocks, rawRanges))
        {
            if (occurrence.EnclosingBlockIndex is null)
            {
                names.Add(occurrence.Name);
            }
        }

        return names;
    }

    private static List<AssignOccurrence> FindAssignOccurrences(
        string content, IReadOnlyList<LoopBlock> blocks, MatchCollection rawRanges)
    {
        var occurrences = new List<AssignOccurrence>();

        foreach (Match match in AssignTargetRegex().Matches(content))
        {
            if (IsWithinAnyRange(match.Index, rawRanges))
            {
                continue;
            }

            occurrences.Add(new AssignOccurrence(
                match.Groups[1].Value, match.Index, FindEnclosingBlockIndex(match.Index, blocks)));
        }

        return occurrences;
    }

    private static int? FindEnclosingBlockIndex(int position, IReadOnlyList<LoopBlock> blocks)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            if (position >= blocks[i].StartIndex && position < blocks[i].StartIndex + blocks[i].Length)
            {
                return i;
            }
        }

        return null;
    }

    private static bool BelongsToScope(
        string expression,
        int position,
        int? enclosingBlockIndex,
        IReadOnlyList<LoopBlock> blocks,
        List<AssignOccurrence> assignOccurrences)
    {
        if (enclosingBlockIndex is int blockIndex)
        {
            var block = blocks[blockIndex];

            if (StartsWithIdentifier(expression, block.LoopVariableName) || StartsWithIdentifier(expression, "forloop"))
            {
                return true;
            }

            if (assignOccurrences.Exists(occurrence =>
                occurrence.EnclosingBlockIndex == blockIndex && StartsWithIdentifier(expression, occurrence.Name)))
            {
                return true;
            }

            return assignOccurrences.Exists(occurrence =>
                occurrence.EnclosingBlockIndex is null
                && occurrence.Position < block.StartIndex
                && StartsWithIdentifier(expression, occurrence.Name));
        }

        return assignOccurrences.Exists(occurrence =>
            occurrence.EnclosingBlockIndex is null
            && occurrence.Position < position
            && StartsWithIdentifier(expression, occurrence.Name));
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
