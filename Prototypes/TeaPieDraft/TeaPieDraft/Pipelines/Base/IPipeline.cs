namespace TeaPieDraft.Pipelines.Base;
internal interface IPipeline<PipelineContextType> where PipelineContextType : IPipelineContext
{
    internal Task<PipelineContextType> RunAsync(
        PipelineContextType initialContext,
        CancellationToken cancellationToken = default);
    internal void AddStep(IPipelineStep<PipelineContextType> step);
    internal void RemoveStep(IPipelineStep<PipelineContextType> step);
    internal void Clear();
}
