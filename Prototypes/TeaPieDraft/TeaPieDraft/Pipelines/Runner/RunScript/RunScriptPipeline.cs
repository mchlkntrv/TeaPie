using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.Pipelines.Runner.RunTestCase;

namespace TeaPieDraft.Pipelines.Runner.RunScript;
internal class RunScriptPipeline : PipelineBase<RunScriptContext>,
    IPipelineStep<RunTestCaseContext>,
    IPipelineStep<RunScriptContext>
{
    public RunScriptPipeline() : base()
    {
    }

    public RunScriptPipeline(RunScriptContext? initialContext) : base(initialContext)
    {
    }

    internal static RunScriptPipeline CreateDefault(RunScriptContext initialContext)
    {
        var instance = new RunScriptPipeline(initialContext);
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

    public async Task<RunTestCaseContext> ExecuteAsync(RunTestCaseContext context, CancellationToken cancellationToken = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var currentScript = context.Current;

        if (currentScript is null) throw new ArgumentNullException("Current script is null.");

        context.Current = await RunAsync(new(currentScript), cancellationToken);

        return context;
    }

    public async Task<RunScriptContext> ExecuteAsync(
        RunScriptContext context,
        CancellationToken cancellationToken = default)
        => context is null
            ? throw new ArgumentNullException(nameof(context))
            : await RunAsync(context, cancellationToken);
}
