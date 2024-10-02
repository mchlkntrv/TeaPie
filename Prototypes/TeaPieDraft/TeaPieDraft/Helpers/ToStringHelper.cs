using TeaPieDraft.StructureExplorer;

namespace TeaPieDraft.Helpers;
internal class ToStringHelper
{
    public static void PrintFolderTree(FolderNode node, string indent)
    {
        Console.WriteLine($"{indent}{node.Name}");

        foreach (var child in node.FoldersChildren)
        {
            foreach (var testCase in child.TestCasesChildren)
            {
                Console.WriteLine($"{indent}{testCase.Name}");
            }

            PrintFolderTree(child, indent + "-");
        }
    }
}
