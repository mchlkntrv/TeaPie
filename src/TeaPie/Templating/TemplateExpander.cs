using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Logging;
using TeaPie.Variables;

namespace TeaPie.Templating;

internal sealed class TemplateExpander(
    ILoopBlockScanner scanner,
    ILoopBodyMasker masker,
    ICollectionSourceResolver sourceResolver,
    IVariablesFluidModelBuilder modelBuilder,
    IVariables variables,
    TemplatingLimits limits,
    ILogger<TemplateExpander> logger) : ITemplateExpander
{
    private const string SourceAliasPrefix = "__teapie_loop_source_";
    private const string LoopIndexPrefix = "__teapie_loop_index_";
    private const string LoopLengthPrefix = "__teapie_loop_length_";
    private const string LoopOffsetPrefix = "__teapie_loop_offset_";
    private const string TemplatingErrorPrefix = "Templating error";
    private const string GuardTargetName = "__teapie_loop_guard";
    private const string GuardFilterName = "__teapie_guard_loop_source";

    private static readonly FluidParser Parser = new();

    public TemplateExpansionResult Expand(string content, string filePath)
    {
        try
        {
            return ExpandTemplate(content, filePath);
        }
        catch (RegexMatchTimeoutException ex)
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': scanning the file took longer than " +
                $"{TemplatingLimits.RegexTimeoutMilliseconds} ms. Check it for an unclosed '{{{{', '{{%' or quote.", ex);
        }
    }

    private TemplateExpansionResult ExpandTemplate(string content, string filePath)
    {
        if (!TemplateDetector.ContainsTemplateTag(content))
        {
            WarnAboutUnsupportedTagDelimiter(content, filePath);
            return new TemplateExpansionResult(content, TemplatedRequestNames.None);
        }

        var blocks = FindLoopBlocks(scanner, content, filePath);
        ValidateNestingDepth(blocks, filePath);
        var forSources = ParseForTagSources(blocks, filePath);
        var isDynamic = Enumerable.Range(0, blocks.Count).Select(i => IsDynamicSource(i, blocks, forSources)).ToArray();

        var model = modelBuilder.Build(variables);
        var edits = new List<TextEdit>();
        var sources = ResolveSourcesAndCollectSourceEdits(blocks, forSources, isDynamic, model, filePath, edits);
        AddDynamicSourceGuardEdits(edits, content, blocks, forSources, isDynamic);
        edits.AddRange(masker.FindMaskEdits(content, blocks));

        var transformed = TextEditApplier.Apply(content, edits, out var positionMap);
        var topLevelIndices = FindTopLevelIndices(blocks);
        var requestCounts = new int?[blocks.Count];
        var loopOutputs = new List<string>();
        var dataValues = new TemplateDataValues();
        var template = WrapLoops(
            RenderedValueGuard.Apply(ParseTemplate(transformed, content, positionMap, filePath), dataValues), blocks,
            forSources, topLevelIndices, requestCounts, loopOutputs, filePath);

        var usage = new DynamicSourceUsage[blocks.Count];
        var context = BuildRenderContext(blocks, forSources, sources, model, usage, filePath);
        var rendered = RenderTemplate(template, context, filePath);
        var warnedIndices = WarnAboutDynamicSourcesEmptyForEveryOuterItem(forSources, usage, filePath);

        WarnAboutTopLevelLoopsWithoutRequests(
            requestCounts, blocks, forSources, topLevelIndices, warnedIndices, filePath);

        return new TemplateExpansionResult(rendered, FindTemplatedRequestNames(content, rendered, blocks, loopOutputs))
        {
            DataValues = dataValues.ValuesByName
        };
    }

    private static TemplatedRequestNames FindTemplatedRequestNames(
        string content, string rendered, IReadOnlyList<LoopBlock> blocks, IReadOnlyList<string> loopOutputs)
    {
        var loopCounts = RequestNameDeclarations.Count(string.Join('\n', loopOutputs));
        var innermostLoopsOfLiteralDeclarations = RequestNameDeclarations.Find(content)
            .Where(declaration => declaration.IsLiteral)
            .ToLookup(
                declaration => declaration.Name,
                declaration => LoopBlockHierarchy.GetEnclosingBlockIndicesInnermostFirst(declaration.Index, blocks)
                    .DefaultIfEmpty(-1)
                    .First(),
                StringComparer.Ordinal);

        var repeatable = RequestNameDeclarations.Count(rendered)
            .Where(entry => entry.Value > 1
                && loopCounts.GetValueOrDefault(entry.Key) >= entry.Value
                && IsWrittenAtMostOncePerLoopAndOnlyInLoops(innermostLoopsOfLiteralDeclarations[entry.Key]))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);

        return new TemplatedRequestNames(repeatable, loopCounts.Keys.ToHashSet(StringComparer.Ordinal));
    }

    private static bool IsWrittenAtMostOncePerLoopAndOnlyInLoops(IEnumerable<int> innermostLoopIndices)
    {
        var indices = innermostLoopIndices.ToList();
        return indices.Count > 0 && !indices.Contains(-1) && indices.Distinct().Count() == indices.Count;
    }

    private void WarnAboutUnsupportedTagDelimiter(string content, string filePath)
    {
        if (TemplateDetector.ContainsTagDelimiter(content))
        {
            logger.LogWarning(
                "Requests file at '{Path}' contains '{{%' but no supported template tag ({Tags}), so it is sent " +
                "unchanged. If it was meant to be a template, check the spelling of its tags.",
                filePath,
                TemplateDetector.SupportedTagNames);
        }
    }

    private void ValidateNestingDepth(IReadOnlyList<LoopBlock> blocks, string filePath)
    {
        var depths = blocks.Select((_, i) => LoopBlockHierarchy.GetAncestorIndices(i, blocks).Count + 1).ToList();
        var deepestIndex = depths.IndexOf(depths.DefaultIfEmpty(0).Max());

        if (deepestIndex >= 0 && depths[deepestIndex] > limits.MaxNestingDepth)
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': loops are nested {depths[deepestIndex]} levels deep, exceeding " +
                $"the maximum of {limits.MaxNestingDepth} (innermost loop over " +
                $"'{blocks[deepestIndex].SourceExpression}').");
        }
    }

    private ForTagSource[] ParseForTagSources(IReadOnlyList<LoopBlock> blocks, string filePath)
    {
        try
        {
            return blocks
                .Select(block => ForTagSourceParser.Parse(block.SourceExpression, limits.MaxLoopItems))
                .ToArray();
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': {StripTemplatingErrorPrefix(ex.Message)}", ex);
        }
    }

    private LoopSource?[] ResolveSourcesAndCollectSourceEdits(
        IReadOnlyList<LoopBlock> blocks, IReadOnlyList<ForTagSource> forSources, bool[] isDynamic,
        IReadOnlyDictionary<string, object?> model, string filePath, List<TextEdit> edits)
    {
        var sources = new LoopSource?[blocks.Count];

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var forSource = forSources[i];

            if (isDynamic[i])
            {
                AddSourceEdit(edits, block, forSource.ToFluidSource(forSource.CollectionExpression));
                continue;
            }

            var source = ResolveSource(sourceResolver, forSource.CollectionExpression, model, filePath);
            sources[i] = source;

            if (source.ItemCount == 0)
            {
                throw new InvalidOperationException(
                    $"Templating error in '{filePath}': loop over '{forSource.CollectionExpression}' produced " +
                    "zero items.");
            }

            if (source.ItemCount > limits.MaxLoopItems)
            {
                throw new InvalidOperationException(
                    $"Templating error in '{filePath}': loop over '{forSource.CollectionExpression}' has " +
                    $"{source.ItemCount} items, exceeding the maximum of {limits.MaxLoopItems} items per loop.");
            }

            var collection = source.Collection is not null ? $"{SourceAliasPrefix}{i}" : forSource.CollectionExpression;
            AddSourceEdit(edits, block, forSource.ToFluidSource(collection));
        }

        return sources;
    }

    private static void AddSourceEdit(List<TextEdit> edits, LoopBlock block, string fluidSource)
    {
        if (!string.Equals(fluidSource, block.SourceExpression, StringComparison.Ordinal))
        {
            edits.Add(new TextEdit(block.SourceExpressionStartIndex, block.SourceExpressionRawLength, fluidSource));
        }
    }

    private static void AddDynamicSourceGuardEdits(
        List<TextEdit> edits, string content, IReadOnlyList<LoopBlock> blocks, IReadOnlyList<ForTagSource> forSources,
        bool[] isDynamic)
    {
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

    private static List<int> FindTopLevelIndices(IReadOnlyList<LoopBlock> blocks)
        => Enumerable.Range(0, blocks.Count)
            .Where(i => LoopBlockHierarchy.IsTopLevel(i, blocks))
            .OrderBy(i => blocks[i].StartIndex)
            .ToList();

    private IFluidTemplate WrapLoops(
        IFluidTemplate template, IReadOnlyList<LoopBlock> blocks, IReadOnlyList<ForTagSource> forSources,
        IReadOnlyList<int> topLevelIndices, int?[] requestCounts, List<string> loopOutputs, string filePath)
    {
        var renderedLoopsLength = 0L;

        try
        {
            return LoopWrapper.Wrap(
                template,
                topLevelIndices.Select(i => blocks[i].LoopVariableName).ToList(),
                limits,
                (loopIndex, requestCount, length) =>
                {
                    var source = forSources[topLevelIndices[loopIndex]].CollectionExpression;
                    if (requestCount > limits.MaxExpandedRequests)
                    {
                        throw new InvalidOperationException(
                            $"Templating error in '{filePath}': loop over '{source}' would expand to more than " +
                            $"{limits.MaxExpandedRequests} requests, counted across all its nesting levels.");
                    }

                    if (renderedLoopsLength + length > limits.MaxExpandedLength)
                    {
                        throw new InvalidOperationException(
                            $"Templating error in '{filePath}': the loops in the file would expand to more than " +
                            $"{limits.MaxExpandedLength} characters (exceeded in the loop over '{source}'). Check " +
                            "the loops for large bodies, or split the file.");
                    }
                },
                (loopIndex, output, requestCount) =>
                {
                    requestCounts[topLevelIndices[loopIndex]] = requestCount;
                    renderedLoopsLength += output.Length;
                    loopOutputs.Add(output);
                });
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': {StripTemplatingErrorPrefix(ex.Message)}", ex);
        }
    }

    private static IFluidTemplate ParseTemplate(
        string transformed, string originalContent, IReadOnlyList<TransformedTextSpan> positionMap, string filePath)
    {
        if (!Parser.TryParse(transformed, out var template, out var parseError))
        {
            var originalError =
                FluidParseErrorMapper.RemapToOriginal(parseError, originalContent, transformed, positionMap);
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': failed to parse template: {originalError}\n" +
                "If this file contains literal '{%' text that is not a TeaPie template tag, wrap it in " +
                "'{% raw %}...{% endraw %}'.");
        }

        return template;
    }

    private TemplateContext BuildRenderContext(
        IReadOnlyList<LoopBlock> blocks, IReadOnlyList<ForTagSource> forSources, LoopSource?[] sources,
        IReadOnlyDictionary<string, object?> variablesModel, DynamicSourceUsage[] usage, string filePath)
    {
        var options = CreateMemberAccessOptions(filePath);
        options.MaxSteps = limits.MaxRenderSteps;
        ValueSizeGuard.WrapFilters(options.Filters, limits);
        options.Filters.AddFilter(GuardFilterName, (input, arguments, context) =>
            GuardDynamicSource(
                input, arguments, context, blocks, forSources, usage, limits.MaxLoopItems, filePath, logger));

        var model = new Dictionary<string, object?>(variablesModel);
        for (var i = 0; i < blocks.Count; i++)
        {
            if (sources[i] is { Collection: not null } source)
            {
                model[$"{SourceAliasPrefix}{i}"] = source.Collection;
            }
        }

        return new TemplateContext(model, options);
    }

    private static ValueTask<FluidValue> GuardDynamicSource(
        FluidValue input, FilterArguments arguments, TemplateContext context, IReadOnlyList<LoopBlock> blocks,
        IReadOnlyList<ForTagSource> forSources, DynamicSourceUsage[] usage, int maxItems, string filePath,
        ILogger logger)
    {
        var blockIndex = (int)arguments.At(0).ToNumberValue();
        var forSource = forSources[blockIndex];
        var raw = input.ToObjectValue();

        if (raw is not IEnumerable enumerable || raw is string)
        {
            var outerItemPath = FormatOuterItemPath(blockIndex, blocks, forSources, context);
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': variable '{forSource.CollectionExpression}' referenced in a " +
                $"'{{% for %}}' loop must be a collection ({outerItemPath}).");
        }

        var itemCount = enumerable.Cast<object?>().Take(maxItems + 1).Count();
        if (itemCount > maxItems)
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': loop over '{forSource.CollectionExpression}' has more than " +
                $"{maxItems} items ({FormatOuterItemPath(blockIndex, blocks, forSources, context)}), exceeding the " +
                $"maximum of {maxItems} items per loop.");
        }

        usage[blockIndex].Evaluated++;

        if (itemCount > 0)
        {
            usage[blockIndex].NonEmpty++;
            return new ValueTask<FluidValue>(NilValue.Instance);
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Loop over '{Source}' in '{Path}' produced zero items for {OuterItem}, so it generated no requests " +
                "there.",
                forSource.CollectionExpression,
                filePath,
                FormatOuterItemPath(blockIndex, blocks, forSources, context));
        }

        return new ValueTask<FluidValue>(NilValue.Instance);
    }

    private HashSet<int> WarnAboutDynamicSourcesEmptyForEveryOuterItem(
        IReadOnlyList<ForTagSource> forSources, DynamicSourceUsage[] usage, string filePath)
    {
        var warnedIndices = new HashSet<int>();

        for (var i = 0; i < usage.Length; i++)
        {
            if (usage[i].Evaluated > 0 && usage[i].NonEmpty == 0)
            {
                warnedIndices.Add(i);
                logger.LogWarning(
                    "Loop over '{Source}' in '{Path}' produced zero items for every one of its {Count} outer " +
                    "item(s), so it generated no requests at all. Check that the collection is filled as expected.",
                    forSources[i].CollectionExpression,
                    filePath,
                    usage[i].Evaluated);
            }
        }

        return warnedIndices;
    }

    private struct DynamicSourceUsage
    {
        public int Evaluated;
        public int NonEmpty;
    }

    private static string FormatOuterItemPath(
        int blockIndex, IReadOnlyList<LoopBlock> blocks, IReadOnlyList<ForTagSource> forSources,
        TemplateContext context)
    {
        var ancestors = LoopBlockHierarchy.GetAncestorIndices(blockIndex, blocks);
        ancestors.Reverse();

        return string.Join('.', ancestors.Select(ancestorIndex =>
        {
            var position = ResolveSourcePosition(ancestorIndex, forSources[ancestorIndex], context);
            return $"{blocks[ancestorIndex].LoopVariableName}[{position.ToString(CultureInfo.InvariantCulture)}]";
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

    private string RenderTemplate(IFluidTemplate template, TemplateContext context, string filePath)
    {
        try
        {
            return template.Render(context);
        }
        catch (InvalidOperationException ex) when (IsRenderStepLimitExceeded(context))
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': template exceeded the maximum of {limits.MaxRenderSteps} " +
                "rendering steps across the whole file - for example too many '{{ }}' expressions per item, or " +
                "nested loops that together run too many times. Check the file for repeated expressions, or " +
                "split it into smaller loops.", ex);
        }
        catch (InvalidOperationException ex)
            when (ex.Message.StartsWith(TemplatingErrorPrefix + ": ", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': {StripTemplatingErrorPrefix(ex.Message)}", ex);
        }
        catch (Exception ex) when (IsUnreportedRenderError(ex))
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': rendering failed: {ex.Message}", ex);
        }
    }

    private static bool IsUnreportedRenderError(Exception ex)
        => ex is not (OutOfMemoryException or OperationCanceledException)
            && !ex.Message.StartsWith(TemplatingErrorPrefix, StringComparison.Ordinal);

    private void WarnAboutTopLevelLoopsWithoutRequests(
        int?[] requestCounts, IReadOnlyList<LoopBlock> blocks, IReadOnlyList<ForTagSource> forSources,
        IReadOnlyList<int> topLevelIndices, IReadOnlySet<int> warnedIndices, string filePath)
    {
        foreach (var rootIndex in topLevelIndices)
        {
            if (requestCounts[rootIndex] == 0 && DeclaresRequests(blocks[rootIndex])
                && !warnedIndices.Any(index => LoopBlockHierarchy.GetAncestorIndices(index, blocks).Contains(rootIndex)))
            {
                logger.LogWarning(
                    "Loop over '{Source}' in '{Path}' generated no requests, although its body declares some " +
                    "(for example because 'limit:'/'offset:' or an '{{% if %}}' excluded every item). Check the " +
                    "loop's collection, options and conditions.",
                    forSources[rootIndex].CollectionExpression,
                    filePath);
            }
        }
    }

    private static bool DeclaresRequests(LoopBlock block) => RequestCountingWriter.ContainsRequestLine(block.Body);

    private static IReadOnlyList<LoopBlock> FindLoopBlocks(ILoopBlockScanner scanner, string content, string filePath)
    {
        try
        {
            return scanner.FindLoopBlocks(content);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': {StripTemplatingErrorPrefix(ex.Message)}", ex);
        }
    }

    private static TemplateOptions CreateMemberAccessOptions(string filePath)
        => new()
        {
            MemberAccessStrategy = new UnsafeMemberAccessStrategy(),
            Undefined = name => throw new InvalidOperationException(
                $"Templating error in '{filePath}': '{name}' is undefined.")
        };

    private static LoopSource ResolveSource(
        ICollectionSourceResolver sourceResolver, string sourceExpression, IReadOnlyDictionary<string, object?> model,
        string filePath)
    {
        try
        {
            return sourceResolver.Resolve(sourceExpression, model);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': {StripTemplatingErrorPrefix(ex.Message)}", ex);
        }
    }

    private static string StripTemplatingErrorPrefix(string message)
    {
        const string prefix = TemplatingErrorPrefix + ": ";
        return message.StartsWith(prefix, StringComparison.Ordinal) ? message[prefix.Length..] : message;
    }

    // Fluid reports the step limit with the same message as its recursion limit, so the step counter decides:
    // once the limit was exceeded, every further step is rejected too.
    private static bool IsRenderStepLimitExceeded(TemplateContext context)
    {
        try
        {
            context.IncrementSteps();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool IsDynamicSource(
        int blockIndex, IReadOnlyList<LoopBlock> blocks, IReadOnlyList<ForTagSource> forSources)
        => LoopBlockHierarchy.GetAncestorIndices(blockIndex, blocks).Exists(ancestorIndex =>
            FluidExpressionIdentifier.StartsWithIdentifier(
                forSources[blockIndex].CollectionExpression, blocks[ancestorIndex].LoopVariableName));
}
