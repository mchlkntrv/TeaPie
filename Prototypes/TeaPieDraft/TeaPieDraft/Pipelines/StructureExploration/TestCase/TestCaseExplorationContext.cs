using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.StructureExplorer;

namespace TeaPieDraft.Pipelines;
internal class TestCaseExplorationContext(string initialPath) : IPipelineContext
{
    public string Path { get; set; } = initialPath;
    public TestCaseStructure? TestCaseStructure { get; set; }
}
