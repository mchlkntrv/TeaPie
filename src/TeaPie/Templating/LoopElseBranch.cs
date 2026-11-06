using System.Text.RegularExpressions;

namespace TeaPie.Templating;

internal static partial class LoopElseBranch
{
    // Returns the text of the loop's own '{% else %}' branch, which is rendered when its collection is empty, or null.
    // An '{% else %}' of a nested '{% if %}', '{% unless %}', '{% case %}' or '{% for %}', or one inside
    // '{% raw %}'/'{% comment %}', does not belong to the loop.
    public static string? Find(string loopBody)
    {
        var literalRanges = LoopBlockScanner.LiteralBlockRegex().Matches(loopBody);
        var depth = 0;

        foreach (Match tag in TagRegex().Matches(loopBody))
        {
            if (literalRanges.Any(range => tag.Index >= range.Index && tag.Index < range.Index + range.Length))
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
