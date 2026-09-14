using System.Text.RegularExpressions;
using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

internal static partial class TemplatingTestHelpers
{
    public static TemplateExpander CreateExpander(global::TeaPie.Variables.IVariables? variables = null)
    {
        var vars = variables ?? new global::TeaPie.Variables.Variables();
        return new TemplateExpander(
            new LoopBlockScanner(),
            new LoopBodyMasker(),
            new CollectionSourceResolver(vars),
            new VariablesFluidModelBuilder(),
            vars);
    }

    /// <summary>
    /// Extracts the "(line:column)" position Fluid embeds in a parse-error message, so tests can
    /// assert on it independently of the surrounding wording.
    /// </summary>
    public static (int Line, int Column) ExtractReportedPosition(string message)
    {
        var match = PositionRegex().Match(message);
        match.Success.Should().BeTrue($"expected message to contain a '(line:column)' position: '{message}'");
        return (int.Parse(match.Groups["line"].Value), int.Parse(match.Groups["column"].Value));
    }

    /// <summary>
    /// Computes the 1-based (line, column) of the start of <paramref name="substring"/>'s first
    /// occurrence in <paramref name="content"/>, using the same original text the user authored -
    /// this is the independent "expected" oracle the fixed error message is compared against.
    /// </summary>
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
