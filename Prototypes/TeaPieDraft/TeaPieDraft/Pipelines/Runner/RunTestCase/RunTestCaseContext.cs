using TeaPieDraft.Pipelines.Base;
using TeaPieDraft.ScriptHandling;

namespace TeaPieDraft.Pipelines.Runner.RunTestCase;
internal class RunTestCaseContext : IPipelineContext
{
    internal RunTestCaseContext() { }

    internal RunTestCaseContext(TestCaseExecution testCase)
    {
        PreRequests = testCase.PreRequests;
        PostResponses = testCase.PostResponses;
        RequestFile = testCase.RequestFile;
        Current = null;
        RequestExecuted = false;
    }

    internal ScriptExecution? Current { get; set; }
    internal List<ScriptExecution> PreRequests { get; set; } = [];
    internal RequestExecution RequestFile { get; set; } = new();
    internal List<ScriptExecution> PostResponses { get; set; } = [];
    internal bool RequestExecuted { get; set; } = false;
}
