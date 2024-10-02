using TeaPieDraft.Pipelines.Base;

namespace TeaPieDraft.Pipelines.StructureExploration.Collection;

internal class PrintStructureStep : IPipelineStep<CollectionExplorationContext>
{
    public CollectionExplorationContext Execute(CollectionExplorationContext context)
    {
        context.CollectionStructure?.PrintOnConsole();
        return context;
    }

    public async Task<CollectionExplorationContext> ExecuteAsync(CollectionExplorationContext context, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return Execute(context);
    }
}
