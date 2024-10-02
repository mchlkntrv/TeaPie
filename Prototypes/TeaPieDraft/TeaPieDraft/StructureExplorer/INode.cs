namespace TeaPieDraft.StructureExplorer;

internal interface INode<ParentType>
{
    public ParentType? Parent { get; set; }
}
