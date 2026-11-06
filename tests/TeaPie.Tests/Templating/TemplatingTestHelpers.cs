using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

internal static partial class TemplatingTestHelpers
{
    public static TemplateExpander CreateExpander(
        global::TeaPie.Variables.IVariables? variables = null, TemplatingLimits? limits = null,
        ILogger<TemplateExpander>? logger = null)
    {
        var vars = variables ?? new global::TeaPie.Variables.Variables();
        return new TemplateExpander(
            new LoopBlockScanner(),
            new LoopBodyMasker(),
            new CollectionSourceResolver(vars),
            new VariablesFluidModelBuilder(),
            vars,
            limits ?? new TemplatingLimits(),
            logger ?? NullLogger<TemplateExpander>.Instance);
    }

    // Expands each run such as "x100" into 100 'x' characters (likewise for 'y'), so that test data with long values
    // stays readable.
    public static string ExpandRuns(string pattern)
        => RunRegex().Replace(pattern, match => new string(match.Groups[1].Value[0], int.Parse(match.Groups[2].Value)));

    [GeneratedRegex("([xy])([0-9]+)")]
    private static partial Regex RunRegex();
}

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception)));
}
