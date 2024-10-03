namespace TeaPieDraft.Pipelines.Base;

internal class InlineStep<ContextType> : IPipelineStep<ContextType>
    where ContextType : IPipelineContext
{
    private readonly Func<ContextType, Task<ContextType>> _lambdaFunction;

    internal InlineStep(Func<ContextType, Task<ContextType>> lambdaFunction)
    {
        _lambdaFunction = lambdaFunction;
    }

    public async Task<ContextType> ExecuteAsync(ContextType context, CancellationToken cancellationToken = default)
        => await _lambdaFunction.Invoke(context);
}
