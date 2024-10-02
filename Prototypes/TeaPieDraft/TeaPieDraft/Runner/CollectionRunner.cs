using Microsoft.Extensions.Logging;
using TeaPieDraft.Pipelines.Runner.RunCollection;
using TeaPieDraft.Pipelines.Runner.RunScriptsCollection;
using TeaPieDraft.ScriptHandling;
using TeaPieDraft.StructureExplorer;

namespace TeaPieDraft.Runner;
internal class CollectionRunner
{
    private readonly ILogger _logger;
    private readonly ScriptCompiler _compiler;

    internal CollectionRunner(ScriptCompiler compiler, ILogger logger)
    {
        _logger = logger;
        _compiler = compiler;
    }

    internal async Task Run(Dictionary<string, TestCaseStructure> testCaseOrder)
    {
        var runContext = new RunCollectionContext();
        runContext.Values = testCaseOrder.Values.Select(x => new TestCaseExecution(x));

        foreach (var testCase in runContext.Values)
        {
            runContext.Current = testCase;

            foreach (var preRequest in testCase.PreRequests)
            {
                runContext.Current.Current = preRequest;
                var preReqScriptsContext = new RunScriptsCollectionContext()
                {
                    Current = runContext.Current.Current,
                    Values = testCase.PreRequests
                };

                var scriptsPipeline = RunScriptCollectionPipeline.CreateDefault(preReqScriptsContext);
                await scriptsPipeline.RunAsync();
            }
        }
    }
}
