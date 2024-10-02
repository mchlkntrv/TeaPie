using TeaPieDraft.Pipelines.Base;

namespace TeaPieDraft.Pipelines.StructureExploration.TestCase;

internal class TestCaseStructureExplorationPipeline : PipelineBase<TestCaseExplorationContext>
{
    public TestCaseStructureExplorationPipeline(TestCaseExplorationContext initialContext) : base(initialContext)
    {
    }
}
