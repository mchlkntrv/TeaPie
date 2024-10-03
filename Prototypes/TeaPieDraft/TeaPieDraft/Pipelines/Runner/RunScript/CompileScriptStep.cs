using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.ScriptHandling;

namespace TeaPieDraft.Pipelines.Runner.RunScript;
internal class CompileScriptStep : IPipelineStep<RunScriptContext>
{
    private readonly ScriptCompiler _compiler;
    internal CompileScriptStep()
    {
        _compiler = new ScriptCompiler();
    }

    internal CompileScriptStep(ScriptCompiler compiler)
    {
        _compiler = compiler;
    }

    public async Task<RunScriptContext> ExecuteAsync(RunScriptContext context, CancellationToken cancellationToken = default)
    {
        if (context?.ProcessedContent is null) throw new ArgumentNullException("Content of the script is null.");

        var compiled = _compiler.CompileScript(context.ProcessedContent);
        context.Script = compiled.Item1;
        context.Compilation = compiled.Item2;

        await Task.CompletedTask;

        return context;
    }
}
