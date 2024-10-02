using Microsoft.Extensions.Logging;
using TeaPieDraft.ScriptHandling;
using TeaPieDraft.StructureExplorer;

namespace TeaPieDraft.Runner;
internal class TestCaseRunner
{
    private readonly ILogger _logger;
    private readonly ScriptCompiler _compiler;

    internal TestCaseRunner(ScriptCompiler compiler, ILogger logger)
    {
        _compiler = compiler;
        _logger = logger;
    }

    internal async Task Run(TestCaseStructure testCase)
    {
        if (testCase == null) throw new ArgumentNullException(nameof(testCase));

        if (testCase.PreRequests.Count > 0)
        {
            foreach (var script in testCase.PreRequests)
            {
                if (script.Path is null) throw new ArgumentNullException(nameof(testCase.Path));

                // var content = await ScriptPreProcessor.GetScriptContentAsync(script.Path);
                // var processedScript = ScriptPreProcessor.PrepareScript(content);
                // var compiled = _compiler.CompileScript(processedScript);
            }
        }

        await Task.CompletedTask;
    }
}
