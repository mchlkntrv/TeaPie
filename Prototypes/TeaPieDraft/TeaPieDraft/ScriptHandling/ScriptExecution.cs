using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using TeaPieDraft.StructureExplorer;

namespace TeaPieDraft.ScriptHandling;
internal class TestCaseExecution
{
    internal TestCaseExecution(TestCaseStructure structure)
    {
        PreRequests = [.. structure.PreRequests.Select(x => new ScriptExecution() { Structure = x })];
        RequestFile = new RequestExecution() { Structure = structure.RequestFile };
        PostResponses = [.. structure.PostResponses.Select(x => new ScriptExecution() { Structure = x })];
        Current = null;
    }

    internal ScriptExecution? Current { get; set; }
    internal List<ScriptExecution> PreRequests { get; set; } = [];
    internal RequestExecution RequestFile { get; set; } = new();
    internal List<ScriptExecution> PostResponses { get; set; } = [];
}

internal class ScriptExecution
{
    internal ScriptExecution() { }

    internal ScriptExecution(Structure structure)
    {
        Structure = structure;
    }

    internal Structure? Structure { get; set; }
    internal string? RawContent { get; set; }
    internal string? ProcessedContent { get; set; }
    internal Script<object>? Script { get; set; }
    internal Compilation? Compilation { get; set; }
}

internal class RequestExecution
{
    internal Structure? Structure { get; set; }
    internal string? RawContent { get; set; }
    internal string? ProcessedContent { get; set; }
}
