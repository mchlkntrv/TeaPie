using System.Text.RegularExpressions;

namespace TeaPie.Templating;

internal sealed partial class LoopBodyMasker : ILoopBodyMasker
{
    public string Mask(string body, string loopVariableName)
        => TokenRegex().Replace(body, match =>
        {
            var inner = match.Groups[1].Value.Trim();
            return BelongsToLoopScope(inner, loopVariableName)
                ? match.Value
                : $"{{% raw %}}{match.Value}{{% endraw %}}";
        });

    private static bool BelongsToLoopScope(string expression, string loopVariableName)
        => StartsWithIdentifier(expression, loopVariableName) || StartsWithIdentifier(expression, "forloop");

    private static bool StartsWithIdentifier(string expression, string identifier)
    {
        if (!expression.StartsWith(identifier, StringComparison.Ordinal))
        {
            return false;
        }

        return expression.Length == identifier.Length || expression[identifier.Length] is '.' or ' ' or '|' or '\t';
    }

    // Matches {{ ... }}, treating "..." (double-quoted) spans as opaque so that literal
    // {{ / }} characters inside a quoted filter argument (see the prepend/append dynamic-naming
    // pattern in the spec) do not get mistaken for the token's own delimiters.
    [GeneratedRegex("\\{\\{((?:\"[^\"]*\"|[^{}\"])*)\\}\\}")]
    private static partial Regex TokenRegex();
}
