using System.Text;
using Fluid;

namespace TeaPie.Templating;

internal sealed class TemplateExpander(
    ILoopBlockScanner scanner,
    ILoopBodyMasker masker,
    ICollectionSourceResolver sourceResolver) : ITemplateExpander
{
    private const int MaxExpandedRequests = 1000;
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
        var reconstructed = $"{{% for {block.LoopVariableName} in {block.SourceExpression} %}}{maskedBody}{{% endfor %}}";

        if (!Parser.TryParse(reconstructed, out var template, out var parseError))
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': failed to parse loop over '{block.SourceExpression}': {parseError}");
        }

        var options = new TemplateOptions { MemberAccessStrategy = new UnsafeMemberAccessStrategy() };
        options.Undefined = name => throw new InvalidOperationException(
            $"Templating error in '{filePath}': '{name}' is undefined while expanding the loop over '{block.SourceExpression}'.");

        var model = new Dictionary<string, object?>();
        if (source.Collection is not null)
        {
            model[block.SourceExpression] = source.Collection;
        }

        var context = new TemplateContext(model, options);
        return template!.Render(context);
    }
}
