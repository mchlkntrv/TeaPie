using FluentAssertions;
using Spectre.Console.Testing;
using TeaPie.StructureExploration;
using TeaPie.Templating;
using TeaPie.Tests.Http;

namespace TeaPie.Tests.Templating;

public class ExpandedContentDumperShould : IDisposable
{
    private const string ExpandedContent = "### Expanded\nGET https://example.test/expanded\n";

    private readonly string _dumpDirectory =
        Path.Combine(Path.GetTempPath(), "teapie-dump-tests", Guid.NewGuid().ToString("N"));

    private readonly TestConsole _console = new();

    [Fact]
    public void WriteTheExpandedContentToTheDumpDirectoryKeepingTheRelativePath()
    {
        var testCase = CreateTestCase();

        CreateDumper().Dump(CreateApplicationContext(_dumpDirectory), testCase, ExpandedContent);

        var dumpFilePath = Path.Combine(_dumpDirectory, testCase.RequestsFile.RelativePath);
        System.IO.File.ReadAllText(dumpFilePath).Should().Be(ExpandedContent);
        _console.Output.Should().BeEmpty();
    }

    [Fact]
    public void WriteTheExpandedContentToTheConsoleWhenNoDumpDirectoryIsGiven()
    {
        var testCase = CreateTestCase();

        CreateDumper().Dump(CreateApplicationContext(string.Empty), testCase, ExpandedContent);

        _console.Output.Should().Contain(Path.GetFileName(testCase.RequestsFile.RelativePath))
            .And.Contain("GET https://example.test/expanded");
        Directory.Exists(_dumpDirectory).Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefuseToWriteADumpInsideTheTestedCollection(bool dumpIntoCollectionParent)
    {
        var testCase = CreateTestCase();
        var relativePath = testCase.RequestsFile.RelativePath;
        var collectionPath = dumpIntoCollectionParent
            ? Path.Combine(_dumpDirectory, relativePath.Split('/', '\\')[0])
            : _dumpDirectory;
        Directory.CreateDirectory(collectionPath);
        var appContext = new ApplicationContextBuilder()
            .WithPath(collectionPath)
            .WithExpandedContentDump(_dumpDirectory)
            .Build();

        var act = () => CreateDumper().Dump(appContext, testCase, ExpandedContent);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{collectionPath}*outside*");
        System.IO.File.Exists(Path.Combine(_dumpDirectory, relativePath)).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_dumpDirectory))
        {
            Directory.Delete(_dumpDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private ExpandedContentDumper CreateDumper() => new(_console);

    private static TestCase CreateTestCase()
        => RequestHelper.PrepareTestCaseContext(RequestsIndex.RequestWithFullStructure, false).TestCase;

    private static ApplicationContext CreateApplicationContext(string dumpPath)
        => new ApplicationContextBuilder()
            .WithPath(RequestsIndex.RootFolderFullPath)
            .WithExpandedContentDump(dumpPath)
            .Build();
}
