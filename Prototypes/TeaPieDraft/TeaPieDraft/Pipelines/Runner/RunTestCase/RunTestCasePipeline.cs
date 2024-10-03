using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.Pipelines.Runner.RunCollection;
using TeaPieDraft.Pipelines.Runner.RunRequest;
using TeaPieDraft.Pipelines.Runner.RunScriptsCollection;

namespace TeaPieDraft.Pipelines.Runner.RunTestCase;
internal class RunTestCasePipeline
    : PipelineBase<RunTestCaseContext>,
    IPipelineStep<RunCollectionContext>,
    IPipelineStep<RunTestCaseContext>
{
    internal RunTestCasePipeline() { }

    internal static RunTestCasePipeline CreateDefault()
    {
        var instance = new RunTestCasePipeline();
        instance.AddStep(RunScriptCollectionPipeline.CreateDefault());
        instance.AddStep(new RunRequestFileStep());
        instance.AddStep(RunScriptCollectionPipeline.CreateDefault());

        return instance;
    }

    internal static RunTestCasePipeline Create(IEnumerable<IPipelineStep<RunTestCaseContext>> steps)
    {
        var instance = new RunTestCasePipeline();
        foreach (var step in steps)
        {
            instance.AddStep(step);
        }

        return instance;
    }

    public async Task<RunCollectionContext> ExecuteAsync(
        RunCollectionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context is null) throw new ArgumentNullException("Run Collection Context");

        if (context.Current is not null)
        {
            await RunAsync(new(context.Current), cancellationToken);
        }
        else
        {
            Console.WriteLine();
        }

        return context;
    }

    public async Task<RunTestCaseContext> ExecuteAsync(RunTestCaseContext context, CancellationToken cancellationToken = default)
    {
        if (context is null) throw new ArgumentNullException("Run Collection Context");
        return await RunAsync(context, cancellationToken);
    }
}
