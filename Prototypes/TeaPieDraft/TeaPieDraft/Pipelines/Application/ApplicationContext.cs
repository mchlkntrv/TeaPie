using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.Pipelines.Runner.RunCollection;

namespace TeaPieDraft.Pipelines.Application;
internal class ApplicationContext : IPipelineContext
{
    public ApplicationContext(string path)
    {
        Path = path;
        ExplorationContext = new CollectionExplorationContext(path);
        RunningContext = new RunCollectionContext();
    }

    public string Path { get; set; }
    public CollectionExplorationContext ExplorationContext { get; set; }
    public RunCollectionContext RunningContext { get; set; }
}
