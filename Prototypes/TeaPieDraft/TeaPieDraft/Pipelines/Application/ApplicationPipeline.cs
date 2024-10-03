using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.Pipelines.Runner.RunCollection;
using TeaPieDraft.Pipelines.StructureExploration.Collection;

namespace TeaPieDraft.Pipelines.Application;
internal class ApplicationPipeline : PipelineBase<ApplicationContext>
{
    internal ApplicationPipeline(ApplicationContext context) : base(context) { }

    internal static ApplicationPipeline CreateDefault(ApplicationContext initialContext)
    {
        var instance = new ApplicationPipeline(initialContext);
        instance.AddStep(CollectionStructureExplorationPipeline.CreateDefault(initialContext));
        instance.AddStep(new PrepareExecutionContextStep());
        instance.AddStep(RunCollectionPipeline.CreateDefault(initialContext));
        return instance;
    }

}
