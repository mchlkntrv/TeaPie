using TeaPieDraft.Pipelines.Base;

namespace TeaPieDraft.Pipelines.Application;
internal class PrepareExecutionContextStep : IPipelineStep<ApplicationContext>
{
    public async Task<ApplicationContext> ExecuteAsync(ApplicationContext context, CancellationToken cancellationToken = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        context.RunningContext.Values = context.ExplorationContext.TestCases.Values
            .Select(x => new ScriptHandling.TestCaseExecution(x));

        context.RunningContext.Current = context.RunningContext.Values.First();
        await Task.CompletedTask;
        return context;
    }
}
