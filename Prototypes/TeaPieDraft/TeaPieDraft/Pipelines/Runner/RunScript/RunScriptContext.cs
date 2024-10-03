using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.ScriptHandling;

namespace TeaPieDraft.Pipelines.Runner.RunScript;
internal class RunScriptContext : ScriptExecution, IPipelineContext
{
    internal RunScriptContext() { }
    internal RunScriptContext(ScriptExecution scriptExecution)
    {
        Structure = scriptExecution.Structure;
        RawContent = scriptExecution.RawContent;
        ProcessedContent = scriptExecution.ProcessedContent;
        Script = scriptExecution.Script;
        Compilation = scriptExecution.Compilation;
    }
}
