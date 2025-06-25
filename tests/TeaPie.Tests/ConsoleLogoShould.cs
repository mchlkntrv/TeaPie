using Spectre.Console;
using TeaPie.DotnetTool;
using Spectre.Console.Testing;

namespace TeaPie.Tests;

public class ConsoleLogoShould
{
    [Fact]
    public async Task BePrintedOut()
    {
        var testConsole = new TestConsole();
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = testConsole;

        var expectedOutput = CreateExpectedOutput();

        try
        {
            await Program.Main([]);

            var output = testConsole.Output;

            Assert.Contains(expectedOutput, output);
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    [Fact]
    public async Task NotBePrintedOut()
    {
        var testConsole = new TestConsole();
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = testConsole;

        var expectedOutput = CreateExpectedOutput();

        try
        {
            await Program.Main(["--no-logo"]);
            var output = testConsole.Output;

            Assert.DoesNotContain(expectedOutput, output);
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private static string CreateExpectedOutput()
    {
        var assembly = typeof(Displayer).Assembly;
        const string resourceName = "TeaPie.DotnetTool.Assets.Fonts.small.flf";
        FigletFont font;
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                throw new Exception($"Resource '{resourceName}' not found. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
            }

            font = FigletFont.Load(stream);
        }

        var version = assembly.GetName().Version;
        var versionText = version is not null
            ? string.Join('.', version.Major, version.Minor, version.Build)
            : string.Empty;

        var expectedTable = new Table
        {
            Border = TableBorder.None
        };

        var figletTea = new FigletText(font, "Tea");
        var figletPie = new FigletText(font, "Pie");
        var figletVersion = new FigletText(font, versionText);

        expectedTable.AddColumn(new TableColumn(figletTea).Width(21));
        expectedTable.AddColumn(new TableColumn(figletPie).Width(17));
        expectedTable.AddColumn(new TableColumn(figletVersion));
        expectedTable.Collapse();

        var expectedConsole = new TestConsole();
        expectedConsole.Write(expectedTable);
        return expectedConsole.Output;
    }
}
