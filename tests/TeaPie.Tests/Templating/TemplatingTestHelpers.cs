using System.Text.RegularExpressions;
using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

internal static partial class TemplatingTestHelpers
{
    public static TemplateExpander CreateExpander(
        global::TeaPie.Variables.IVariables? variables = null, TemplatingLimits? limits = null)
    {
        var vars = variables ?? new global::TeaPie.Variables.Variables();
        return new TemplateExpander(
            new LoopBlockScanner(),
            new LoopBodyMasker(),
            new CollectionSourceResolver(vars),
            new VariablesFluidModelBuilder(),
            vars,
            limits ?? new TemplatingLimits());
    }

    public static (int Line, int Column) ExtractReportedPosition(string message)
    {
        var match = PositionRegex().Match(message);
        match.Success.Should().BeTrue($"expected message to contain a '(line:column)' position: '{message}'");
        return (int.Parse(match.Groups["line"].Value), int.Parse(match.Groups["column"].Value));
    }

    public static (int Line, int Column) FindOriginalPosition(string content, string substring)
    {
        var index = content.IndexOf(substring, StringComparison.Ordinal);
        index.Should().BeGreaterOrEqualTo(0, $"'{substring}' should be present in the original content");

        var line = 1;
        var lineStart = 0;
        for (var i = 0; i < index; i++)
        {
            if (content[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (line, index - lineStart + 1);
    }

    [GeneratedRegex(@"at \((?<line>\d+):(?<column>\d+)\)")]
    private static partial Regex PositionRegex();
}
