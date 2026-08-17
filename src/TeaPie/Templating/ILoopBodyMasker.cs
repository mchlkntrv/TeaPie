namespace TeaPie.Templating;

internal interface ILoopBodyMasker
{
    string Mask(string body, string loopVariableName);
}
