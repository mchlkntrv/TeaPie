namespace TeaPieDraft.Pipelines.Base;
internal abstract class BaseStep<ContextType> : IPipelineStep<ContextType>
    where ContextType : IPipelineContext
{
    public virtual async Task<ContextType> ExecuteAsync(ContextType context, CancellationToken cancellationToken = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        await Task.CompletedTask;
        return context;
    }
}
