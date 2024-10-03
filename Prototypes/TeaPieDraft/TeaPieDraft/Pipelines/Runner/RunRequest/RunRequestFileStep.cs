using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.Pipelines.Runner.RunTestCase;

namespace TeaPieDraft.Pipelines.Runner.RunRequest;
internal class RunRequestFileStep : IPipelineStep<RunTestCaseContext>
{
    public async Task<RunTestCaseContext> ExecuteAsync(RunTestCaseContext context, CancellationToken cancellationToken = default)
    {
        context.RequestExecuted = true;
        await Task.CompletedTask;
        return context;
    }
}
