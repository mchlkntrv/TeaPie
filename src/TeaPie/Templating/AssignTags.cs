using System.Text.RegularExpressions;

namespace TeaPie.Templating;

internal static partial class AssignTags
{
    internal readonly record struct Occurrence(string Name, int Position);

    public static IReadOnlySet<string> FindNames(string content)
        => FindOccurrences(content)
            .Select(occurrence => occurrence.Name)
            .ToHashSet(StringComparer.Ordinal);

    public static List<Occurrence> FindOccurrences(string content)
        => FindOccurrences(content, LiteralBlocks.Find(content));

    public static List<Occurrence> FindOccurrences(string content, MatchCollection literalRanges)
    {
        var occurrences = new List<Occurrence>();

        foreach (Match match in AssignTargetRegex().Matches(content))
        {
            if (!TextRanges.Contains(literalRanges, match.Index))
            {
                occurrences.Add(new Occurrence(match.Groups[1].Value, match.Index));
            }
        }

        return occurrences;
    }

    [GeneratedRegex(
        @"\{%-?\s*assign\s+(" + FluidExpressionIdentifier.Pattern + @")\s*=",
        RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex AssignTargetRegex();
}
