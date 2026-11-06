using System.Text.RegularExpressions;

namespace TeaPie.Templating;

internal static class TextRanges
{
    public static bool Contains(IReadOnlyList<Match> ranges, int index)
    {
        var low = 0;
        var high = ranges.Count - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var range = ranges[middle];

            if (index < range.Index)
            {
                high = middle - 1;
            }
            else if (index >= range.Index + range.Length)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
