using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.ScriptHandling;

namespace TeaPieDraft.Pipelines.Runner.RunScript;
internal class ReadScriptContentStep : BaseStep<RunScriptContext>
{
    public override async Task<RunScriptContext> ExecuteAsync(RunScriptContext context, CancellationToken cancellationToken = default)
    {
        await base.ExecuteAsync(context, cancellationToken);

        var structure = context?.Structure;
        var path = structure?.Path;

        if (structure is null) throw new ArgumentNullException("Script's structure is null.");
        if (path is null) throw new ArgumentNullException("Path to current script is null.");

        context!.RawContent = await FileReader.GetFileContentAsync(path);

        return context;
    }
}
