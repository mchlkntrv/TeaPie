using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.Pipelines.CollectionPipeline;
using TeaPieDraft.Pipelines.Runner.RunScript;
using TeaPieDraft.ScriptHandling;

namespace TeaPieDraft.Pipelines.Runner.RunScriptsCollection;
internal class RunScriptCollectionPipeline
    : CollectionPipelineBase<RunScriptsCollectionContext, RunScriptContext, ScriptExecution>
{
    public RunScriptCollectionPipeline() : base()
    {
    }

    protected RunScriptCollectionPipeline(RunScriptsCollectionContext initialContext) : base(initialContext)
    {
    }

    internal static RunScriptCollectionPipeline CreateDefault(RunScriptsCollectionContext initialContext)
    {
        var instance = new RunScriptCollectionPipeline(initialContext);

        if (initialContext is null) throw new ArgumentNullException("Initial context");
        var itemContext = initialContext.GetItemContext();
        if (itemContext is null) throw new ArgumentNullException("Context for step is null.");

        instance.AddStep(RunScriptPipeline.CreateDefault(itemContext));

        return instance;
    }

    internal static RunScriptCollectionPipeline Create(
        IEnumerable<IPipelineStep<RunScriptContext>> steps,
        RunScriptsCollectionContext initialContext)
    {
        var instance = new RunScriptCollectionPipeline(initialContext);
        foreach (var step in steps)
        {
            instance.AddStep(step);
        }

        return instance;
    }
}
