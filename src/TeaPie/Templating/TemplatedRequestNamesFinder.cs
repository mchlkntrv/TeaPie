namespace TeaPie.Templating;

internal static class TemplatedRequestNamesFinder
{
    public static TemplatedRequestNames Find(
        string content, string rendered, IReadOnlyList<LoopBlock> blocks, IReadOnlyList<string> loopOutputs)
    {
        var loopCounts = RequestNameDeclarations.Count(string.Join('\n', loopOutputs));
        var commentRanges = LiteralBlocks.Find(content)
            .Where(range => range.Groups[1].Value == "comment")
            .ToList();
        var innermostLoopsOfLiteralDeclarations = RequestNameDeclarations.Find(content)
            .Where(declaration => declaration.IsLiteral && !TextRanges.Contains(commentRanges, declaration.Index))
            .ToLookup(
                declaration => declaration.Name,
                declaration => LoopBlockHierarchy.GetEnclosingBlockIndicesInnermostFirst(declaration.Index, blocks)
                    .DefaultIfEmpty(-1)
                    .First(),
                StringComparer.Ordinal);

        var repeatable = RequestNameDeclarations.Count(rendered)
            .Where(entry => entry.Value > 1
                && loopCounts.GetValueOrDefault(entry.Key) >= entry.Value
                && IsWrittenAtMostOncePerLoopAndOnlyInLoops(innermostLoopsOfLiteralDeclarations[entry.Key]))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);

        return new TemplatedRequestNames(repeatable, loopCounts.Keys.ToHashSet(StringComparer.Ordinal));
    }

    private static bool IsWrittenAtMostOncePerLoopAndOnlyInLoops(IEnumerable<int> innermostLoopIndices)
    {
        var indices = innermostLoopIndices.ToList();
        return indices.Count > 0 && !indices.Contains(-1) && indices.Distinct().Count() == indices.Count;
    }
}
