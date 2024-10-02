namespace TeaPieDraft.Pipelines.Base;

internal class InlineStep<ContextType> : BaseStep<ContextType>
    where ContextType : IPipelineContext
{
    private readonly Func<ContextType, Task<ContextType>> _lambdaFunction;

    internal InlineStep(Func<ContextType, Task<ContextType>> lambdaFunction)
    {
        _lambdaFunction = lambdaFunction;
    }

    public override async Task<ContextType> ExecuteAsync(ContextType context, CancellationToken cancellationToken = default)
    {
        await base.ExecuteAsync(context, cancellationToken);
        return await _lambdaFunction.Invoke(context);
    }
}
