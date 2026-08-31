using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

internal static class TemplatingTestHelpers
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
}
