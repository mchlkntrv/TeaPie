using System.Collections;
using TeaPieDraft.Helpers;

namespace TeaPieDraft.StructureExplorer;

internal class CollectionStructure : Structure, IEnumerable<INode<FolderNode>>
{
    public FolderNode? CollectionFolder { get; set; }

    public IEnumerator<INode<FolderNode>> GetEnumerator()
        => new CollectionStructureEnumerator(CollectionFolder);

    internal void PrintOnConsole()
    {
        if (CollectionFolder is not null)
        {
            ToStringHelper.PrintFolderTree(CollectionFolder, string.Empty);
        }
        else
        {
            Console.WriteLine("Empty collection structure.");
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal class CollectionStructureEnumerator : IEnumerator<TestCaseStructure>
{
    private readonly FolderNode _root;
    private readonly Stack<IEnumerator<INode<FolderNode>>> _stack;
    private TestCaseStructure _current;

    public CollectionStructureEnumerator(FolderNode root)
    {
        _root = root;
        _stack = new Stack<IEnumerator<INode<FolderNode>>>();
        _stack.Push(_root.Children.GetEnumerator());
    }

    public TestCaseStructure Current => _current;

    object IEnumerator.Current => Current;

    public void Dispose() => throw new NotImplementedException();

    public bool MoveNext()
    {
        // NOT WORKING AS EXPECTED!!!
        while (_stack.Count > 0)
        {
            var enumerator = _stack.Peek();

            if (enumerator.MoveNext())
            {
                var node = enumerator.Current;

                // If the node is a FolderNode, first push its FolderChildren onto the stack
                if (node is FolderNode folder)
                {
                    // Then push the enumerator for the folder's folders
                    _stack.Push(folder.FoldersChildren.GetEnumerator());

                    // Push the enumerator for the folder's test cases first
                    _stack.Push(folder.TestCasesChildren.GetEnumerator());
                }
                // If the node is a TestCaseStructure, return it
                else if (node is TestCaseStructure testCase)
                {
                    _current = testCase;
                    return true;
                }
            }
            else
            {
                // If the enumerator is done, pop it off the stack
                _stack.Pop();
            }
        }

        return false; // No more nodes to visit
    }

    public void Reset()
    {
        _stack.Clear();
        _stack.Push(_root.Children.GetEnumerator());
    }
}
