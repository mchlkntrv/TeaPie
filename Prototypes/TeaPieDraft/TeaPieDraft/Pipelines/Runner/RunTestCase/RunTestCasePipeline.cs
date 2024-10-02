using TeaPieDraft.Pipelines.Base;

namespace TeaPieDraft.Pipelines.Runner.RunTestCase;
internal class RunTestCasePipeline : PipelineBase<RunTestCaseContext>
{
    private readonly List<IPipelineStep<RunTestCaseContext>> _preRequestsSteps = [];
    private readonly List<IPipelineStep<RunTestCaseContext>> _postResponsesSteps = [];

    public RunTestCasePipeline(RunTestCaseContext? initialContext) : base(initialContext)
    {
    }

    internal static RunTestCasePipeline CreateDefault(RunTestCaseContext initialContext)
    {
        var instance = new RunTestCasePipeline(initialContext);

        return instance;
    }

    internal static RunTestCasePipeline Create(
        IEnumerable<IPipelineStep<RunTestCaseContext>> steps,
        RunTestCaseContext initialContext)
    {
        var instance = new RunTestCasePipeline(initialContext);
        foreach (var step in steps)
        {
            instance.AddStep(step);
        }

        return instance;
    }

    public override async Task<RunTestCaseContext> RunAsync(
        RunTestCaseContext? initialContext = default,
        CancellationToken cancellationToken = default)
    {
        if (initialContext is not null) { _initialContext = initialContext; }

        var currentContext = _initialContext;
        await Task.CompletedTask;
        return currentContext!;
    }
}
