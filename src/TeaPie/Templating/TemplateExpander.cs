using System.Collections;
using System.Text.RegularExpressions;
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using TeaPie.Http.Parsing;
using TeaPie.Variables;

namespace TeaPie.Templating;

internal sealed partial class TemplateExpander(
    ILoopBlockScanner scanner,
    ILoopBodyMasker masker,
    ICollectionSourceResolver sourceResolver,
    IVariablesFluidModelBuilder modelBuilder,
    IVariables variables,
    TemplatingLimits limits) : ITemplateExpander
{
    private const string SourceAliasPrefix = "__teapie_loop_source_";
    private const string TreeStartMarkerPrefix = "\u0000__teapie_loop_tree_start_";
    private const string TreeEndMarkerPrefix = "\u0000__teapie_loop_tree_end_";
    private const string MarkerSuffix = "__\u0000";

    private static readonly FluidParser Parser = new();

    public string Expand(string content, string filePath)
    {
        if (!content.Contains("{%", StringComparison.Ordinal))
        {
            return content;
        }

        var blocks = FindLoopBlocks(scanner, content, filePath);
        var edits = new List<TextEdit>();
        var sources = ResolveSourcesAndCollectAliasEdits(blocks, filePath, edits);

        ValidateDynamicSources(blocks, sources, filePath);

        var nestingRootIndices = FindNestingRootIndices(blocks);
        AddNestingTreeMarkerEdits(edits, blocks, nestingRootIndices);

        edits.AddRange(masker.FindMaskEdits(content, blocks));
        var topLevelNames = masker.FindTopLevelAssignTargetNames(content, blocks);

        var transformed = TextEditApplier.Apply(content, edits, out var positionMap);
        var template = ParseTemplate(transformed, content, positionMap, filePath);

        var (context, topLevelAssignments) = BuildRenderContext(blocks, sources, topLevelNames, filePath);
        var rendered = RenderTemplate(template, context, filePath);

        ValidateNestedTreeRequestCounts(rendered, blocks, nestingRootIndices, filePath);
        rendered = StripNestingTreeMarkers(rendered, nestingRootIndices);

        PropagateTopLevelAssignments(topLevelAssignments);

        return rendered;
    }

    private LoopSource?[] ResolveSourcesAndCollectAliasEdits(
        IReadOnlyList<LoopBlock> blocks, string filePath, List<TextEdit> edits)
    {
        var sources = new LoopSource?[blocks.Count];

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var (effectiveExpression, isRequired) = SplitRequiredModifier(block.SourceExpression);

            if (IsDynamicSource(i, blocks))
            {
                if (isRequired)
                {
                    edits.Add(new TextEdit(
                        block.SourceExpressionStartIndex, block.SourceExpressionRawLength, effectiveExpression));
                }

                continue;
            }

            var source = ResolveSource(sourceResolver, effectiveExpression, filePath);
            sources[i] = source;

            if (source.ItemCount == 0)
            {
                throw new InvalidOperationException(
                    $"Templating error in '{filePath}': loop over '{effectiveExpression}' produced zero items.");
            }

            if (LoopBlockHierarchy.IsStandaloneBlock(i, blocks) && source.ItemCount > limits.MaxExpandedRequests)
            {
                throw new InvalidOperationException(
                    $"Templating error in '{filePath}': loop over '{block.SourceExpression}' would expand to " +
                    $"{source.ItemCount} requests, exceeding the maximum of {limits.MaxExpandedRequests}.");
            }

            if (source.Collection is not null)
            {
                edits.Add(new TextEdit(
                    block.SourceExpressionStartIndex, block.SourceExpressionRawLength, $"{SourceAliasPrefix}{i}"));
            }
        }

        return sources;
    }

    private static List<int> FindNestingRootIndices(IReadOnlyList<LoopBlock> blocks)
        => Enumerable.Range(0, blocks.Count).Where(i => LoopBlockHierarchy.IsNestingRoot(i, blocks)).ToList();

    private static void AddNestingTreeMarkerEdits(
        List<TextEdit> edits, IReadOnlyList<LoopBlock> blocks, IReadOnlyList<int> nestingRootIndices)
    {
        foreach (var rootIndex in nestingRootIndices)
        {
            var block = blocks[rootIndex];
            edits.Add(new TextEdit(block.StartIndex, 0, $"{TreeStartMarkerPrefix}{rootIndex}{MarkerSuffix}"));
            edits.Add(new TextEdit(block.StartIndex + block.Length, 0, $"{TreeEndMarkerPrefix}{rootIndex}{MarkerSuffix}"));
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
                $"Templating error in '{filePath}': failed to parse template: {originalError}. If this file " +
                "contains literal '{{%' text that is not a TeaPie template tag, wrap it in " +
                "'{{% raw %}}...{{% endraw %}}'.");
        }

        return template;
    }

    private (TemplateContext Context, Dictionary<string, FluidValue> TopLevelAssignments) BuildRenderContext(
        IReadOnlyList<LoopBlock> blocks, LoopSource?[] sources, IReadOnlySet<string> topLevelNames, string filePath)
    {
        var options = new TemplateOptions
        {
            MemberAccessStrategy = new UnsafeMemberAccessStrategy(),
            MaxSteps = limits.MaxRenderSteps
        };
        options.Undefined = name => throw new InvalidOperationException(
            $"Templating error in '{filePath}': '{name}' is undefined.");

        var model = new Dictionary<string, object?>(modelBuilder.Build(variables));
        for (var i = 0; i < blocks.Count; i++)
        {
            if (sources[i] is { Collection: not null } source)
            {
                model[$"{SourceAliasPrefix}{i}"] = source.Collection;
            }
        }

        var context = new TemplateContext(model, options);

        var topLevelAssignments = new Dictionary<string, FluidValue>(StringComparer.Ordinal);
        context.Assigned = (identifier, value, _) =>
        {
            if (topLevelNames.Contains(identifier))
            {
                topLevelAssignments[identifier] = value;
            }

            return new ValueTask<FluidValue>(value);
        };

        return (context, topLevelAssignments);
    }

    private string RenderTemplate(IFluidTemplate template, TemplateContext context, string filePath)
    {
        try
        {
            return template.Render(context);
        }
        catch (InvalidOperationException ex) when (IsRenderStepLimitExceeded(ex))
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': template exceeded the maximum of {limits.MaxRenderSteps} " +
                "rendering steps across the whole file - likely too many '{{ }}' " +
                "expressions per item rather than a large collection (collection size is capped separately " +
                "per loop). Check the file for repeated expressions, or split it into smaller loops.", ex);
        }
    }

    private void ValidateNestedTreeRequestCounts(
        string rendered, IReadOnlyList<LoopBlock> blocks, IReadOnlyList<int> nestingRootIndices, string filePath)
    {
        foreach (var rootIndex in nestingRootIndices)
        {
            var startMarker = $"{TreeStartMarkerPrefix}{rootIndex}{MarkerSuffix}";
            var endMarker = $"{TreeEndMarkerPrefix}{rootIndex}{MarkerSuffix}";
            var startIndex = rendered.IndexOf(startMarker, StringComparison.Ordinal);
            var endIndex = rendered.IndexOf(endMarker, StringComparison.Ordinal);

            if (startIndex < 0 || endIndex < 0)
            {
                continue;
            }

            var segment = rendered[(startIndex + startMarker.Length)..endIndex];

            var requestCount = RequestSeparatorRegex().Split(segment)
                .Count(fragment => RequestMethodAndUriLineRegex().IsMatch(fragment));

            if (requestCount > limits.MaxExpandedRequests)
            {
                throw new InvalidOperationException(
                    $"Templating error in '{filePath}': the nested loop tree would expand to {requestCount} " +
                    "requests combined across all nesting levels, exceeding the maximum of " +
                    $"{limits.MaxExpandedRequests} (root loop over '{blocks[rootIndex].SourceExpression}').");
            }
        }
    }

    private static string StripNestingTreeMarkers(string rendered, IReadOnlyList<int> nestingRootIndices)
    {
        foreach (var rootIndex in nestingRootIndices)
        {
            rendered = rendered
                .Replace($"{TreeStartMarkerPrefix}{rootIndex}{MarkerSuffix}", string.Empty, StringComparison.Ordinal)
                .Replace($"{TreeEndMarkerPrefix}{rootIndex}{MarkerSuffix}", string.Empty, StringComparison.Ordinal);
        }

        return rendered;
    }

    private void PropagateTopLevelAssignments(Dictionary<string, FluidValue> topLevelAssignments)
    {
        foreach (var (name, value) in topLevelAssignments)
        {
            variables.SetVariable(name, value.ToObjectValue());
        }
    }

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

    private static void ValidateDynamicSources(IReadOnlyList<LoopBlock> blocks, LoopSource?[] sources, string filePath)
    {
        var childIndicesByParent = new Dictionary<int, List<int>>();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (LoopBlockHierarchy.IsTopLevel(i, blocks))
            {
                continue;
            }

            var parentIndex = LoopBlockHierarchy.GetAncestorIndices(i, blocks)[0];
            (childIndicesByParent.TryGetValue(parentIndex, out var siblings)
                ? siblings
                : childIndicesByParent[parentIndex] = []).Add(i);
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            if (LoopBlockHierarchy.IsTopLevel(i, blocks) && sources[i]?.Collection is { } rootCollection)
            {
                WalkDynamicDescendants(
                    i, rootCollection, new Dictionary<string, object?>(), string.Empty, blocks, sources,
                    childIndicesByParent, filePath);
            }
        }
    }

    private static void WalkDynamicDescendants(
        int parentIndex, IEnumerable parentCollection, IReadOnlyDictionary<string, object?> boundAncestors,
        string pathPrefix, IReadOnlyList<LoopBlock> blocks, LoopSource?[] sources,
        Dictionary<int, List<int>> childIndicesByParent, string filePath)
    {
        if (!childIndicesByParent.TryGetValue(parentIndex, out var childIndices))
        {
            return;
        }

        var parentBlock = blocks[parentIndex];
        var index = 0;

        foreach (var item in parentCollection)
        {
            var bound = new Dictionary<string, object?>(boundAncestors) { [parentBlock.LoopVariableName] = item };
            var path = $"{pathPrefix}{parentBlock.LoopVariableName}[{index}]";

            foreach (var childIndex in childIndices)
            {
                var childBlock = blocks[childIndex];

                var childCollection = sources[childIndex]?.Collection
                    ?? EvaluateAndGuardDynamicSource(childBlock, bound, path, filePath);

                WalkDynamicDescendants(
                    childIndex, childCollection, bound, $"{path}.", blocks, sources, childIndicesByParent, filePath);
            }

            index++;
        }
    }

    private static IEnumerable EvaluateAndGuardDynamicSource(
        LoopBlock block, IReadOnlyDictionary<string, object?> boundAncestors, string path, string filePath)
    {
        var (effectiveExpression, isRequired) = SplitRequiredModifier(block.SourceExpression);

        if (!Parser.TryParse($"{{{{ {effectiveExpression} }}}}", out var probe, out var parseError) ||
            ((IStatementList)probe).Statements is not [OutputStatement { Expression: var expression }])
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': failed to parse loop source '{effectiveExpression}': " +
                $"{parseError}.");
        }

        var options = new TemplateOptions { MemberAccessStrategy = new UnsafeMemberAccessStrategy() };
        var context = new TemplateContext(new Dictionary<string, object?>(boundAncestors), options);
        var value = expression.EvaluateAsync(context).GetAwaiter().GetResult();
        var raw = value.ToObjectValue();

        if (raw is not IEnumerable enumerable || raw is string)
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': variable '{effectiveExpression}' referenced in a " +
                $"'{{% for %}}' loop must be a collection ({path}).");
        }

        var materialized = enumerable.Cast<object?>().ToList();

        if (isRequired && materialized.Count == 0)
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': loop over '{effectiveExpression}' produced zero items " +
                $"({path}).");
        }

        return materialized;
    }

    private static (string Expression, bool IsRequired) SplitRequiredModifier(string sourceExpression)
    {
        var match = RequiredModifierRegex().Match(sourceExpression);
        return match.Success
            ? (match.Groups["expr"].Value.TrimEnd(), true)
            : (sourceExpression, false);
    }

    [GeneratedRegex(@"^(?<expr>.+?)\s*\|\s*required\s*$")]
    private static partial Regex RequiredModifierRegex();

    private static LoopSource ResolveSource(ICollectionSourceResolver sourceResolver, string sourceExpression, string filePath)
    {
        try
        {
            return sourceResolver.Resolve(sourceExpression);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': {StripTemplatingErrorPrefix(ex.Message)}", ex);
        }
    }

    private static string StripTemplatingErrorPrefix(string message)
    {
        const string prefix = "Templating error: ";
        return message.StartsWith(prefix, StringComparison.Ordinal) ? message[prefix.Length..] : message;
    }

    private static bool IsRenderStepLimitExceeded(InvalidOperationException ex)
        => ex.Message.Contains(FluidMessageFormats.RenderStepLimitMarker, StringComparison.OrdinalIgnoreCase);

    private static bool IsDynamicSource(int blockIndex, IReadOnlyList<LoopBlock> blocks)
    {
        var block = blocks[blockIndex];

        return LoopBlockHierarchy.GetAncestorIndices(blockIndex, blocks).Exists(ancestorIndex =>
            FluidExpressionIdentifier.StartsWithIdentifier(block.SourceExpression, blocks[ancestorIndex].LoopVariableName));
    }

    [GeneratedRegex(HttpFileParserConstants.HttpRequestSeparatorDirectiveLineRegex)]
    private static partial Regex RequestSeparatorRegex();

    [GeneratedRegex(HttpFileParserConstants.RequestMethodAndUriLinePattern, RegexOptions.IgnoreCase)]
    private static partial Regex RequestMethodAndUriLineRegex();
}
