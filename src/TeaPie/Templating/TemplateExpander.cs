using System.Text;
using Fluid;

namespace TeaPie.Templating;

internal sealed class TemplateExpander(
    ILoopBlockScanner scanner,
    ILoopBodyMasker masker,
    ICollectionSourceResolver sourceResolver) : ITemplateExpander
{
    private const int MaxExpandedRequests = 1000;

    // Fluid parses dots in a 'for x in Y' source expression as member access (Y.Z means
    // "member Z of Y"), never as a literal dictionary key. TeaPie's own resolution of the
    // (possibly dotted) source expression via ICollectionSourceResolver is the only resolution
    // that matters, so named collections are always rebound under this fixed, dot-free alias
    // before being handed to Fluid — this keeps Fluid's member-access parsing away from the
    // original source expression entirely. Numeric ranges never go through the model, so they
    // keep using the literal range expression directly.
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

        var options = new TemplateOptions { MemberAccessStrategy = new UnsafeMemberAccessStrategy() };
        options.Undefined = name => throw new InvalidOperationException(
            $"Templating error in '{filePath}': '{name}' is undefined while expanding the loop over '{block.SourceExpression}'.");

        var model = new Dictionary<string, object?>();
        if (source.Collection is not null)
        {
            model[SourceAlias] = source.Collection;
        }

        var context = new TemplateContext(model, options);
        var rendered = template!.Render(context);

        // Belt-and-braces guard: TeaPie's own resolution already found 'source.ItemCount' items
        // and the loop body is non-empty, so a genuinely empty render here means some other
        // naming collision (not necessarily the dotted-name one this alias fixes) produced the
        // same silent-zero-iteration symptom. Fail loudly instead of shipping a green run with
        // zero requests actually expanded.
        if (source.ItemCount > 0 && !string.IsNullOrWhiteSpace(block.Body) && rendered.Length == 0)
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': loop over '{block.SourceExpression}' resolved " +
                $"{source.ItemCount} item(s) but rendered empty output. This indicates a templating " +
                "engine issue (e.g. a naming collision) rather than a genuinely empty collection.");
        }

        return rendered;
    }
}
