using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.Pipelines.Runner.RunTestCase;

namespace TeaPieDraft.Pipelines.Runner.RunScript;
internal class RunScriptPipeline : PipelineBase<RunScriptContext>,
    IPipelineStep<RunScriptContext>
{
    public RunScriptPipeline() : base() { }

    internal static RunScriptPipeline CreateDefault()
    {
        var instance = new RunScriptPipeline();
        instance.AddStep(new ReadScriptContentStep());
        instance.AddStep(new PreProcessScriptStep());
        instance.AddStep(new CompileScriptStep());
        instance.AddStep(new ExecuteScriptStep());

        return instance;
    }

    public async Task<RunScriptContext> ExecuteAsync(
        RunScriptContext context,
        CancellationToken cancellationToken = default)
        => context is null
            ? throw new ArgumentNullException(nameof(context))
            : await RunAsync(context, cancellationToken);
}
