using System.Text.RegularExpressions;

namespace TeaPie.Templating;

internal static partial class LoopElseBranch
{
    public static string? Find(string loopBody)
    {
        var literalRanges = LiteralBlocks.Find(loopBody);
        var depth = 0;

        foreach (Match tag in TagRegex().Matches(loopBody))
        {
            if (TextRanges.Contains(literalRanges, tag.Index))
            {
                continue;
            }

            switch (tag.Groups[1].Value)
            {
                case "for" or "if" or "unless" or "case":
                    depth++;
                    break;
                case "endfor" or "endif" or "endunless" or "endcase":
                    depth--;
                    break;
                case "else" when depth == 0:
                    return loopBody[(tag.Index + tag.Length)..];
            }
        }

        return null;
    }

    [GeneratedRegex(@"\{%-?\s*([\w-]+)[^%]*(?:%(?!\})[^%]*)*%\}", RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex TagRegex();
}
