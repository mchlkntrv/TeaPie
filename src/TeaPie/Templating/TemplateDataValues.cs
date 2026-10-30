using System.Text.RegularExpressions;

namespace TeaPie.Templating;

// Templates are rendered before the file is split into requests and before TeaPie resolves its variables.
// A rendered value that would change that structure is therefore written as a reference to itself, which TeaPie
// resolves inside the already parsed request, just as it resolves any '{{variable}}' in a file without templates.
internal sealed partial class TemplateDataValues
{
    public const string ReferencePrefix = "__teapie_value_";

    public static readonly IReadOnlyDictionary<string, string> None = new Dictionary<string, string>();

    private readonly Dictionary<string, string> _valuesByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _namesByValue = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> ValuesByName => _valuesByName;

    public string Protect(string rendered, bool bracesWrittenInTemplate)
    {
        var value = ReferenceRegex().Replace(
            rendered, match => _valuesByName.TryGetValue(match.Groups[1].Value, out var original) ? original : match.Value);

        return ChangesFileStructure(value, bracesWrittenInTemplate) ? GetReference(value) : rendered;
    }

    private string GetReference(string value)
    {
        if (!_namesByValue.TryGetValue(value, out var name))
        {
            name = $"{ReferencePrefix}{_valuesByName.Count + 1}";
            _namesByValue[value] = name;
            _valuesByName[name] = value;
        }

        return $"{{{{{name}}}}}";
    }

    private static bool ChangesFileStructure(string value, bool bracesWrittenInTemplate)
        => value.Contains('\n')
            || value.Contains('\r')
            || value.Contains("###", StringComparison.Ordinal)
            || (!bracesWrittenInTemplate
                && (value.Contains("{{", StringComparison.Ordinal) || value.Contains("}}", StringComparison.Ordinal)));

    [GeneratedRegex(@"\{\{(" + ReferencePrefix + @"\d+)\}\}", RegexOptions.None, TemplatingLimits.RegexTimeoutMilliseconds)]
    private static partial Regex ReferenceRegex();
}
