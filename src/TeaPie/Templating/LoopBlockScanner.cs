using System.Text.RegularExpressions;

namespace TeaPie.Templating;

internal sealed partial class LoopBlockScanner : ILoopBlockScanner
{
    public IReadOnlyList<LoopBlock> FindLoopBlocks(string content)
    {
        var tags = ForTagRegex().Matches(content).Cast<Match>().Select(m => (Match: m, IsFor: true))
            .Concat(EndForTagRegex().Matches(content).Cast<Match>().Select(m => (Match: m, IsFor: false)))
            .OrderBy(t => t.Match.Index)
            .ToList();

        var stack = new Stack<Match>();
        var blocks = new List<LoopBlock>();

        foreach (var (match, isFor) in tags)
        {
            if (isFor)
            {
                stack.Push(match);
                continue;
            }

            if (stack.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Templating error: found '{match.Value}' without a matching '{{% for %}}'.");
            }

            var openMatch = stack.Pop();
            var bodyStart = openMatch.Index + openMatch.Length;
            var loopVariableName = openMatch.Groups[2].Value;
            var sourceExpressionGroup = openMatch.Groups[3];
            var sourceExpression = sourceExpressionGroup.Value.Trim();
            var body = content[bodyStart..match.Index];
            var length = match.Index + match.Length - openMatch.Index;

            blocks.Add(new LoopBlock(
                loopVariableName, sourceExpression, body, openMatch.Index, length,
                sourceExpressionGroup.Index, sourceExpressionGroup.Length));
        }

        if (stack.Count > 0)
        {
            var unclosed = stack.Peek();
            throw new InvalidOperationException(
                $"Templating error: missing '{{% endfor %}}' for the loop starting with '{unclosed.Value}'.");
        }

        EnsureNoResidualForTags(content, blocks);

        return blocks;
    }

    private static void EnsureNoResidualForTags(string content, List<LoopBlock> blocks)
    {
        foreach (Match match in ForTagFamilyRegex().Matches(content))
        {
            if (!IsWithinAnyBlock(match.Index, blocks))
            {
                throw new InvalidOperationException(
                    $"Templating error: found a stray or malformed loop tag '{match.Value}' that is not part of " +
                    "a valid '{% for %}' ... '{% endfor %}' block.");
            }
        }
    }

    private static bool IsWithinAnyBlock(int index, List<LoopBlock> blocks)
        => blocks.Exists(block => index >= block.StartIndex && index < block.StartIndex + block.Length);

    [GeneratedRegex(@"\{%(-)?\s*for\s+([A-Za-z_][A-Za-z0-9_]*)\s+in\s+(.+?)\s*(-)?%\}")]
    private static partial Regex ForTagRegex();

    [GeneratedRegex(@"\{%(-)?\s*endfor\s*(-)?%\}")]
    private static partial Regex EndForTagRegex();

    [GeneratedRegex(@"\{%-?\s*(for|endfor)\b[^%]*-?%\}")]
    private static partial Regex ForTagFamilyRegex();
}
