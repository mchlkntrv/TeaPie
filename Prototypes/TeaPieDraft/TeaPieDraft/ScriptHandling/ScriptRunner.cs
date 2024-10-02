using Microsoft.CodeAnalysis.Scripting;

namespace TeaPieDraft.ScriptHandling;
internal class ScriptRunner
{
    internal ScriptRunner() { }

    internal async Task RunScriptAsync(Script<object> script)
    {
        await script.RunAsync(
            globals: new Globals() { tp = Application.UserContext });
    }
}
