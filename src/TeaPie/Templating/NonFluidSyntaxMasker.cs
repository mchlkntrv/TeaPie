using System.Text.RegularExpressions;

namespace TeaPie.Templating;

internal sealed partial class NonFluidSyntaxMasker : INonFluidSyntaxMasker
{
    public IReadOnlyList<TextEdit> FindMaskEdits(string content, IReadOnlyList<LoopBlock> blocks)
    {
        var literalRanges = LiteralBlocks.Find(content);
        var tagRanges = TagRegionRegex().Matches(content);
        var assignOccurrences = AssignTags.FindOccurrences(content, literalRanges);
        var functionTokenMatches = FunctionTokenRegex().Matches(content);
        var plainTokenMatches = TokenRegex().Matches(content);
        var edits = new List<TextEdit>();

        foreach (Match tag in tagRanges)
        {
            var bracePosition = tag.Index - 1;
            if (!TextRanges.Contains(literalRanges, bracePosition)
                && !TextRanges.Contains(functionTokenMatches, bracePosition)
                && !TextRanges.Contains(plainTokenMatches, bracePosition))
            {
                AddPrecedingBracesEdit(content, tag.Index, edits);
            }
        }

        foreach (Match match in FindTokenMatches(functionTokenMatches, plainTokenMatches))
        {
            if (TextRanges.Contains(literalRanges, match.Index) || TextRanges.Contains(tagRanges, match.Index))
            {
                continue;
            }

            var inner = StripWhitespaceControl(match.Groups[1].Value).Trim();
            var enclosingBlockIndex = FindEnclosingBlockIndex(match.Index, blocks);

            AddPrecedingBracesEdit(content, match.Index, edits);

            if (!BelongsToScope(inner, match.Index, enclosingBlockIndex, blocks, assignOccurrences))
            {
                edits.Add(new TextEdit(match.Index, match.Length, $"{{% raw %}}{match.Value}{{% endraw %}}"));
            }
        }

        return edits;
    }

    private static string StripWhitespaceControl(string inner)
    {
        var start = inner.StartsWith('-') ? 1 : 0;
        var end = inner.Length > start && inner.EndsWith('-') ? inner.Length - 1 : inner.Length;
        return inner[start..end];
    }

    private static void AddPrecedingBracesEdit(string content, int syntaxIndex, List<TextEdit> edits)
    {
        var start = syntaxIndex;
        while (start > 0 && content[start - 1] == '{')
        {
            start--;
        }

        if (start < syntaxIndex)
        {
            edits.Add(new TextEdit(start, syntaxIndex - start, $"{{% raw %}}{content[start..syntaxIndex]}{{% endraw %}}"));
        }
    }

    private static int? FindEnclosingBlockIndex(int position, IReadOnlyList<LoopBlock> blocks)
    {
        var enclosing = LoopBlockHierarchy.GetEnclosingBlockIndicesInnermostFirst(position, blocks);
        return enclosing.Count > 0 ? enclosing[0] : null;
    }

    private static bool BelongsToScope(
        string expression,
        int position,
        int? enclosingBlockIndex,
        IReadOnlyList<LoopBlock> blocks,
        List<AssignTags.Occurrence> assignOccurrences)
    {
        if (expression.StartsWith('$'))
        {
            return false;
        }

        if (FluidExpressionIdentifier.ReferencesIdentifier(expression, "forloop"))
        {
            return true;
        }

        var visibleBefore = position;

        if (enclosingBlockIndex is int blockIndex)
        {
            var chain = new List<int> { blockIndex };
            chain.AddRange(LoopBlockHierarchy.GetAncestorIndices(blockIndex, blocks));

            if (chain.Exists(ancestorIndex =>
                FluidExpressionIdentifier.ReferencesIdentifier(expression, blocks[ancestorIndex].LoopVariableName)))
            {
                return true;
            }

            var outermost = blocks[chain[^1]];
            visibleBefore = outermost.StartIndex + outermost.Length;
        }

        return assignOccurrences.Exists(occurrence =>
            occurrence.Position < visibleBefore
            && FluidExpressionIdentifier.ReferencesIdentifier(expression, occurrence.Name));
    }

    private static IEnumerable<Match> FindTokenMatches(
        MatchCollection functionTokenMatches, MatchCollection plainTokenMatches)
    {
        foreach (Match match in functionTokenMatches)
        {
            yield return match;
        }

        foreach (Match match in plainTokenMatches)
        {
            if (!TextRanges.Contains(functionTokenMatches, match.Index))
            {
                yield return match;
            }
        }
    }

    [GeneratedRegex("\\{\\{((?:\"[^\"]*\"|[^{}\"])*)\\}\\}", RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(
        "\\{\\{(\\$(?>\"[^\"]*\"|'[^']*'|\\{\\{(?<D>)|\\}\\}(?<-D>)|[^{}])*(?(D)(?!)))\\}\\}",
        RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex FunctionTokenRegex();

    [GeneratedRegex(@"\{%-?.*?-?%\}", RegexOptions.Singleline, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex TagRegionRegex();
}
