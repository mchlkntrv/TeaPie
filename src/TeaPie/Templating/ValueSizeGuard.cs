using Fluid;
using Fluid.Values;

namespace TeaPie.Templating;

// A value that is built from itself, such as 'assign s = s | append: s' in a loop, doubles with every step without
// writing anything, so the size of every filter result and captured value is checked where it is created.
internal static class ValueSizeGuard
{
    public static void WrapFilters(FilterCollection filters, TemplatingLimits limits)
    {
        foreach (var (name, filter) in filters.ToList())
        {
            filters.AddFilter(name, (input, arguments, context) =>
            {
                var result = filter(input, arguments, context);
                return result.IsCompletedSuccessfully
                    ? new ValueTask<FluidValue>(EnsureWithinLimits(result.Result, $"filter '{name}'", limits))
                    : CheckWhenCompleted(result, name, limits);
            });
        }
    }

    public static FluidValue EnsureWithinLimits(FluidValue value, string producer, TemplatingLimits limits)
    {
        if (value is StringValue && value.ToStringValue().Length > limits.MaxExpandedLength)
        {
            throw new InvalidOperationException(
                $"Templating error: {producer} produced a text longer than {limits.MaxExpandedLength} characters. " +
                "Check the file for a value that is built from itself, such as 'assign s = s | append: s' in a loop.");
        }

        if (value is ArrayValue array && array.Values.Count > limits.MaxListItems)
        {
            throw new InvalidOperationException(
                $"Templating error: {producer} produced a list with more than {limits.MaxListItems} items. " +
                "Check the file for a value that is built from itself, such as 'assign a = a | concat: a' in a loop.");
        }

        return value;
    }

    private static async ValueTask<FluidValue> CheckWhenCompleted(
        ValueTask<FluidValue> result, string name, TemplatingLimits limits)
        => EnsureWithinLimits(await result, $"filter '{name}'", limits);
}
