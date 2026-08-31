using System.Text;
using Fluid;
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

    private const string SourceAlias = "__teapie_loop_source";

    private static readonly FluidParser Parser = new();

    public string Expand(string content, string filePath)
    {
        if (!content.Contains("{%", StringComparison.Ordinal))
        {
            return content;
        }

        var blocks = scanner.FindLoopBlocks(content);
        if (blocks.Count == 0)
        {
            return content;
        }

        var result = new StringBuilder();
        var cursor = 0;

        foreach (var block in blocks)
        {
            result.Append(content, cursor, block.StartIndex - cursor);
            result.Append(ExpandBlock(block, filePath));
            cursor = block.StartIndex + block.Length;
        }

        result.Append(content, cursor, content.Length - cursor);
        return result.ToString();
    }

    private string ExpandBlock(LoopBlock block, string filePath)
    {
        var source = sourceResolver.Resolve(block.SourceExpression);

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

        var maskedBody = masker.Mask(block.Body, block.LoopVariableName);
        var forSource = source.Collection is not null ? SourceAlias : block.SourceExpression;
        var reconstructed = $"{{% for {block.LoopVariableName} in {forSource} %}}{maskedBody}{{% endfor %}}";

        if (!Parser.TryParse(reconstructed, out var template, out var parseError))
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': failed to parse loop over '{block.SourceExpression}': {parseError}");
        }

        var options = new TemplateOptions
        {
            MemberAccessStrategy = new UnsafeMemberAccessStrategy(),
            MaxSteps = MaxRenderSteps
        };
        options.Undefined = name => throw new InvalidOperationException(
            $"Templating error in '{filePath}': '{name}' is undefined while expanding the loop over '{block.SourceExpression}'.");

        var model = new Dictionary<string, object?>(modelBuilder.Build(variables));
        if (source.Collection is not null)
        {
            model[SourceAlias] = source.Collection;
        }

        var context = new TemplateContext(model, options);

        string rendered;
        try
        {
            rendered = template!.Render(context);
        }
        catch (InvalidOperationException ex) when (IsRenderStepLimitExceeded(ex))
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': loop over '{block.SourceExpression}' ({source.ItemCount} " +
                $"item(s)) exceeded the maximum of {MaxRenderSteps} rendering steps - likely too many '{{ }}' " +
                "expressions per item rather than a large collection (collection size is capped separately). " +
                "Check the loop body for repeated expressions, or split it into smaller loops.", ex);
        }

        if (!string.IsNullOrWhiteSpace(block.Body) && rendered.Length == 0)
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': loop over '{block.SourceExpression}' resolved " +
                $"{source.ItemCount} item(s) but rendered empty output. This indicates a templating " +
                "engine issue (e.g. a naming collision) rather than a genuinely empty collection.");
        }

        return rendered;
    }

    private static bool IsRenderStepLimitExceeded(InvalidOperationException ex)
        => ex.Message.Contains("recursion", StringComparison.OrdinalIgnoreCase);
}
