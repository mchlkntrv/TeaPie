using TeaPieDraft.Pipelines.Base;

namespace TeaPieDraft.Pipelines.StructureExploration.TestCase;

internal class ExploreTestCaseStep : IPipelineStep<TestCaseExplorationContext>
{
    public async Task<TestCaseExplorationContext> ExecuteAsync(TestCaseExplorationContext context, CancellationToken cancellationToken = default)
    {
        if (context == null) throw new ArgumentNullException("context");

        var structure = StructureExplorer.StructureExplorer.ExploreTestCase(context.Path);
        context.TestCaseStructure = structure;

        await Task.CompletedTask;
        return context;
    }
}
