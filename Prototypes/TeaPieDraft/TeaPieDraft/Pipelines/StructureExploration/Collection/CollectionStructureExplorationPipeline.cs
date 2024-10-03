using TeaPieDraft.Pipelines.Application;
using TeaPieDraft.Pipelines.Base;

namespace TeaPieDraft.Pipelines.StructureExploration.Collection;

internal class CollectionStructureExplorationPipeline
    : PipelineBase<CollectionExplorationContext>,
    IPipelineStep<ApplicationContext>
{
    public CollectionStructureExplorationPipeline() : base()
    {
    }
    protected CollectionStructureExplorationPipeline(CollectionExplorationContext initialContext) : base(initialContext)
    {
    }

    internal static CollectionStructureExplorationPipeline CreateDefault(CollectionExplorationContext initialContext)
    {
        var instance = new CollectionStructureExplorationPipeline(initialContext);
        instance.AddStep(new ExploreCollectionStep());
        instance.AddStep(new ComputeTestCaseOrderStep());
        instance.AddStep(new PrintTestCaseOrderStep());
        return instance;
    }

    internal static CollectionStructureExplorationPipeline CreateDefault(ApplicationContext initialContext)
        => CreateDefault(initialContext.ExplorationContext);

    internal static CollectionStructureExplorationPipeline Create(
        IEnumerable<IPipelineStep<CollectionExplorationContext>> steps,
        CollectionExplorationContext initialContext)
    {
        var instance = new CollectionStructureExplorationPipeline(initialContext);
        foreach (var step in steps)
        {
            instance.AddStep(step);
        }

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
