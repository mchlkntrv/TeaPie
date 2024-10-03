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
    internal RunCollectionPipeline() { }

    internal static RunCollectionPipeline CreateDefault()
    {
        var instance = new RunCollectionPipeline();
        instance.AddStep(RunTestCasePipeline.CreateDefault());

        return instance;
    }

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
