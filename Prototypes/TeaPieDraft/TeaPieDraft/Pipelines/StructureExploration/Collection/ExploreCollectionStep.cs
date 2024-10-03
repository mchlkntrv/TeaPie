using Microsoft.Extensions.Logging;
using TeaPieDraft.Pipelines.Base;

namespace TeaPieDraft.Pipelines.StructureExploration.Collection;

internal class ExploreCollectionStep : IPipelineStep<CollectionExplorationContext>
{
    public async Task<CollectionExplorationContext> ExecuteAsync(CollectionExplorationContext context, CancellationToken cancellationToken = default)
    {
        if (context == null) throw new ArgumentNullException("context");

        var logger = TeaPieDraft.Application.UserContext!.Logger;
        logger.LogInformation("Structure of the collection is going to be explored.");
        var explorer = new StructureExplorer.StructureExplorer();
        var structure = StructureExplorer.StructureExplorer.ExploreCollectionAsync(context.Path);
        context.CollectionStructure = structure;

        await Task.CompletedTask;
        return context;
    }
}
