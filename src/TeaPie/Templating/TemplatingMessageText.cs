using System.Text;

namespace TeaPie.Templating;

// Templating errors and warnings quote text from the user's file (tags, loop sources, names, source lines). That text
// can be huge, e.g. a long token after '{%' or a minified body on one line, so it is shortened before it is quoted to
// keep the messages readable and cheap to log, print and store.
internal static class TemplatingMessageText
{
    public const int MaxQuotedLength = 100;
    public const int MaxSourceLineLength = 200;
    public const int MaxMessageLength = 500;
    private const string Ellipsis = "...";

    public static string Shorten(string text, int maxLength = MaxQuotedLength)
        => text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, CutBefore(text, maxLength)), Ellipsis);

    // For messages written by Fluid, which quote the offending token in apostrophes.
    public static string ShortenQuoted(string message)
    {
        var builder = new StringBuilder();
        var index = 0;

        while (index < message.Length)
        {
            var open = message.IndexOf('\'', index);
            if (open < 0)
            {
                builder.Append(message, index, message.Length - index);
                break;
            }

            builder.Append(message, index, open + 1 - index);
            var close = message.IndexOf('\'', open + 1);
            var end = close < 0 ? message.Length : close;
            AppendShortened(builder, message, open + 1, end - open - 1);

            if (close < 0)
            {
                break;
            }

            builder.Append('\'');
            index = close + 1;
        }

        return Shorten(builder.ToString(), MaxMessageLength);
    }

    public static string ShortenLineAround(string line, int column)
    {
        if (line.Length <= MaxSourceLineLength)
        {
            return line;
        }

        var start = Math.Clamp(column - 1 - (MaxSourceLineLength / 2), 0, line.Length - MaxSourceLineLength);
        if (start > 0 && char.IsLowSurrogate(line[start]))
        {
            start++;
        }

        var end = CutBefore(line, start + MaxSourceLineLength);
        var prefix = start > 0 ? Ellipsis : string.Empty;
        var suffix = end < line.Length ? Ellipsis : string.Empty;
        return string.Concat(prefix, line.AsSpan(start, end - start), suffix);
    }

    private static void AppendShortened(StringBuilder builder, string text, int start, int length)
    {
        if (length <= MaxQuotedLength)
        {
            builder.Append(text, start, length);
            return;
        }

        builder.Append(Shorten(text.Substring(start, MaxQuotedLength + 1)));
    }

    // Keeps a surrogate pair together by cutting before its first half.
    private static int CutBefore(string text, int index)
        => index > 0 && index < text.Length && char.IsHighSurrogate(text[index - 1]) ? index - 1 : index;
}
