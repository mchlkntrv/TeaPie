using Consts = TeaPieDraft.Parsing.ParsingConstants;

namespace TeaPieDraft.StructureExplorer;

internal class StructureExplorer
{
    internal static CollectionStructure ExploreCollectionAsync(string collectionFolderPath)
    {
        if (string.IsNullOrEmpty(collectionFolderPath) || !Directory.Exists(collectionFolderPath))
        {
            throw new ArgumentException("The provided folder path is invalid or the file does not exist.");
        }

        var folderName = Path.GetFileName(collectionFolderPath.TrimEnd(Path.DirectorySeparatorChar));

        FolderNode currentNode = new()
        {
            RelativePath = folderName,
            Path = collectionFolderPath,
            Name = folderName,
            Parent = null,
        };

        CollectionStructure structure = new()
        {
            RelativePath = folderName,
            Path = collectionFolderPath,
            Name = folderName,
            CollectionFolder = currentNode
        };

        Stack<FolderNode> traversalPath = new();
        traversalPath.Push(currentNode);

        FolderNode childNode;
        Stack<string> subFolders = [];
        string[] files = [];
        var currentFolderPath = collectionFolderPath;
        string? folderPath;

        while (traversalPath.Count != 0)
        {
            // Update of current node
            currentNode = traversalPath.Pop();
            folderPath = currentNode.Path;

            // Inspection of current node
            subFolders = new([.. Directory.GetDirectories(folderPath!)]);
            files = Directory.GetFiles(folderPath!);

            // Find all request files withing this folder
            var requestFiles = files.Where(x =>
                x.EndsWith($"{Consts.RequestSuffix}{Consts.RequestFileExtension}") ||
                x.EndsWith($"{Consts.RequestFileExtension}"));

            // Explore test cases
            if (requestFiles.Any())
            {
                ExploreTestCases(requestFiles, currentNode);
            }

            while (subFolders.Count > 0)
            {
                currentFolderPath = subFolders.Pop();
                var name = Path.GetFileName(currentFolderPath.TrimEnd(Path.DirectorySeparatorChar));
                childNode = new()
                {
                    RelativePath = currentNode.RelativePath + "\\" + name,
                    Path = currentFolderPath,
                    Name = name,
                    Parent = currentNode,
                };

                currentNode.AddChild(childNode);
                traversalPath.Push(childNode);
            }
        }

        return structure;
    }
    internal static Dictionary<string, TestCaseStructure> ComputeTestCaseOrder(FolderNode? root)
    {
        var order = new Dictionary<string, TestCaseStructure>();
        TraverseFolder(root, order);
        return order;
    }

    private static void TraverseFolder(FolderNode? folder, Dictionary<string, TestCaseStructure> testCases)
    {
        if (folder is null)
        {
            return;
        }

        foreach (var childFolder in folder.FoldersChildren)
        {
            TraverseFolder(childFolder, testCases);
        }

        foreach (var testCase in folder.TestCasesChildren)
        {
            testCases.Add(testCase.Path!, testCase);
        }
    }

    private static void ExploreTestCases(
        IEnumerable<string> requestFiles,
        FolderNode currentNode)
    {
        var testCases = new List<TestCaseStructure>();

        TestCaseStructure testCaseStructure;
        foreach (var file in requestFiles)
        {
            testCaseStructure = ExploreTestCase(file, currentNode);
            testCases.Add(testCaseStructure);
        }

        foreach (var testCase in testCases)
        {
            currentNode.AddChild(testCase);
        }
    }

    internal static TestCaseStructure ExploreTestCase(string testCaseRequestFile)
        => ExploreTestCase(testCaseRequestFile, null);

    internal static TestCaseStructure ExploreTestCase(string testCaseRequestFile, FolderNode? parentNode)
    {
        // Validate the input path
        if (string.IsNullOrEmpty(testCaseRequestFile) || !File.Exists(testCaseRequestFile))
        {
            throw new ArgumentException("The provided file path is invalid or the file does not exist.");
        }

        var name = Path.GetFileName(testCaseRequestFile);
        TestCaseStructure structure = new()
        {
            Path = testCaseRequestFile,
            Name = name
        };

        if (parentNode is not null)
        {
            structure.RelativePath = parentNode?.RelativePath + "\\" + name;
        }

        FileInfo fileInfo = new(testCaseRequestFile);
        var directory = fileInfo.Directory ?? throw new ArgumentException("File's directory can not be null.");

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(testCaseRequestFile);
        var fileNameWithoutExtensionAndSuffix = fileNameWithoutExtension;
        if (fileNameWithoutExtension.EndsWith(Consts.RequestSuffix))
        {
            fileNameWithoutExtensionAndSuffix = fileNameWithoutExtension[..^Consts.RequestSuffix.Length];
        }

        // Get all files in the directory
        var files = Directory.GetFiles(directory.FullName);

        var userDefinedScripts = files.Where(x =>
            x.EndsWith($"{Consts.ScriptFileExtension}") &&
            !x.EndsWith($"{fileNameWithoutExtensionAndSuffix}{Consts.PreRequestSuffix}{Consts.ScriptFileExtension}") &&
            !x.EndsWith($"{fileNameWithoutExtensionAndSuffix}{Consts.PostResponseSuffix}{Consts.ScriptFileExtension}"));

        var preRequest = files.Where(x =>
            x.EndsWith($"{fileNameWithoutExtensionAndSuffix}{Consts.PreRequestSuffix}{Consts.ScriptFileExtension}")).FirstOrDefault();

        var postResponse = files.Where(x =>
            x.EndsWith($"{fileNameWithoutExtensionAndSuffix}{Consts.PostResponseSuffix}{Consts.ScriptFileExtension}")).FirstOrDefault();

        return PrepareTestCaseStructure(structure, userDefinedScripts, preRequest, testCaseRequestFile, postResponse);
    }

    private static TestCaseStructure PrepareTestCaseStructure(
        TestCaseStructure structure,
        IEnumerable<string> userDefinedScripts,
        string? preRequest,
        string testCaseRequestFile,
        string? postResponse)
    {
        structure.RequestFile = PrepareStructureAsync(structure, testCaseRequestFile);

        foreach (var script in userDefinedScripts)
        {
            if (!string.IsNullOrEmpty(script))
            {
                var scriptStructure = PrepareStructureAsync(structure, script);
                structure.UserDefinedScripts.Add(scriptStructure);
            }
        }

        if (!string.IsNullOrEmpty(preRequest))
        {
            var scriptStructure = PrepareStructureAsync(structure, preRequest);
            structure.PreRequests.Add(scriptStructure);
        }

        if (!string.IsNullOrEmpty(postResponse))
        {
            var scriptStructure = PrepareStructureAsync(structure, postResponse);
            structure.PostResponses.Add(scriptStructure);
        }

        return structure;
    }

    private static Structure PrepareStructureAsync(
        TestCaseStructure testCaseStructure,
        string filePath)
    {
        var dir = Path.GetDirectoryName(testCaseStructure.RelativePath);
        var name = Path.GetFileName(filePath);

        var fileStructure = new Structure()
        {
            RelativePath = dir + "\\" + name,
            Path = filePath,
            Name = name,
        };

        return fileStructure;
    }
}
