using Fluid;
using Fluid.Values;
using TeaPie.Variables;

namespace TeaPie.Templating;

internal sealed class TemplateExpander(
    ILoopBlockScanner scanner,
    ILoopBodyMasker masker,
    ICollectionSourceResolver sourceResolver,
    IVariablesFluidModelBuilder modelBuilder,
    IVariables variables) : ITemplateExpander
{
    private const int MaxExpandedRequests = 1000;
    private const int MaxRenderSteps = 200000;

    private const string SourceAliasPrefix = "__teapie_loop_source_";

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
            var source = sourceResolver.Resolve(block.SourceExpression);
            sources[i] = source;

            if (source.ItemCount == 0)
            {
                throw new InvalidOperationException(
                    $"Templating error in '{filePath}': loop over '{block.SourceExpression}' produced zero items.");
            }

            if (source.ItemCount > MaxExpandedRequests)
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
            if (sources[i]!.Value.Collection is not null)
            {
                model[$"{SourceAliasPrefix}{i}"] = sources[i]!.Value.Collection;
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

        foreach (var (name, value) in topLevelAssignments)
        {
            variables.SetVariable(name, value.ToObjectValue());
        }

        return rendered;
    }

    private static bool IsRenderStepLimitExceeded(InvalidOperationException ex)
        => ex.Message.Contains("recursion", StringComparison.OrdinalIgnoreCase);
}
