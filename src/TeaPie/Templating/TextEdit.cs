using System.Text;

namespace TeaPie.Templating;

internal readonly record struct TextEdit(int Start, int Length, string Replacement);

internal static class TextEditApplier
{
    public static string Apply(string content, IEnumerable<TextEdit> edits)
    {
        var ordered = edits.OrderBy(edit => edit.Start).ToList();
        var result = new StringBuilder();
        var cursor = 0;

        foreach (var edit in ordered)
        {
            result.Append(content, cursor, edit.Start - cursor);
            result.Append(edit.Replacement);
            cursor = edit.Start + edit.Length;
        }

        result.Append(content, cursor, content.Length - cursor);
        return result.ToString();
    }
}
