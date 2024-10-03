namespace TeaPieDraft.Pipelines.Base;
internal abstract class PipelineBase<ContextType> : IPipeline<ContextType>
    where ContextType : IPipelineContext
{
    protected readonly List<IPipelineStep<ContextType>> _pipelineSteps = [];

    internal PipelineBase() { }

    public virtual void AddStep(IPipelineStep<ContextType> step) => _pipelineSteps.Add(step);

    public void AddStep(Func<ContextType, Task<ContextType>> lambdaFunction)
    {
        AddStep(new InlineStep<ContextType>(lambdaFunction));
    }

    public virtual async Task<ContextType> RunAsync(
        ContextType context,
        CancellationToken cancellationToken = default)
    {
        ContextType input, result = context;
        foreach (var step in _pipelineSteps)
        {
            input = result;
            result = await step.ExecuteAsync(input, cancellationToken);
        }

        return result;
    }

    public virtual void RemoveStep(IPipelineStep<ContextType> step) => _pipelineSteps.Remove(step);
    public virtual void Clear() => _pipelineSteps.Clear();
}
