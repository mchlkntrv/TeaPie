using TeaPieDraft.Pipelines.Base;

namespace TeaPieDraft.Pipelines.CollectionPipeline;

internal interface ICollectionPipeline<PipelineContextType, StepContext>
    where PipelineContextType : IPipelineContext
    where StepContext : IPipelineContext
{
    internal void SetContext(PipelineContextType context);
    internal Task<PipelineContextType> RunAsync(PipelineContextType initialContext, CancellationToken cancellationToken = default);
    internal void AddStep(IPipelineStep<StepContext> step);
    internal void AddParallelSteps(params IPipelineStep<StepContext>[] steps);
    internal void RemoveStep(IPipelineStep<StepContext> step);
    internal void Clear();
}
