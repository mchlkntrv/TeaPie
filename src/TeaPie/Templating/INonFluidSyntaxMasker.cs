namespace TeaPie.Templating;

internal interface INonFluidSyntaxMasker
{
    IReadOnlyList<TextEdit> FindMaskEdits(string content, IReadOnlyList<LoopBlock> blocks);
}
