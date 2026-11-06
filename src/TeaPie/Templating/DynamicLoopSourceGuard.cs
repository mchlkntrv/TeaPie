using System.Collections;
using System.Globalization;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Logging;
using static TeaPie.Templating.TemplatingMessageText;

namespace TeaPie.Templating;

internal sealed class DynamicLoopSourceGuard(TemplateExpansionInput input, TemplatingLimits limits, ILogger logger)
{
    private const string LoopIndexPrefix = "__teapie_loop_index_";
    private const string LoopLengthPrefix = "__teapie_loop_length_";
    private const string LoopOffsetPrefix = "__teapie_loop_offset_";
    private const string GuardTargetName = "__teapie_loop_guard";
    private const string GuardFilterName = "__teapie_guard_loop_source";

    private readonly Usage[] _usage = new Usage[input.Blocks.Count];

    public void AddGuardEdits(List<TextEdit> edits)
    {
        var (content, _, blocks, forSources, isDynamic) = input;
        var ancestorsByBlock = Enumerable.Range(0, blocks.Count)
            .Select(i => LoopBlockHierarchy.GetAncestorIndices(i, blocks))
            .ToArray();

        for (var i = 0; i < blocks.Count; i++)
        {
            var leadsToDynamicSource = isDynamic[i]
                || Enumerable.Range(0, blocks.Count).Any(d => isDynamic[d] && ancestorsByBlock[d].Contains(i));

            if (ancestorsByBlock[i].Count == 0 || !leadsToDynamicSource)
            {
                continue;
            }

            var block = blocks[i];
            var parentIndex = ancestorsByBlock[i][0];
            var parent = forSources[parentIndex];
            var trimMarker = content[block.StartIndex + 2] == '-' ? "-" : string.Empty;
            var injected = $"{{%{trimMarker} assign {LoopIndexPrefix}{parentIndex} = forloop.index0 %}}";

            if (parent.IsReversed)
            {
                injected += $"{{% assign {LoopLengthPrefix}{parentIndex} = forloop.length %}}";
            }

            if (parent.IsReversed && parent.OffsetExpression is not null)
            {
                injected += $"{{% assign {LoopOffsetPrefix}{parentIndex} = {parent.OffsetExpression} %}}";
            }

            if (isDynamic[i])
            {
                injected += $"{{% assign {GuardTargetName} = {forSources[i].CollectionExpression} | " +
                    $"{GuardFilterName}: {i} %}}";
            }

            edits.Add(new TextEdit(block.StartIndex, 0, injected));
        }
    }

    public void RegisterFilter(TemplateOptions options)
        => options.Filters.AddFilter(GuardFilterName, Guard);

    public IReadOnlySet<int> WarnAboutSourcesEmptyForEveryOuterItem()
    {
        var warnedIndices = new HashSet<int>();

        for (var i = 0; i < _usage.Length; i++)
        {
            if (_usage[i].Evaluated > 0 && _usage[i].NonEmpty == 0 && !ElseBranchDeclaresRequests(input.Blocks[i]))
            {
                warnedIndices.Add(i);
                logger.LogWarning(
                    "Loop over '{Source}' in '{Path}' produced zero items for every one of its {Count} outer " +
                    "item(s), so it generated no requests at all. Check that the collection is filled as expected.",
                    Shorten(input.ForSources[i].CollectionExpression),
                    input.FilePath,
                    _usage[i].Evaluated);
            }
        }

        return warnedIndices;
    }

    private ValueTask<FluidValue> Guard(FluidValue value, FilterArguments arguments, TemplateContext context)
    {
        var blockIndex = (int)arguments.At(0).ToNumberValue();
        var forSource = input.ForSources[blockIndex];
        var filePath = input.FilePath;
        var maxItems = limits.MaxLoopItems;
        var raw = value.ToObjectValue();

        if (raw is not IEnumerable enumerable || raw is string)
        {
            var outerItemPath = FormatOuterItemPath(blockIndex, context);
            var actual = raw is null ? ", but it is missing or null" : string.Empty;
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': variable '{Shorten(forSource.CollectionExpression)}' referenced in a " +
                $"'{{% for %}}' loop must be a collection{actual} ({outerItemPath}).");
        }

        var itemCount = enumerable.Cast<object?>().Take(maxItems + 1).Count();
        if (itemCount > maxItems)
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': loop over '{Shorten(forSource.CollectionExpression)}' has more than " +
                $"{maxItems} items ({FormatOuterItemPath(blockIndex, context)}), exceeding the " +
                $"maximum of {maxItems} items per loop.");
        }

        _usage[blockIndex].Evaluated++;

        if (itemCount > 0)
        {
            _usage[blockIndex].NonEmpty++;
            return new ValueTask<FluidValue>(NilValue.Instance);
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Loop over '{Source}' in '{Path}' produced zero items for {OuterItem}, so it generated no requests " +
                "there.",
                Shorten(forSource.CollectionExpression),
                filePath,
                FormatOuterItemPath(blockIndex, context));
        }

        return new ValueTask<FluidValue>(NilValue.Instance);
    }

    private string FormatOuterItemPath(int blockIndex, TemplateContext context)
    {
        var ancestors = LoopBlockHierarchy.GetAncestorIndices(blockIndex, input.Blocks);
        ancestors.Reverse();

        return string.Join('.', ancestors.Select(ancestorIndex =>
        {
            var position = ResolveSourcePosition(ancestorIndex, input.ForSources[ancestorIndex], context);
            var name = Shorten(input.Blocks[ancestorIndex].LoopVariableName);
            return $"{name}[{position.ToString(CultureInfo.InvariantCulture)}]";
        }));
    }

    private static decimal ResolveSourcePosition(int blockIndex, ForTagSource forSource, TemplateContext context)
    {
        var index = context.GetValue($"{LoopIndexPrefix}{blockIndex}").ToNumberValue();

        if (!forSource.IsReversed)
        {
            return index;
        }

        var length = context.GetValue($"{LoopLengthPrefix}{blockIndex}").ToNumberValue();
        var offset = forSource.OffsetExpression is not null
            ? context.GetValue($"{LoopOffsetPrefix}{blockIndex}").ToNumberValue()
            : 0;

        return length - 1 - index + offset;
    }

    private static bool ElseBranchDeclaresRequests(LoopBlock block)
        => LoopElseBranch.Find(block.Body) is { } elseBranch && RequestCountingWriter.ContainsRequestLine(elseBranch);

    private struct Usage
    {
        public int Evaluated;
        public int NonEmpty;
    }
}
