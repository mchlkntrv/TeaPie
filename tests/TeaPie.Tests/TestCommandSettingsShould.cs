using FluentAssertions;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using TeaPie.DotnetTool;

namespace TeaPie.Tests;

public class TestCommandSettingsShould
{
    [Theory]
    [InlineData(new[] { "test", "--dump-expanded", "./coll" }, null)]
    [InlineData(new[] { "test", "./coll", "--dump-expanded" }, null)]
    [InlineData(new[] { "test", "--dump-expanded-dir", "./out", "./coll" }, "./out")]
    [InlineData(new[] { "test", "./coll", "--dump-expanded-dir", "./out" }, "./out")]
    [InlineData(new[] { "test", "--dump-expanded", "--dump-expanded-dir", "./out", "./coll" }, "./out")]
    public void KeepThePositionalPathSeparateFromTheDumpOptions(string[] args, string? expectedDirectory)
    {
        var app = new CommandAppTester();
        app.Configure(config => config.AddCommand<SettingsCapturingCommand>("test"));

        var result = app.Run(args);

        result.ExitCode.Should().Be(0);
        var settings = result.Settings.Should().BeOfType<TestCommand.Settings>().Subject;
        settings.Path.Should().Be("./coll");
        settings.ShouldDumpExpanded.Should().BeTrue();
        settings.DumpExpandedDirectory.Should().Be(expectedDirectory);
    }

    [Theory]
    [InlineData(new[] { "test", "./coll", "--dump-expanded", "-q" }, false)]
    [InlineData(new[] { "test", "./coll", "--dump-expanded-dir", "./out", "-q" }, true)]
    public void RejectAConsoleDumpInQuietMode(string[] args, bool expectedValid)
    {
        var app = new CommandAppTester();
        app.Configure(config => config.AddCommand<SettingsCapturingCommand>("test"));

        var result = app.Run(args);

        if (expectedValid)
        {
            result.ExitCode.Should().Be(0);
        }
        else
        {
            result.ExitCode.Should().NotBe(0);
            result.Output.Should().Contain("--dump-expanded-dir");
        }
    }

    private sealed class SettingsCapturingCommand : Command<TestCommand.Settings>
    {
        public override int Execute(CommandContext context, TestCommand.Settings settings) => 0;
    }
}
