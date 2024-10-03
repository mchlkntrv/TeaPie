using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.Pipelines.Runner.RunCollection;
using TeaPieDraft.Pipelines.StructureExploration.Collection;

namespace TeaPieDraft.Pipelines.Application;
internal class ApplicationPipeline : PipelineBase<ApplicationContext>
{
    internal ApplicationPipeline() { }

    internal static ApplicationPipeline CreateDefault()
    {
        var instance = new ApplicationPipeline();
        instance.AddStep(CollectionStructureExplorationPipeline.CreateDefault());
        instance.AddStep(new PrepareExecutionContextStep());
        instance.AddStep(RunCollectionPipeline.CreateDefault());
        return instance;
    }

}
