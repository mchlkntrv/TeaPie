namespace TeaPieDraft.StructureExplorer;

internal class FolderNode : Structure, INode<FolderNode>
{
    public FolderNode? Parent { get; set; }
    public List<FolderNode> FoldersChildren { get; set; } = [];
    public List<TestCaseStructure> TestCasesChildren { get; set; } = [];
    public List<INode<FolderNode>> Children { get; set; } = [];

    public void AddChild(INode<FolderNode> child)
    {
        if (child is FolderNode folderChild)
        {
            FoldersChildren.Add(folderChild);
            FoldersChildren = [.. FoldersChildren.OrderBy(x => x.Name)];
        }
        else if (child is TestCaseStructure testCaseChild)
        {
            TestCasesChildren.Add(testCaseChild);
            TestCasesChildren = [.. TestCasesChildren.OrderBy(x => x.Name)];
        }

        child.Parent = this;
        Children.Add(child);
        Children = [.. Children.OrderBy(x => ((Structure)x).Name)];
    }
}
