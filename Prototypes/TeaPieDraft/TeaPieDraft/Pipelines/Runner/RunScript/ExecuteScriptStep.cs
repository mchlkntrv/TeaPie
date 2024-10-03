using Microsoft.Extensions.Logging;
using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.ScriptHandling;
using Constants = TeaPieDraft.Parsing.ParsingConstants;

namespace TeaPieDraft.Pipelines.Runner.RunScript;
internal class ExecuteScriptStep : IPipelineStep<RunScriptContext>
{
    public async Task<RunScriptContext> ExecuteAsync(RunScriptContext context, CancellationToken cancellationToken = default)
    {
        var compilation = context?.Compilation;
        var script = context?.Script;
        var logger = TeaPieDraft.Application.UserContext?.Logger;

        if (compilation is null) throw new ArgumentNullException("Compilation of the current script is null.");
        if (script is null) throw new ArgumentNullException("Script object is null.");

        try
        {
            logger?.LogInformation("Running {scriptType} '{relativePath}'",
                GetTypeOfScript(context),
                context?.Structure?.RelativePath);

            var state = await script.RunAsync(
                globals: new Globals() { tp = TeaPieDraft.Application.UserContext },
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
