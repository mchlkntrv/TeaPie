using Fluid;
using TeaPie.Json;
using static TeaPie.Templating.TemplatingMessageText;

namespace TeaPie.Templating;

internal static class FluidTemplateRenderer
{
    private static readonly FluidParser _parser = new();

    public static IFluidTemplate Parse(
        string transformed, string originalContent, IReadOnlyList<TransformedTextSpan> positionMap, string filePath)
    {
        if (!_parser.TryParse(transformed, out var template, out var parseError))
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

    public static TemplateOptions CreateOptions(string content, string filePath, TemplatingLimits limits)
    {
        var options = new TemplateOptions
        {
            MemberAccessStrategy = new UnsafeMemberAccessStrategy(),
            Undefined = name => throw new InvalidOperationException(
                $"Templating error in '{filePath}': {UndefinedMemberLocator.Describe(name, content)}")
        };

        options.ValueConverters.Add(value => value is CaseInsensitiveExpandoObject expando ? expando.Properties : null);
        options.MaxSteps = limits.MaxRenderSteps;
        ValueSizeGuard.WrapFilters(options.Filters, limits);
        return options;
    }

    public static string Render(
        IFluidTemplate template, TemplateContext context, string filePath, TemplatingLimits limits)
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
            when (ex.Message.StartsWith(TemplatingErrors.Prefix + ": ", StringComparison.Ordinal))
        {
            throw TemplatingErrors.InFile(filePath, ex.Message, ex);
        }
        catch (Exception ex) when (IsUnreportedRenderError(ex))
        {
            throw new InvalidOperationException(
                $"Templating error in '{filePath}': rendering failed: {ShortenQuoted(ex.Message)}", ex);
        }
    }

    private static bool IsUnreportedRenderError(Exception ex)
        => ex is not (OutOfMemoryException or OperationCanceledException)
            && !ex.Message.StartsWith(TemplatingErrors.Prefix, StringComparison.Ordinal);

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
}
