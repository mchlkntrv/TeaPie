using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.StructureExplorer;

namespace TeaPieDraft.Pipelines;

internal class CollectionExplorationContext(string initialPath) : IPipelineContext
{
    public string Path { get; set; } = initialPath;
    public CollectionStructure? CollectionStructure { get; set; }
    public Dictionary<string, TestCaseStructure> TestCases { get; set; } = [];
}
