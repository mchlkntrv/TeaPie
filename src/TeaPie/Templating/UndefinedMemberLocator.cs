using System.Text.RegularExpressions;
using static TeaPie.Templating.TemplatingMessageText;

namespace TeaPie.Templating;

internal static partial class UndefinedMemberLocator
{
    // Fluid reports only the name of the missing member, so the first expression in the original file that reads
    // a member of that name is shown with it.
    public static string Describe(string memberName, string content)
    {
        var message = $"'{Shorten(memberName)}' is undefined: the value has no property or field '{Shorten(memberName)}' " +
            "(names are case-sensitive).";

        var literalRanges = LoopBlockScanner.LiteralBlockRegex().Matches(content);
        var memberAccess = new Regex(
            $@"\.\s*{Regex.Escape(memberName)}(?![A-Za-z0-9_-])|\[\s*([""']){Regex.Escape(memberName)}\1\s*\]",
            RegexOptions.None,
            TimeSpan.FromMilliseconds(TemplatingLimits.RegexTimeoutMilliseconds));

        foreach (Match expression in ExpressionRegex().Matches(content))
        {
            if (!literalRanges.Any(range => expression.Index >= range.Index && expression.Index < range.Index + range.Length)
                && memberAccess.IsMatch(expression.Value))
            {
                var (line, column) = FluidParseErrorMapper.ToLineColumn(content, expression.Index);
                return $"{message} First use at ({line}:{column}): {Shorten(expression.Value)}";
            }
        }

        return message;
    }

    [GeneratedRegex(@"\{\{[^\n]*?\}\}|\{%[^\n]*?%\}", RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex ExpressionRegex();
}
