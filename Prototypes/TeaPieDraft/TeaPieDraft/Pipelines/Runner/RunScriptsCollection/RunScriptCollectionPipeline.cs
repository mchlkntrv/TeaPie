using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.Pipelines.CollectionPipeline;
using TeaPieDraft.Pipelines.Runner.RunScript;
using TeaPieDraft.Pipelines.Runner.RunTestCase;
using TeaPieDraft.ScriptHandling;

namespace TeaPieDraft.Pipelines.Runner.RunScriptsCollection;
internal class RunScriptCollectionPipeline
    : CollectionPipelineBase<RunScriptsCollectionContext, RunScriptContext, ScriptExecution>,
    IPipelineStep<RunTestCaseContext>
{
    public RunScriptCollectionPipeline() : base() { }

    protected RunScriptCollectionPipeline(RunScriptsCollectionContext initialContext) : base(initialContext) { }

    internal static RunScriptCollectionPipeline CreateDefault(RunScriptsCollectionContext initialContext)
    {
        var instance = new RunScriptCollectionPipeline(initialContext);

        if (initialContext is null) throw new ArgumentNullException("Initial context");
        var itemContext = initialContext.GetItemContext();
        if (itemContext is null) throw new ArgumentNullException("Context for step.");

        instance.AddStep(RunScriptPipeline.CreateDefault(itemContext));

        return instance;
    }

    public async Task<RunTestCaseContext> ExecuteAsync(
        RunTestCaseContext context,
        CancellationToken cancellationToken = default)
    {
        if (context == null) throw new ArgumentNullException("Run Collection Context");

        if (!context.RequestExecuted)
        {
            await RunAsync(new(context.PreRequests), cancellationToken);
        }
        else
        {
            await RunAsync(new(context.PostResponses), cancellationToken);
        }

        return context;
    }
}
