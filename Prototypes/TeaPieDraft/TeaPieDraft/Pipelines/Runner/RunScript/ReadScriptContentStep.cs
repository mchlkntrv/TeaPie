using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.ScriptHandling;

namespace TeaPieDraft.Pipelines.Runner.RunScript;
internal class ReadScriptContentStep : IPipelineStep<RunScriptContext>
{
    public async Task<RunScriptContext> ExecuteAsync(RunScriptContext context, CancellationToken cancellationToken = default)
    {
        var structure = context?.Structure;
        var path = structure?.Path;

        if (structure is null) throw new ArgumentNullException("Script's structure");
        if (path is null) throw new ArgumentNullException("Path to current script");

        context!.RawContent = await FileReader.GetFileContentAsync(path);

        return context;
    }
}
