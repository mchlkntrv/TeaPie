using TeaPieDraft.Pipelines.CollectionPipeline;
using TeaPieDraft.Pipelines.Runner.RunScript;
using TeaPieDraft.ScriptHandling;

namespace TeaPieDraft.Pipelines.Runner.RunScriptsCollection;
internal class RunScriptsCollectionContext : ICollectionPipelineContext<ScriptExecution, RunScriptContext>
{
    public ScriptExecution? Current { get; set; }
    public IEnumerable<ScriptExecution> Values { get; set; } = [];

    public RunScriptContext? GetItemContext() => Current is null ? null : new(Current);
}
