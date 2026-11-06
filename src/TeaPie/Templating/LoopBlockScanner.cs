using System.Text.RegularExpressions;
using static TeaPie.Templating.TemplatingMessageText;

namespace TeaPie.Templating;

internal sealed partial class LoopBlockScanner : ILoopBlockScanner
{
    public IReadOnlyList<LoopBlock> FindLoopBlocks(string content)
    {
        var literalRanges = LiteralBlockRegex().Matches(content);
        EnsureEveryRawTagIsClosed(content, literalRanges);
        EnsureNoLiquidTag(content, literalRanges);
        var forTags = ForTagRegex().Matches(content);
        EnsureEveryForTagIsWellFormed(content, forTags, literalRanges);

        var tags = forTags.Cast<Match>().Select(m => (Match: m, IsFor: true))
            .Concat(EndForTagRegex().Matches(content).Cast<Match>().Select(m => (Match: m, IsFor: false)))
            .Where(t => !IsWithinAnyRange(t.Match.Index, literalRanges))
            .OrderBy(t => t.Match.Index)
            .ToList();

        var stack = new Stack<Match>();
        var blocks = new List<LoopBlock>();

        foreach (var (match, isFor) in tags)
        {
            if (isFor)
            {
                stack.Push(match);
                continue;
            }

            if (stack.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Templating error: found '{Shorten(match.Value)}' without a matching '{{% for %}}'.");
            }

            var openMatch = stack.Pop();
            var bodyStart = openMatch.Index + openMatch.Length;
            if (match.Index < bodyStart)
            {
                throw StrayOrMalformedTag(openMatch.Value);
            }

            var loopVariableName = openMatch.Groups[2].Value;
            var sourceExpressionGroup = openMatch.Groups[3];
            var sourceExpression = sourceExpressionGroup.Value.Trim();
            var body = content[bodyStart..match.Index];
            var length = match.Index + match.Length - openMatch.Index;

            blocks.Add(new LoopBlock(
                loopVariableName, sourceExpression, body, openMatch.Index, length,
                sourceExpressionGroup.Index, sourceExpressionGroup.Length));
        }

        if (stack.Count > 0)
        {
            var unclosed = stack.Peek();
            throw new InvalidOperationException(
                $"Templating error: missing '{{% endfor %}}' for the loop starting with '{Shorten(unclosed.Value)}'.");
        }

        EnsureNoResidualForTags(content, blocks, literalRanges);

        return blocks;
    }

    private static void EnsureEveryRawTagIsClosed(string content, MatchCollection literalRanges)
    {
        foreach (Match opener in RawTagRegex().Matches(content))
        {
            if (!IsWithinAnyRange(opener.Index, literalRanges))
            {
                var (line, column) = FluidParseErrorMapper.ToLineColumn(content, opener.Index);
                throw new InvalidOperationException(
                    $"Templating error: missing '{{% endraw %}}' for the '{{% raw %}}' at ({line}:{column}).");
            }
        }
    }

    private static void EnsureNoLiquidTag(string content, MatchCollection literalRanges)
    {
        foreach (Match tag in LiquidTagRegex().Matches(content))
        {
            if (!IsWithinAnyRange(tag.Index, literalRanges))
            {
                var (line, column) = FluidParseErrorMapper.ToLineColumn(content, tag.Index);
                throw new InvalidOperationException(
                    $"Templating error: the '{{% liquid %}}' tag at ({line}:{column}) is not supported. Write each " +
                    "statement as a separate tag instead, for example '{% assign a = 1 %}'.");
            }
        }
    }

    // A '{% for' that the loop tag regex does not recognize would otherwise leave its '{% endfor %}' unmatched and
    // be reported as a stray '{% endfor %}', far from the actual mistake.
    private static void EnsureEveryForTagIsWellFormed(string content, MatchCollection forTags, MatchCollection literalRanges)
    {
        foreach (Match opener in ForOpenerRegex().Matches(content))
        {
            if (!IsWithinAnyRange(opener.Index, literalRanges) && !IsWithinAnyRange(opener.Index, forTags))
            {
                throw MalformedForTag(content, opener.Index);
            }
        }
    }

    private static InvalidOperationException MalformedForTag(string content, int index)
    {
        var lineEnd = content.IndexOf('\n', index);
        lineEnd = lineEnd < 0 ? content.Length : lineEnd;
        var closing = content.IndexOf("%}", index, lineEnd - index, StringComparison.Ordinal);
        var tag = content[index..(closing < 0 ? lineEnd : closing + 2)].TrimEnd();
        var (line, column) = FluidParseErrorMapper.ToLineColumn(content, index);

        return new($"Templating error: invalid '{{% for %}}' tag '{Shorten(tag)}' at ({line}:{column}). Write it on one " +
            "line as '{% for <item> in <collection> %}', for example '{% for car in Cars %}'.");
    }

    private static void EnsureNoResidualForTags(string content, List<LoopBlock> blocks, MatchCollection literalRanges)
    {
        foreach (Match match in ForTagFamilyRegex().Matches(content))
        {
            if (!IsWithinAnyBlock(match.Index, blocks) && !IsWithinAnyRange(match.Index, literalRanges))
            {
                throw StrayOrMalformedTag(match.Value);
            }
        }
    }

    private static InvalidOperationException StrayOrMalformedTag(string tag)
        => new($"Templating error: found a stray or malformed loop tag '{Shorten(tag)}' that is not part of a valid " +
            "'{% for %}' ... '{% endfor %}' block.");

    private static bool IsWithinAnyBlock(int index, List<LoopBlock> blocks)
        => blocks.Exists(block => index >= block.StartIndex && index < block.StartIndex + block.Length);

    private static bool IsWithinAnyRange(int index, MatchCollection ranges)
        => ranges.Any(range => index >= range.Index && index < range.Index + range.Length);

    [GeneratedRegex(
        @"\{%(-)?\s*for\s+(" + FluidExpressionIdentifier.Pattern + @")\s+in\s+((?:""[^""]*""|'[^']*'|[^""'\n])+?)\s*(-)?%\}",
        RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex ForTagRegex();

    // 'for-x' is an unknown tag for Fluid, not a loop, so a hyphen ends the match like a word character does.
    [GeneratedRegex(@"\{%-?\s*for(?![\w-])", RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex ForOpenerRegex();

    [GeneratedRegex(@"\{%(-)?\s*endfor\s*(-)?%\}", RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex EndForTagRegex();

    [GeneratedRegex(@"\{%-?\s*(for|endfor)\b[^%]*-?%\}", RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex ForTagFamilyRegex();

    [GeneratedRegex(@"\{%-?\s*raw\s*-?%\}", RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex RawTagRegex();

    [GeneratedRegex(@"\{%-?\s*liquid(?![\w-])", RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex LiquidTagRegex();

    [GeneratedRegex(
        @"\{%-?\s*(raw|comment)\s*-?%\}.*?\{%-?\s*end\1\s*-?%\}",
        RegexOptions.Singleline, TemplatingLimits.RegexTimeoutMilliseconds)]
    internal static partial Regex LiteralBlockRegex();
}
