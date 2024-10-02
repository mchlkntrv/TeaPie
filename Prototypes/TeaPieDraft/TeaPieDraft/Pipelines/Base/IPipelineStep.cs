namespace TeaPieDraft.Pipelines.Base;
internal interface IPipelineStep<PipelineContextType> where PipelineContextType : IPipelineContext
{
    public Task<PipelineContextType> ExecuteAsync(PipelineContextType context, CancellationToken cancellationToken = default);
}
