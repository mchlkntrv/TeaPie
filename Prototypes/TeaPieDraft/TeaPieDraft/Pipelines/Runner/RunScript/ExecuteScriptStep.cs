using Microsoft.Extensions.Logging;
using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.ScriptHandling;
using Constants = TeaPieDraft.Parsing.ParsingConstants;

namespace TeaPieDraft.Pipelines.Runner.RunScript;
internal class ExecuteScriptStep : BaseStep<RunScriptContext>
{
    private readonly ScriptRunner _runner;
    internal ExecuteScriptStep(ScriptRunner scriptRunner)
    {
        _runner = scriptRunner;
    }

    internal ExecuteScriptStep() { _runner = new(); }

    public override async Task<RunScriptContext> ExecuteAsync(RunScriptContext context, CancellationToken cancellationToken = default)
    {
        await base.ExecuteAsync(context, cancellationToken);

        var compilation = context?.Compilation;
        var script = context?.Script;
        var logger = Application.UserContext?.Logger;

        if (compilation is null) throw new ArgumentNullException("Compilation of the current script is null.");
        if (script is null) throw new ArgumentNullException("Script object is null.");

        try
        {
            logger?.LogInformation("Running {scriptType} '{relativePath}'",
                GetTypeOfScript(context),
                context?.Structure?.RelativePath);

            var state = await script.RunAsync(
                globals: new Globals() { tp = Application.UserContext },
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            logger?.LogError("Error occured while execution of the script '{relativePath}'", context?.Structure?.RelativePath);
            logger?.LogError("Original exception: {exceptionMessage}", ex.ToString());
        }

        return context!;
    }

    private string GetTypeOfScript(ScriptExecution? script)
    {
        var path = script?.Structure?.Path;
        if (!string.IsNullOrEmpty(path))
        {
            if (path.EndsWith($"{Constants.PreRequestSuffix}{Constants.ScriptFileExtension}"))
            {
                return "Pre-Request script";
            }
            else if (path.EndsWith($"{Constants.PostResponseSuffix}{Constants.ScriptFileExtension}"))
            {
                return "Post-Response script";
            }
            else
            {
                return "User-defined script";
            }
        }
        else
        {
            return "Unknown script";
        }
    }
}
