using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.ScriptHandling;

namespace TeaPieDraft.Pipelines.Runner.RunScript;
internal class PreProcessScriptStep : IPipelineStep<RunScriptContext>
{
    private readonly ScriptPreProcessor _preProcessor;

    public PreProcessScriptStep()
    {
        _preProcessor = new ScriptPreProcessor();
    }

    internal PreProcessScriptStep(ScriptPreProcessor preProcessor)
    {
        _preProcessor = preProcessor;
    }

    public async Task<RunScriptContext> ExecuteAsync(RunScriptContext context, CancellationToken cancellationToken = default)
    {
        if (context?.RawContent is null) throw new ArgumentNullException("Content of current script is null.");

        context.ProcessedContent = await _preProcessor.PrepareScriptAsync(context.Structure!.Path!, context.RawContent);

        return context;
    }
}
