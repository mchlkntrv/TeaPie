using TeaPieDraft.Pipelines.CollectionPipeline;
using TeaPieDraft.Pipelines.Runner.RunTestCase;
using TeaPieDraft.ScriptHandling;

namespace TeaPieDraft.Pipelines.Runner.RunCollection;
internal class RunCollectionContext : ICollectionPipelineContext<TestCaseExecution, RunTestCaseContext>
{
    public TestCaseExecution? Current { get; set; }
    public IEnumerable<TestCaseExecution> Values { get; set; } = [];

    public RunTestCaseContext? GetItemContext() => Current is null ? new() : new(Current);
}
