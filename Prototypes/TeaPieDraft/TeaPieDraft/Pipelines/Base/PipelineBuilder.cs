namespace TeaPieDraft.Pipelines.Base;

internal class PipelineBuilder<PipelineType, ContextType>
    where ContextType : IPipelineContext
    where PipelineType : IPipeline<ContextType>, new()
{
    private PipelineType? _pipeline;
    private ContextType? _pipelineContext;

    internal static PipelineBuilder<PipelineType, ContextType> Create(PipelineType pipeline)
        => new(pipeline);

    internal static PipelineBuilder<PipelineType, ContextType> Create(PipelineType pipeline, ContextType? pipelineContext)
        => new(pipeline, pipelineContext);

    private PipelineBuilder(PipelineType pipeline)
    {
        _pipeline = pipeline;
    }

    private PipelineBuilder(PipelineType pipeline, ContextType? pipelineContext)
    {
        _pipeline = pipeline;

        if (pipelineContext is not null)
        {
            _pipelineContext = pipelineContext;
            _pipeline?.SetContext(_pipelineContext);
        }
    }

    internal async Task<ContextType> RunAsync(CancellationToken cancellationToken = default)
    {
        if (_pipelineContext is null)
        {
            throw new ArgumentNullException("Pipeline context");
        }

        _pipelineContext = await _pipeline.RunAsync(_pipelineContext, cancellationToken);
        return _pipelineContext;
    }

    internal PipelineBuilder<PipelineType, ContextType> WithContext(ContextType pipelineContext)
    {
        _pipelineContext = pipelineContext;
        _pipeline?.SetContext(pipelineContext);
        return this;
    }

    internal PipelineBuilder<PipelineType, ContextType> AddStep(IPipelineStep<ContextType> step)
    {
        _pipeline?.AddStep(step);
        return this;
    }

    internal PipelineBuilder<PipelineType, ContextType> WithSteps(IEnumerable<IPipelineStep<ContextType>> steps)
    {
        foreach (var step in steps)
        {
            _pipeline?.AddStep(step);
        }
        return this;
    }

    internal PipelineType Build()
    {
        if (_pipeline is not null)
        {
            return _pipeline;
        }
        else
        {
            var instance = new PipelineType();
            instance.SetContext(_pipelineContext!);
            _pipeline = instance;
        }

        return _pipeline;
    }
}
