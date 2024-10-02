namespace TeaPieDraft.StructureExplorer;

internal class TestCaseStructure : Structure, INode<FolderNode>
{
    public FolderNode? Parent { get; set; }
    public List<Structure> UserDefinedScripts { get; set; } = [];
    public List<Structure> PreRequests { get; set; } = [];
    public Structure RequestFile { get; set; } = new();
    public List<Structure> PostResponses { get; set; } = [];

    public override string ToString()
        => $"{Name}\n\tPre-Request Scripts: {PreRequests.Count}\n\tPost-Response Scripts: {PostResponses.Count}";
}
