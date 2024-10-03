using TeaPieDraft.Pipelines.Base;

namespace TeaPieDraft.Pipelines.CollectionPipeline;
internal abstract class CollectionPipelineBase<CollectionContextType, StepContextType, ItemType>
    : ICollectionPipeline<CollectionContextType, StepContextType>
        where CollectionContextType : ICollectionPipelineContext<ItemType, StepContextType>
        where StepContextType : IPipelineContext
{
    protected readonly List<IPipelineStep<StepContextType>> _pipelineSteps = [];

    protected CollectionContextType? _initialContext;

    internal CollectionPipelineBase() { }

    internal CollectionPipelineBase(CollectionContextType? initialContext)
    {
        _initialContext = initialContext;
    }

    public virtual void SetContext(CollectionContextType context) { _initialContext = context; }
    public virtual void AddStep(IPipelineStep<StepContextType> step) => _pipelineSteps.Add(step);

    public void AddStep(Func<StepContextType, Task<StepContextType>> lambdaFunction)
    {
        AddStep(new InlineStep<StepContextType>(lambdaFunction));
    }

    public void AddParallelSteps(params IPipelineStep<StepContextType>[] steps) => throw new NotImplementedException();

    public virtual async Task<CollectionContextType> RunAsync(
        CollectionContextType? initialContext = default,
        CancellationToken cancellationToken = default)
    {
        ResolveInput(initialContext);

        StepContextType? contextForStep;
        foreach (var current in _initialContext!.Values)
        {
            _initialContext.Current = current;

            foreach (var step in _pipelineSteps)
            {
                contextForStep = _initialContext.GetItemContext();
                if (contextForStep is not null)
                {
                    await step.ExecuteAsync(contextForStep, cancellationToken);
                }
                else
                {
                    throw new ArgumentNullException("Context for this step can not be null.");
                }
            }
        }

        return _initialContext;
    }

    protected void ResolveInput(CollectionContextType? initialContext)
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
    public virtual void RemoveStep(IPipelineStep<StepContextType> step) => _pipelineSteps.Remove(step);
    public virtual void Clear() => _pipelineSteps.Clear();
}
