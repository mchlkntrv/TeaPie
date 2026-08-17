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

            var loopVariableName = openMatch.Groups[1].Value;
            var sourceExpression = openMatch.Groups[2].Value.Trim();
            var body = content[bodyStart..closeMatch.Index];
            var length = closeMatch.Index + closeMatch.Length - openMatch.Index;

            blocks.Add(new LoopBlock(loopVariableName, sourceExpression, body, openMatch.Index, length));

            position = closeMatch.Index + closeMatch.Length;
        }

        return blocks;
    }

    [GeneratedRegex(@"\{%\s*for\s+([A-Za-z_][A-Za-z0-9_]*)\s+in\s+(.+?)\s*%\}")]
    private static partial Regex ForTagRegex();

    [GeneratedRegex(@"\{%\s*endfor\s*%\}")]
    private static partial Regex EndForTagRegex();
}
