namespace TeaPieDraft.Pipelines.Base;
internal abstract class PipelineBase<ContextType> : IPipeline<ContextType> where ContextType : IPipelineContext
{
    protected readonly List<IPipelineStep<ContextType>> _pipelineSteps = [];

    protected ContextType? _initialContext;

    internal PipelineBase() { }

    internal PipelineBase(ContextType? initialContext)
    {
        _initialContext = initialContext;
    }

    public virtual void SetContext(ContextType context) { _initialContext = context; }

    public virtual void AddParallelSteps(params IPipelineStep<ContextType>[] steps) => throw new NotImplementedException();
    public virtual void AddStep(IPipelineStep<ContextType> step) => _pipelineSteps.Add(step);

    public void AddStep(Func<ContextType, Task<ContextType>> lambdaFunction)
    {
        AddStep(new InlineStep<ContextType>(lambdaFunction));
    }

    public virtual async Task<ContextType> RunAsync(
        ContextType? initialContext = default,
        CancellationToken cancellationToken = default)
    {
        ResolveInput(initialContext);

        ContextType input, result = _initialContext!;
        foreach (var step in _pipelineSteps)
        {
            input = result;
            result = await step.ExecuteAsync(input, cancellationToken);
        }

        return result;
    }

    protected void ResolveInput(ContextType? initialContext)
    {
        if (initialContext is null)
        {
            if (_initialContext is null)
            {
                throw new ArgumentNullException("Pipeline Context");
            }
        }
        else
        {
            _initialContext = initialContext;
        }
    }
    public virtual void RemoveStep(IPipelineStep<ContextType> step) => _pipelineSteps.Remove(step);
    public virtual void Clear() => _pipelineSteps.Clear();
}
