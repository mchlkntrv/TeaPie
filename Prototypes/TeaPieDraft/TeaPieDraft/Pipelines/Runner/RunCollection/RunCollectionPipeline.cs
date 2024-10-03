using TeaPieDraft.Pipelines.Application;
using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.Pipelines.CollectionPipeline;
using TeaPieDraft.Pipelines.Runner.RunTestCase;
using TeaPieDraft.ScriptHandling;

namespace TeaPieDraft.Pipelines.Runner.RunCollection;

internal class RunCollectionPipeline
    : CollectionPipelineBase<RunCollectionContext, RunTestCaseContext, TestCaseExecution>,
    IPipelineStep<ApplicationContext>
{
    internal RunCollectionPipeline(RunCollectionContext runningContext) : base(runningContext) { }

    internal static RunCollectionPipeline CreateDefault(RunCollectionContext initialContext)
    {
        var instance = new RunCollectionPipeline(initialContext);

        if (initialContext is null) throw new ArgumentNullException("Initial context");
        var itemContext = initialContext.GetItemContext();
        if (itemContext is null) throw new ArgumentNullException("Context for step is null.");

        instance.AddStep(RunTestCasePipeline.CreateDefault(itemContext));

        return instance;
    }

    internal static RunCollectionPipeline CreateDefault(ApplicationContext initialContext)
        => CreateDefault(initialContext.RunningContext!);

    public async Task<ApplicationContext> ExecuteAsync(
        ApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (context.RunningContext is null) throw new ArgumentNullException("Running context");

        context.RunningContext = await RunAsync(context.RunningContext, cancellationToken);
        return context;
    }
}
