using TeaPieDraft.Pipelines.Base;

namespace TeaPieDraft.Pipelines.StructureExploration.Collection;

internal class ComputeTestCaseOrderStep : IPipelineStep<CollectionExplorationContext>
{
    public CollectionExplorationContext Execute(CollectionExplorationContext context)
    {
        context.TestCases =
            StructureExplorer.StructureExplorer.ComputeTestCaseOrder(context.CollectionStructure?.CollectionFolder);
        return context;
    }

    public async Task<CollectionExplorationContext> ExecuteAsync(
        CollectionExplorationContext context,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return Execute(context);
    }

}
