using System.Text.RegularExpressions;

namespace TeaPie.Templating;

internal sealed partial class LoopBlockScanner : ILoopBlockScanner
{
    public IReadOnlyList<LoopBlock> FindLoopBlocks(string content)
    {
        List<LoopBlock> blocks = [];
        var position = 0;

        while (true)
        {
            var openMatch = ForTagRegex().Match(content, position);
            if (!openMatch.Success)
            {
                break;
            }

            var bodyStart = openMatch.Index + openMatch.Length;
            var closeMatch = EndForTagRegex().Match(content, bodyStart);
            var nextOpenMatch = ForTagRegex().Match(content, bodyStart);

            if (!closeMatch.Success)
            {
                throw new InvalidOperationException(
                    $"Templating error: missing '{{% endfor %}}' for the loop starting with '{openMatch.Value}'.");
            }

            if (nextOpenMatch.Success && nextOpenMatch.Index < closeMatch.Index)
            {
                throw new InvalidOperationException(
                    "Templating error: nested '{% for %}' loops are not supported.");
            }

            var loopVariableName = openMatch.Groups[2].Value;
            var sourceExpressionGroup = openMatch.Groups[3];
            var sourceExpression = sourceExpressionGroup.Value.Trim();
            var body = content[bodyStart..closeMatch.Index];
            var length = closeMatch.Index + closeMatch.Length - openMatch.Index;

            blocks.Add(new LoopBlock(
                loopVariableName, sourceExpression, body, openMatch.Index, length,
                sourceExpressionGroup.Index, sourceExpressionGroup.Length));

            position = closeMatch.Index + closeMatch.Length;
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
