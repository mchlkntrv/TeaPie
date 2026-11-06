namespace TeaPie.Templating;

internal static class FluidMessageFormats
{
    public const string ParseErrorPositionPattern = @"^(?<message>.*) at \((?<line>\d+):(?<column>\d+)\)";
}
