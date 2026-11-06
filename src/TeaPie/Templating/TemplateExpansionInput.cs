namespace TeaPie.Templating;

internal sealed record TemplateExpansionInput(
    string Content,
    string FilePath,
    IReadOnlyList<LoopBlock> Blocks,
    IReadOnlyList<ForTagSource> ForSources,
    IReadOnlyList<bool> IsDynamic);
