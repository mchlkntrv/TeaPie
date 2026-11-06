using System.Text.RegularExpressions;
using Fluid;
using Microsoft.Extensions.Logging;
using TeaPie.Variables;
using static TeaPie.Templating.TemplatingMessageText;

namespace TeaPie.Templating;

internal sealed class TemplateExpander(
    ILoopBlockScanner scanner,
    INonFluidSyntaxMasker masker,
    ICollectionSourceResolver sourceResolver,
    IVariablesFluidModelBuilder modelBuilder,
    IVariables variables,
    TemplatingLimits limits,
    ILogger<TemplateExpander> logger) : ITemplateExpander
{
    private readonly LoopSourcePreparer _sourcePreparer = new(sourceResolver, variables, limits);

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

        WarnAboutAssignsHidingVariables(content, filePath);
        var blocks = TemplatingErrors.InFile(filePath, () => scanner.FindLoopBlocks(content));
        ValidateNestingDepth(blocks, filePath);
        var forSources = ParseForTagSources(blocks, filePath);
        var isDynamic = Enumerable.Range(0, blocks.Count).Select(i => IsDynamicSource(i, blocks, forSources)).ToArray();
        var input = new TemplateExpansionInput(content, filePath, blocks, forSources, isDynamic);
        var dynamicSourceGuard = new DynamicLoopSourceGuard(input, limits, logger);

        var model = modelBuilder.Build(variables);
        var edits = new List<TextEdit>();
        var sources = _sourcePreparer.Prepare(input, model, edits);
        dynamicSourceGuard.AddGuardEdits(edits);
        edits.AddRange(masker.FindMaskEdits(content, blocks));

        var transformed = TextEditApplier.Apply(content, edits, out var positionMap);
        var topLevelIndices = FindTopLevelIndices(blocks);
        var requestCounts = new int?[blocks.Count];
        var loopOutputs = new List<string>();
        var dataValues = new TemplateDataValues();
        var template = WrapLoops(
            input,
            RenderedValueGuard.Apply(FluidTemplateRenderer.Parse(transformed, content, positionMap, filePath), dataValues),
            topLevelIndices, requestCounts, loopOutputs);

        var options = FluidTemplateRenderer.CreateOptions(content, filePath, limits);
        dynamicSourceGuard.RegisterFilter(options);
        var renderModel = new Dictionary<string, object?>(model);
        LoopSourcePreparer.AddSourceAliases(renderModel, sources);
        var rendered = FluidTemplateRenderer.Render(template, new TemplateContext(renderModel, options), filePath, limits);
        var warnedIndices = dynamicSourceGuard.WarnAboutSourcesEmptyForEveryOuterItem();

        WarnAboutTopLevelLoopsWithoutRequests(input, requestCounts, topLevelIndices, warnedIndices);

        return new TemplateExpansionResult(
            rendered, TemplatedRequestNamesFinder.Find(content, rendered, blocks, loopOutputs))
        {
            DataValues = dataValues.ValuesByName
        };
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

    private void WarnAboutAssignsHidingVariables(string content, string filePath)
    {
        foreach (var name in AssignTags.FindNames(content).Where(variables.ContainsVariable))
        {
            logger.LogWarning(
                "'{{% assign {Name} %}}' in '{Path}' hides the TeaPie variable '{Name}' within this file; the variable " +
                "itself is not changed. Rename the assigned name if you meant to use the variable.",
                Shorten(name),
                filePath,
                Shorten(name));
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
                $"'{Shorten(blocks[deepestIndex].SourceExpression)}').");
        }
    }

    private ForTagSource[] ParseForTagSources(IReadOnlyList<LoopBlock> blocks, string filePath)
        => TemplatingErrors.InFile(filePath, () => blocks
            .Select(block => ForTagSourceParser.Parse(block.SourceExpression, limits.MaxLoopItems))
            .ToArray());

    private static bool IsDynamicSource(
        int blockIndex, IReadOnlyList<LoopBlock> blocks, IReadOnlyList<ForTagSource> forSources)
        => LoopBlockHierarchy.GetAncestorIndices(blockIndex, blocks).Exists(ancestorIndex =>
            FluidExpressionIdentifier.StartsWithIdentifier(
                forSources[blockIndex].CollectionExpression, blocks[ancestorIndex].LoopVariableName));

    private static List<int> FindTopLevelIndices(IReadOnlyList<LoopBlock> blocks)
        => Enumerable.Range(0, blocks.Count)
            .Where(i => LoopBlockHierarchy.IsTopLevel(i, blocks))
            .OrderBy(i => blocks[i].StartIndex)
            .ToList();

    private IFluidTemplate WrapLoops(
        TemplateExpansionInput input, IFluidTemplate template, IReadOnlyList<int> topLevelIndices,
        int?[] requestCounts, List<string> loopOutputs)
    {
        var (_, filePath, blocks, forSources, _) = input;
        var renderedLoopsLength = 0L;

        return TemplatingErrors.InFile(filePath, () => LoopWrapper.Wrap(
            template,
            topLevelIndices.Select(i => blocks[i].LoopVariableName).ToList(),
            limits,
            (loopIndex, requestCount, length) =>
            {
                var source = forSources[topLevelIndices[loopIndex]].CollectionExpression;
                if (requestCount > limits.MaxExpandedRequests)
                {
                    throw new InvalidOperationException(
                        $"Templating error in '{filePath}': loop over '{Shorten(source)}' would expand to more than " +
                        $"{limits.MaxExpandedRequests} requests, counted across all its nesting levels.");
                }

                if (renderedLoopsLength + length > limits.MaxExpandedLength)
                {
                    throw new InvalidOperationException(
                        $"Templating error in '{filePath}': the loops in the file would expand to more than " +
                        $"{limits.MaxExpandedLength} characters (exceeded in the loop over '{Shorten(source)}'). Check " +
                        "the loops for large bodies, or split the file.");
                }
            },
            (loopIndex, output, requestCount) =>
            {
                requestCounts[topLevelIndices[loopIndex]] = requestCount;
                renderedLoopsLength += output.Length;
                loopOutputs.Add(output);
            }));
    }

    private void WarnAboutTopLevelLoopsWithoutRequests(
        TemplateExpansionInput input, int?[] requestCounts, IReadOnlyList<int> topLevelIndices,
        IReadOnlySet<int> warnedIndices)
    {
        var blocks = input.Blocks;

        foreach (var rootIndex in topLevelIndices)
        {
            if (requestCounts[rootIndex] == 0 && RequestCountingWriter.ContainsRequestLine(blocks[rootIndex].Body)
                && !warnedIndices.Any(index => LoopBlockHierarchy.GetAncestorIndices(index, blocks).Contains(rootIndex)))
            {
                logger.LogWarning(
                    "Loop over '{Source}' in '{Path}' generated no requests, although its body declares some " +
                    "(for example because 'limit:'/'offset:' or an '{{% if %}}' excluded every item). Check the " +
                    "loop's collection, options and conditions.",
                    Shorten(input.ForSources[rootIndex].CollectionExpression),
                    input.FilePath);
            }
        }
    }
}
