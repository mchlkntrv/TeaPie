namespace TeaPie.Templating;

internal sealed record TemplateExpansionResult(string Content, TemplatedRequestNames RequestNames)
{
    public IReadOnlyDictionary<string, string> DataValues { get; init; } = TemplateDataValues.None;
}

internal sealed record TemplatedRequestNames(IReadOnlySet<string> Repeatable, IReadOnlySet<string> GeneratedByLoops)
{
    public static readonly TemplatedRequestNames None =
        new(new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
}
