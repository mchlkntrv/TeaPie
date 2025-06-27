using Spectre.Console;
using TeaPie.DotnetTool;
using Spectre.Console.Testing;
using System.Reflection;

namespace TeaPie.Tests;

public class ConsoleLogoShould
{
    [Theory]
    [InlineData(new string[0], true)]
    [InlineData(new string[] { "--no-logo" }, false)]
    public void BePrintedBasedOnArgs(string[] args, bool shouldPrintLogo)
    {
        var (testConsole, originalConsole) = UseTestConsole();

        var expectedOutput = CreateExpectedOutput();

        try
        {
            var programType = typeof(Program).GetTypeInfo();
            var entryPoint = programType.Assembly.EntryPoint ?? throw new InvalidOperationException("Entry point not found");
            entryPoint.Invoke(null, [args]);

            var output = testConsole.Output;
            if (shouldPrintLogo)
            {
                Assert.Contains(expectedOutput, output);
            }
            else
            {
                Assert.DoesNotContain(expectedOutput, output);
            }
        }
        finally
        {
            RestoreConsole(originalConsole);
        }
    }

    private static string CreateExpectedOutput()
    {
        var (testConsole, originalConsole) = UseTestConsole();

        try
        {
            Displayer.DisplayApplicationHeader();
            return testConsole.Output;
        }
        finally
        {
            RestoreConsole(originalConsole);
        }
    }

    private static (TestConsole testConsole, IAnsiConsole originalConsole) UseTestConsole()
    {
        var testConsole = new TestConsole();
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = testConsole;
        return (testConsole, originalConsole);
    }

    private static void RestoreConsole(IAnsiConsole originalConsole)
    {
        AnsiConsole.Console = originalConsole;
    }
}
