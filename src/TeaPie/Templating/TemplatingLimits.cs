namespace TeaPie.Templating;

internal sealed class TemplatingLimits
{
    public const int RegexTimeoutMilliseconds = 1000;

    public int MaxLoopItems { get; init; } = 1000;

    public int MaxExpandedRequests { get; init; } = 1000;

    public int MaxExpandedLength { get; init; } = 10_000_000;

    public int MaxListItems { get; init; } = 100_000;

    public int MaxRenderSteps { get; init; } = 200000;

    public int MaxNestingDepth { get; init; } = 10;
}
