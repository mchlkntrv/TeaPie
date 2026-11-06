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

    [Theory]
    [InlineData("GET.http", null, "GET.http")]
    [InlineData("Seed.tp", "Seed", "Seed.tp.http")]
    [InlineData("Seed.tp", "First", "Seed.tp.First.http")]
    [InlineData("Seed.tp", "Create user/admin", "Seed.tp.Create user_admin.http")]
    public void WriteTheDumpNamedAfterTheRequestsFileAndTheTestCaseOfATpFile(
        string fileName, string? tpTestCaseName, string expectedDumpFileName)
    {
        var testCase = CreateTestCase(fileName, tpTestCaseName);

        CreateDumper().Dump(CreateApplicationContext(_dumpDirectory), testCase, ExpandedContent);

        var expectedDumpFilePath = Path.Combine(
            _dumpDirectory, Path.GetDirectoryName(testCase.RequestsFile.RelativePath)!, expectedDumpFileName);
        Directory.GetFiles(_dumpDirectory, "*", SearchOption.AllDirectories).Should().Equal(expectedDumpFilePath);
        System.IO.File.ReadAllText(expectedDumpFilePath).Should().Be(ExpandedContent);
        _console.Output.Should().BeEmpty();
    }

    [Fact]
    public void WriteTheExpandedContentToTheConsoleWhenNoDumpDirectoryIsGiven()
    {
        var testCase = CreateTestCase("Seed.tp", "First");

        CreateDumper().Dump(CreateApplicationContext(string.Empty), testCase, ExpandedContent);

        _console.Output.Should().Contain("Seed.tp (First) ").And.Contain("GET https://example.test/expanded");
        Directory.Exists(_dumpDirectory).Should().BeFalse();
    }

    // Both the check before the run and the dump itself must refuse a dump inside the tested collection.
    [Theory]
    [InlineData("", "", true)]
    [InlineData("Demo", "", true)]
    [InlineData("Demo", "Demo/out", true)]
    [InlineData("Demo", "out", false)]
    [InlineData("Demo", null, false)]
    public void RefuseToWriteADumpInsideTheTestedCollection(
        string collectionSubPath, string? dumpSubPath, bool expectedRefusal)
    {
        var testCase = CreateTestCase();
        var collectionPath = Path.Combine(_dumpDirectory, collectionSubPath);
        Directory.CreateDirectory(collectionPath);
        var structure = new CollectionStructure();
        structure.TryAddTestCase(testCase);
        var appContext = new ApplicationContextBuilder()
            .WithPath(collectionPath)
            .WithExpandedContentDump(dumpSubPath is null ? string.Empty : Path.Combine(_dumpDirectory, dumpSubPath))
            .Build();
        appContext.CollectionStructure = structure;
        var dumpFilesBefore = Directory.GetFiles(_dumpDirectory, "*", SearchOption.AllDirectories);

        var checkBeforeTheRun = () => CreateDumper().EnsureDumpsAreOutsideOfTestedCollection(appContext);
        var dump = () => CreateDumper().Dump(appContext, testCase, ExpandedContent);

        if (expectedRefusal)
        {
            checkBeforeTheRun.Should().Throw<InvalidOperationException>().WithMessage($"*{collectionPath}*outside*");
            dump.Should().Throw<InvalidOperationException>().WithMessage($"*{collectionPath}*outside*");
            Directory.GetFiles(_dumpDirectory, "*", SearchOption.AllDirectories).Should().Equal(dumpFilesBefore);
        }
        else
        {
            checkBeforeTheRun.Should().NotThrow();
            dump.Should().NotThrow();
        }
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

    private static TestCase CreateTestCase(string fileName, string? tpTestCaseName)
    {
        var file = RequestHelper.GetFile(Path.Combine(RequestsIndex.RootFolderFullPath, fileName));

        return tpTestCaseName is null
            ? new TestCase(file)
            : new TestCase(file) { Name = tpTestCaseName, IsFromTpFile = true };
    }

    private static ApplicationContext CreateApplicationContext(string dumpPath)
        => new ApplicationContextBuilder()
            .WithPath(RequestsIndex.RootFolderFullPath)
            .WithExpandedContentDump(dumpPath)
            .Build();
}
