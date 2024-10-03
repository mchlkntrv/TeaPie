using TeaPieDraft.Pipelines.Application;
using TeaPieDraft.Pipelines.Base;

namespace TeaPieDraft.Pipelines.StructureExploration.Collection;

internal class CollectionStructureExplorationPipeline
    : PipelineBase<CollectionExplorationContext>,
    IPipelineStep<ApplicationContext>
{
    public CollectionStructureExplorationPipeline() : base() { }

    internal static CollectionStructureExplorationPipeline CreateDefault()
    {
        var instance = new CollectionStructureExplorationPipeline();
        instance.AddStep(new ExploreCollectionStep());
        instance.AddStep(new ComputeTestCaseOrderStep());
        instance.AddStep(new PrintTestCaseOrderStep());
        return instance;
    }

    public async Task<ApplicationContext> ExecuteAsync(
        ApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        context.ExplorationContext ??= new CollectionExplorationContext(context.Path);
        context.ExplorationContext = await RunAsync(context.ExplorationContext, cancellationToken);
        return context;
    }
}
