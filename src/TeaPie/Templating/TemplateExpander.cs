using System.Text.RegularExpressions;
using Fluid;
using Fluid.Values;
using TeaPie.Http.Parsing;
using TeaPie.Variables;

namespace TeaPie.Templating;

internal sealed partial class TemplateExpander(
    ILoopBlockScanner scanner,
    ILoopBodyMasker masker,
    ICollectionSourceResolver sourceResolver,
    IVariablesFluidModelBuilder modelBuilder,
    IVariables variables) : ITemplateExpander
{
    private const int MaxExpandedRequests = 1000;
    private const int MaxRenderSteps = 200000;

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

        var blocks = scanner.FindLoopBlocks(content);
        var sources = new LoopSource?[blocks.Count];
        var edits = new List<TextEdit>();

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];

            if (IsDynamicSource(i, blocks))
            {
                continue;
            }

            var source = sourceResolver.Resolve(block.SourceExpression);
            sources[i] = source;

            if (source.ItemCount == 0)
            {
                throw new InvalidOperationException(
                    $"Templating error in '{filePath}': loop over '{block.SourceExpression}' produced zero items.");
            }

            if (LoopBlockHierarchy.IsStandaloneBlock(i, blocks) && source.ItemCount > MaxExpandedRequests)
            {
                throw new InvalidOperationException(
                    $"Templating error in '{filePath}': loop over '{block.SourceExpression}' would expand to " +
                    $"{source.ItemCount} requests, exceeding the maximum of {MaxExpandedRequests}.");
            }

            if (source.Collection is not null)
            {
                edits.Add(new TextEdit(
                    block.SourceExpressionStartIndex, block.SourceExpressionRawLength, $"{SourceAliasPrefix}{i}"));
            }
        }

        var nestingRootIndices = Enumerable.Range(0, blocks.Count)
            .Where(i => LoopBlockHierarchy.IsNestingRoot(i, blocks))
            .ToList();

        foreach (var rootIndex in nestingRootIndices)
        {
            var block = blocks[rootIndex];
            edits.Add(new TextEdit(block.StartIndex, 0, $"{TreeStartMarkerPrefix}{rootIndex}{MarkerSuffix}"));
            edits.Add(new TextEdit(block.StartIndex + block.Length, 0, $"{TreeEndMarkerPrefix}{rootIndex}{MarkerSuffix}"));
        }

        edits.AddRange(masker.FindMaskEdits(content, blocks));

        var topLevelNames = masker.FindTopLevelAssignTargetNames(content, blocks);

        var transformed = TextEditApplier.Apply(content, edits);

        if (!Parser.TryParse(transformed, out var template, out var parseError))
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': failed to parse template: {parseError}. If this file " +
                "contains literal '{{%' text that is not a TeaPie template tag, wrap it in " +
                "'{{% raw %}}...{{% endraw %}}'.");
        }

        var options = new TemplateOptions
        {
            MemberAccessStrategy = new UnsafeMemberAccessStrategy(),
            MaxSteps = MaxRenderSteps
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

        string rendered;
        try
        {
            rendered = template!.Render(context);
        }
        catch (InvalidOperationException ex) when (IsRenderStepLimitExceeded(ex))
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': template exceeded the maximum of {MaxRenderSteps} " +
                "rendering steps across the whole file - likely too many '{{ }}' " +
                "expressions per item rather than a large collection (collection size is capped separately " +
                "per loop). Check the file for repeated expressions, or split it into smaller loops.", ex);
        }

        foreach (var rootIndex in nestingRootIndices)
        {
            var startMarker = $"{TreeStartMarkerPrefix}{rootIndex}{MarkerSuffix}";
            var endMarker = $"{TreeEndMarkerPrefix}{rootIndex}{MarkerSuffix}";
            var startIndex = rendered.IndexOf(startMarker, StringComparison.Ordinal);
            var endIndex = rendered.IndexOf(endMarker, StringComparison.Ordinal);

            if (startIndex < 0 || endIndex < 0)
            {
                // The nesting-root tree sits inside a top-level if/unless condition that was
                // false, so Fluid never rendered the block (or its markers) at all. Nothing to count.
                continue;
            }

            var segment = rendered[(startIndex + startMarker.Length)..endIndex];

            var requestCount = RequestSeparatorRegex().Split(segment)
                .Count(fragment => RequestMethodAndUriLineRegex().IsMatch(fragment));

            if (requestCount > MaxExpandedRequests)
            {
                throw new InvalidOperationException(
                    $"Templating error in '{filePath}': the nested loop tree would expand to {requestCount} " +
                    $"requests combined across all nesting levels, exceeding the maximum of {MaxExpandedRequests} " +
                    $"(root loop over '{blocks[rootIndex].SourceExpression}').");
            }
        }

        foreach (var rootIndex in nestingRootIndices)
        {
            rendered = rendered
                .Replace($"{TreeStartMarkerPrefix}{rootIndex}{MarkerSuffix}", string.Empty, StringComparison.Ordinal)
                .Replace($"{TreeEndMarkerPrefix}{rootIndex}{MarkerSuffix}", string.Empty, StringComparison.Ordinal);
        }

        foreach (var (name, value) in topLevelAssignments)
        {
            variables.SetVariable(name, value.ToObjectValue());
        }

        return rendered;
    }

    private static bool IsRenderStepLimitExceeded(InvalidOperationException ex)
        => ex.Message.Contains("recursion", StringComparison.OrdinalIgnoreCase);

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
