using Microsoft.Extensions.Logging;
using Spectre.Console;
using TeaPie.StructureExploration;

namespace TeaPie.Templating;

internal sealed class ExpandedContentDumper(IAnsiConsole console) : IExpandedContentDumper
{
    public void Dump(ApplicationContext context, TestCase testCase, string expandedContent)
    {
        var relativePath = testCase.RequestsFile.RelativePath;

        if (string.IsNullOrEmpty(context.ExpandedContentDumpPath))
        {
            console.Write(new Rule($"[grey]Expanded: {relativePath.EscapeMarkup()}[/]").LeftJustified());
            console.Write(new Text(expandedContent));
            console.WriteLine();
            return;
        }

        var dumpFilePath = Path.Combine(context.ExpandedContentDumpPath, ToRequestFilePath(relativePath));
        EnsureOutsideOfTestedCollection(context.Path, dumpFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(dumpFilePath)!);
        System.IO.File.WriteAllText(dumpFilePath, expandedContent);

        context.Logger.LogInformation(
            "Expanded content of the requests file at '{Path}' was written to '{DumpFilePath}'.",
            relativePath,
            dumpFilePath);
    }

    private static void EnsureOutsideOfTestedCollection(string testedPath, string dumpFilePath)
    {
        var collectionPath = Directory.Exists(testedPath) ? testedPath : Path.GetDirectoryName(testedPath)!;
        var collectionPrefix = Path.GetFullPath(collectionPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (Path.GetFullPath(dumpFilePath).StartsWith(collectionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Expanded content cannot be written to '{dumpFilePath}', because it is inside the tested collection " +
                $"'{collectionPath}' (it could overwrite a requests file or be run as an extra test case). Choose a " +
                "dump directory outside of the collection.");
        }
    }

    private static string ToRequestFilePath(string relativePath)
        => Path.GetExtension(relativePath).Equals(Constants.RequestFileExtension, StringComparison.OrdinalIgnoreCase)
            ? relativePath
            : relativePath + Constants.RequestFileExtension;
}
