using System.Text.RegularExpressions;

namespace TeaPie.Templating;

internal static partial class LiteralBlocks
{
    public static MatchCollection Find(string content) => LiteralBlockRegex().Matches(content);

    [GeneratedRegex(
        @"\{%-?\s*(raw|comment)\s*-?%\}.*?\{%-?\s*end\1\s*-?%\}",
        RegexOptions.Singleline, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex LiteralBlockRegex();
}
