using Spectre.Console;
using TeaPie.DotnetTool;
using Spectre.Console.Testing;
using System.Reflection;

namespace TeaPie.Tests;

public class ConsoleLogoShould
{
    private static readonly string[] _noArgs = [];
    private static readonly string[] _noLogoArg = ["--no-logo"];

    [Fact]
    public void BePrintedOut()
    {
        var testConsole = new TestConsole();
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = testConsole;

        var expectedOutput = CreateExpectedOutput();

        try
        {
            var programType = typeof(Program).GetTypeInfo();
            var entryPoint = programType.Assembly.EntryPoint ?? throw new InvalidOperationException("Entry point not found");
            entryPoint.Invoke(null, [_noArgs]);

            var output = testConsole.Output;
            Assert.Contains(expectedOutput, output);
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    [Fact]
    public void NotBePrintedOut()
    {
        var testConsole = new TestConsole();
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = testConsole;

        var expectedOutput = CreateExpectedOutput();

        try
        {
            var programType = typeof(Program).GetTypeInfo();
            var entryPoint = programType.Assembly.EntryPoint ?? throw new InvalidOperationException("Entry point not found");
            entryPoint.Invoke(null, [_noLogoArg]);

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
        var expectedTable = Displayer.RenderTable();
        var expectedConsole = new TestConsole();
        expectedConsole.Write(expectedTable);
        return expectedConsole.Output;
    }
}
