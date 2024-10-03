using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.Pipelines.Runner.RunTestCase;

namespace TeaPieDraft.Pipelines.Runner.RunScript;
internal class RunScriptPipeline : PipelineBase<RunScriptContext>,
    IPipelineStep<RunScriptContext>
{
    public RunScriptPipeline() : base()
    {
    }

    public RunScriptPipeline(RunScriptContext? initialContext) : base(initialContext)
    {
    }

    internal static RunScriptPipeline CreateDefault()
    {
        var instance = new RunScriptPipeline();
        instance.AddStep(new ReadScriptContentStep());
        instance.AddStep(new PreProcessScriptStep());
        instance.AddStep(new CompileScriptStep());
        instance.AddStep(new ExecuteScriptStep());

        return instance;
    }

    internal static RunScriptPipeline Create(
        IEnumerable<IPipelineStep<RunScriptContext>> steps,
        RunScriptContext initialContext)
    {
        var instance = new RunScriptPipeline(initialContext);
        foreach (var step in steps)
        {
            instance.AddStep(step);
        }

        return instance;
    }

    public async Task<RunScriptContext> ExecuteAsync(
        RunScriptContext context,
        CancellationToken cancellationToken = default)
        => context is null
            ? throw new ArgumentNullException(nameof(context))
            : await RunAsync(context, cancellationToken);
}
