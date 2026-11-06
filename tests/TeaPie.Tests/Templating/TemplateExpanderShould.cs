using FluentAssertions;
using Microsoft.Extensions.Logging;
using static TeaPie.Tests.Templating.TemplatingTestHelpers;
using TeaPie.Json;
using TeaPie.Templating;
using TeaPieVariables = global::TeaPie.Variables.Variables;

namespace TeaPie.Tests.Templating;

public class TemplateExpanderShould
{
    private const string FilePath = "requests/seed.http";
    private const string ErrorPrefix = "Templating error in '" + FilePath + "': ";
    private const string RawHint =
        "If this file contains literal '{%' text that is not a TeaPie template tag, wrap it in '{% raw %}...{% endraw %}'.";

    private const string OneRequest = "### r\nGET https://example.test/{{ i }}\n";
    private const string OneRequestWithCrlf = "### r\r\nGET https://example.test/{{ i }}\r\n";
    private const string TenCharacters = "{% assign s = 'xxxxxxxxxx' %}";

    private const string FourLevelContent =
        "{% for region in Regions %}{% for country in region.Countries %}" +
        "{% for city in country.Cities %}{% for street in city.Streets %}" +
        "[{{ region.Name }}/{{ country.Name }}/{{ city.Name }}/{{ street }}]" +
        "{% endfor %}{% endfor %}{% endfor %}{% endfor %}";

    private const string NewCompaniesLoop =
        "{% for c in NewCompanies %}{% for u in c.Customers %}### Add\nPOST https://example.test/{{ u }}\n";

    private const string InnerLoopEmptyEverywhereWarning =
        "Loop over 'c.Customers' in 'requests/seed.http' produced zero items for every one of its 2 outer item(s), so " +
        "it generated no requests at all. Check that the collection is filled as expected.";

    private const string LoopWithoutRequestsWarningEnd =
        "' in 'requests/seed.http' generated no requests, although its body declares some (for example because " +
        "'limit:'/'offset:' or an '{% if %}' excluded every item). Check the loop's collection, options and conditions.";

    [Theory]
    [InlineData("POST {{ApiGatewayBaseUrl}}/companies\n\n{ \"name\": \"Acme\" }", false)]
    [InlineData("POST https://example.test\n\n{\"fmt\":\"{%Y-%m-%d}\"}", true)]
    [InlineData("{\"template\": \"Hello {% name %}!\"}", true)]
    [InlineData("{% comment %}note{% endcomment %}", true)]
    [InlineData("{% format %}{% iffy %}{% assigned %}{% raw_x %}", true)]
    public void ReturnAFileWithoutASupportedTagUnchanged(string content, bool warns)
    {
        var logger = new RecordingLogger<TemplateExpander>();

        var result = CreateExpander(CreateVariables(), logger: logger).Expand(content, FilePath).Content;

        result.Should().BeSameAs(content);
        logger.Entries.Should().HaveCount(warns ? 1 : 0);
        logger.Entries.Where(entry =>
                entry.Level == LogLevel.Warning && entry.Message.Contains(FilePath) && entry.Message.Contains("sent unchanged"))
            .Should().HaveCount(warns ? 1 : 0);
    }

    [Theory]
    [InlineData("{% if PartnerCount > 5 %}BIG{% else %}SMALL{% endif %}", "BIG")]
    [InlineData("{% unless SkipSeed %}SEED{% endunless %}", "SEED")]
    [InlineData("{% assign a = SecretIds %}{% assign b = ShadowedIds %}[{{ a }}{{ b }}]" +
        "{% if SecretIds or ShadowedIds %}YES{% endif %}", "[]")]
    [InlineData("{% assign s = Shared %}{{ s }}", "test-case")]
    [InlineData("{% assign label = 'seeded' %}{% for t in Tenants %}{{ label }}-{{ t.Name }};{% endfor %}",
        "seeded-Acme;seeded-Globex;")]
    [InlineData("{% raw %}{%Y-%m-%d}{% endraw %}", "{%Y-%m-%d}")]
    [InlineData("{% raw %}{% for i (1..2) %}{% endfor %}{% endraw %}", "{% for i (1..2) %}{% endfor %}")]
    [InlineData("{% comment %}{% for x in Xs %}{% endfor %}{% for in %}{% endcomment %}GET https://example.test",
        "GET https://example.test")]
    [InlineData("{% for i in (1..2) %}[{{ i }}{% raw %}{% endfor %}{% endraw %}]{% endfor %}",
        "[1{% endfor %}][2{% endfor %}]")]
    public void RenderTopLevelTags(string content, string expected)
        => Expand(content).Should().Be(expected);

    [Theory]
    [InlineData("{% for t in Tenants %}{{ forloop.index }}:{{ t.Name }};{% endfor %}", "1:Acme;2:Globex;")]
    [InlineData("{% for p in Temp.FreePartners %}{{ p }}{% endfor %}", "ab")]
    [InlineData("{% for i in (1..3) %}[{{ i }}]{% endfor %}", "[1][2][3]")]
    [InlineData("{% for v in (\"a,b\", 42, true, 1.5) %}{{ v }};{% endfor %}", "a,b;42;true;1.5;")]
    [InlineData("{% for s in (\"a|b\", \"c d\") reversed %}{{ s }};{% endfor %}", "c d;a|b;")]
    [InlineData("A\r\n\r\n{%- for i in (1..2) -%}\r\n[{{ i }}]\r\n{%- endfor -%}\r\n\r\nB", "A[1][2]B")]
    [InlineData("{% for c in Companies limit: 1 %}\n  {%- for l in c.Licenses -%}\n  [{{ l }}]\n  {%- endfor -%}\n" +
        "{% endfor %}", "[A1][A2][A3]")]
    [InlineData("{% for t in Tenants %}\r\n### {{ t.Name }}\r\nGET x\r\n{% endfor %}\r\n### Plain\r\nGET y\r\n",
        "\r\n### Acme\r\nGET x\r\n\r\n### Globex\r\nGET x\r\n\r\n### Plain\r\nGET y\r\n")]
    public void ExpandLoopsOverEverySupportedSource(string content, string expected)
        => Expand(content).Should().Be(expected);

    [Theory]
    [InlineData("{% for i in Items offset: 1 limit: 3 reversed %}{{ i }}{% endfor %}", "432")]
    [InlineData("{% for i in Items reversed offset:1 limit:3 %}{{ i }}{% endfor %}", "432")]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses limit: MaxLicenses %}{{ l }}{% endfor %}{% endfor %}",
        "A1")]
    public void ApplyLoopOptions(string content, string expected)
        => Expand(content).Should().Be(expected);

    [Theory]
    [InlineData("{% for t in Tenants %}{{ApiGatewayBaseUrl}}/{{ t.Name }};{% endfor %}",
        "{{ApiGatewayBaseUrl}}/Acme;{{ApiGatewayBaseUrl}}/Globex;")]
    [InlineData("{% for i in (1..1) %}{{$guid}}{{$randomInt 1 100}}{{$add {{X}} 1}}{{$add {{Y}} 1}}:{{ i }}{% endfor %}",
        "{{$guid}}{{$randomInt 1 100}}{{$add {{X}} 1}}{{$add {{Y}} 1}}:1")]
    [InlineData("{% for t in Tenants %}{{ t.Name }}={% raw %}{{ t.Name }}{{ApiGatewayBaseUrl}}{% endraw %};{% endfor %}",
        "Acme={{ t.Name }}{{ApiGatewayBaseUrl}};Globex={{ t.Name }}{{ApiGatewayBaseUrl}};")]
    [InlineData("{% for t in Tenants %}{{ \"p-\" | append: t.Name }}|{{- t.Name -}};{% endfor %}", "p-Acme|Acme;p-Globex|Globex;")]
    [InlineData("{% for i in (1..2) %}{{ Items[i] }}|{{ \"x\" | append: i }}|{{ i|plus: 1 }}|" +
        "{{ \"a\" | replace: \"a\", i }};{% endfor %}", "2|x1|2|1;3|x2|3|2;")]
    [InlineData("{% for t in Tenants %}{{ X.t }}{{Login.response.body.$..t}}{% endfor %}",
        "{{ X.t }}{{Login.response.body.$..t}}{{ X.t }}{{Login.response.body.$..t}}")]
    [InlineData("{% for t in Tenants %}{% assign greeting = \"Hi\" %}{{ greeting }}{{greetingX}};{% endfor %}",
        "Hi{{greetingX}};Hi{{greetingX}};")]
    [InlineData("{{ gatewayName }}{% assign gatewayName = \"gw\" %}{{ gatewayName }}", "{{ gatewayName }}gw")]
    [InlineData("{% comment %}{% assign ApiKey = \"dev\" %}{% endcomment %}{% for i in (1..1) %}X-Key: {{ApiKey}}" +
        "{% endfor %}", "X-Key: {{ApiKey}}")]
    [InlineData("{% comment %}Wrap text in {% raw %} blocks.{% endcomment %}{% for i in (1..1) %}{{ApiGatewayBaseUrl}}" +
        "{% raw %}lit{% endraw %}{% endfor %}", "{{ApiGatewayBaseUrl}}lit")]
    public void PreserveTeaPieSyntax(string content, string expected)
        => Expand(content).Should().Be(expected);

    [Theory]
    [InlineData("{% for t in Tenants %}{{% if t.Name == 'Acme' %}\"a\":1{% endif %}};{% endfor %}", "{\"a\":1};{};")]
    [InlineData("{% for t in Tenants %}{% assign g = Greeting %}{{ g }} {{ t.Name }};{% endfor %}", "Hi Acme;Hi Globex;")]
    [InlineData("{% for i in (1..1) %}{% if NoSuch %}1{% endif %}{% if MaybeNull %}2{% endif %}" +
        "{% if Temp.FreePartners %}3{% endif %}{% if my-flag %}4{% endif %}{% assign x = my-var %}{{ x }}{% endfor %}",
        "4hello")]
    [InlineData("{% for i in (1..2) %}{% if i > 1 %}A{% else %}B{% endif %}{% endfor %}", "BA")]
    [InlineData("{% for i in (1..2) %}{% assign x = i %}{% endfor %}[{{ x }}]{% if x == 2 %}!{% endif %}", "[2]!")]
    [InlineData("{% for i in (1..2) %}{% assign Greeting = i %}{% endfor %}[{{ Greeting }}]" +
        "{% if Greeting == 2 %}!{% endif %}", "[2]!")]
    public void ApplyConditionsAndAssignmentsInsideLoopBodies(string content, string expected)
        => Expand(content).Should().Be(expected);

    [Theory]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses %}[{{ c.Name }}:{{ l }}]{% endfor %}{% endfor %}",
        "[Acme:A1][Acme:A2][Acme:A3]")]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses %}{{ l }}{% else %}[{{ c.Name }}:none]{% endfor %}" +
        "{% endfor %}", "A1A2A3[Globex:none]")]
    [InlineData("{% for c in Companies %}{% for i in (1..1) %}{% for l in c.Licenses %}{{ l }}{% else %}-{% endfor %}" +
        "{% endfor %}{% endfor %}", "A1A2A3-")]
    [InlineData("{% for my-c in Companies %}{% assign my-n = my-c.Name %}{% for l in my-c.Licenses %}[{{ my-n }}:{{ l }}]" +
        "{% endfor %}{% endfor %}", "[Acme:A1][Acme:A2][Acme:A3]")]
    [InlineData("{% for c in Companies %}{% assign k = 'basic' %}{% for g in c.Groups[k] %}{{ g }}{% endfor %}" +
        "{% endfor %}", "ABGB")]
    [InlineData("{% for r in Records %}{{ r[NameKey] }}:{% for l in r[ListKey] %}{{ l }}{% endfor %}{% endfor %}",
        "Rec:R1R2")]
    [InlineData(FourLevelContent, "[EU/SK/BA/Main][EU/CZ/Praha/Narodni]")]
    [InlineData("{% for c in Customers %}[{{ c.name }}|{{ c.NAME }}|{{ c.address.city }}|" +
        "{% for i in c.ids %}{{ i }}{% if i == 1 %}={% endif %}{% endfor %}]{% endfor %}", "[A|A|BA|1=2][B|B||]")]
    public void ExpandNestedLoops(string content, string expected)
        => Expand(content).Should().Be(expected);

    // One row per message. An expected message without '*' is the exact text the user sees; '*' stands for the part
    // of a message that Fluid writes.
    [Theory]
    [InlineData("{% for a in (1..2) %}{% endfor %}\n{% endfor %}",
        ErrorPrefix + "found '{% endfor %}' without a matching '{% for %}'.")]
    [InlineData("{% for i in (1..99999999999) %}{{ i }}{% endfor %}",
        "*numeric range '(1..99999999999)' has a bound that is too large*")]
    [InlineData("{% for p in EmptyList %}{{ p }}{% else %}none{% endfor %}",
        ErrorPrefix + "loop over 'EmptyList' produced zero items.")]
    [InlineData("{% if Flag %}{% for p in Missing %}{{ p }}{% endfor %}{% endif %}",
        ErrorPrefix + "collection variable 'Missing' referenced in a '{% for %}' loop was not found.")]
    [InlineData("{% for c in AString %}{{ c }}{% endfor %}",
        ErrorPrefix + "variable 'AString' referenced in a '{% for %}' loop must be a collection.")]
    [InlineData("{% for m in MaybeNull %}{{ m }}{% endfor %}",
        ErrorPrefix + "variable 'MaybeNull' referenced in a '{% for %}' loop must be a collection, but it is null.")]
    [InlineData("{% for s in (\"a\",) %}{{ s }}{% endfor %}", "*inline collection*has an empty item*")]
    [InlineData("{% for s in ShadowedIds %}{{ s }}{% endfor %}",
        ErrorPrefix + "variable 'ShadowedIds' is tagged as 'secret' and cannot be used as a '{% for %}' loop source, " +
        "because its values would appear in the expanded requests file and in logs.")]
    [InlineData("{% for i in (1..1) %}{% for s in SecretIds %}{{ s }}{% endfor %}{% endfor %}",
        "*'SecretIds'*tagged as 'secret'*")]
    [InlineData("{% assign items = Root %}{% for item in items.Children %}{{ item }}{% endfor %}",
        "*collection variable 'items.Children'*was not found*")]
    [InlineData("{% for c in BadCompanies offset: 1 reversed %}{% for l in c.Licenses %}{{ l }}{% endfor %}{% endfor %}",
        ErrorPrefix + "variable 'c.Licenses' referenced in a '{% for %}' loop must be a collection (c[1]).")]
    [InlineData("{% for c in NullCompanies %}{% for l in c.Licenses %}{{ l }}{% endfor %}{% endfor %}",
        ErrorPrefix + "variable 'c.Licenses' referenced in a '{% for %}' loop must be a collection, but it is missing " +
        "or null (c[0]).")]
    [InlineData("{% for c in Companies %}\n### r\nGET https://example.test/{{ c.Nme }}\n{% endfor %}",
        ErrorPrefix + "'Nme' is undefined: the value has no property or field 'Nme' (names are case-sensitive). " +
        "First use at (3:26): {{ c.Nme }}")]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses | first %}{{ l }}{% endfor %}{% endfor %}",
        "*filters are not supported*'c.Licenses | first'*")]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses required %}{{ l }}{% endfor %}{% endfor %}",
        "*unsupported option 'required'*'limit:', 'offset:' and 'reversed'*")]
    [InlineData("{% for i in Items limit: 1 limit: 2 %}{{ i }}{% endfor %}", "*option 'limit:' is specified more than once*")]
    [InlineData("{% for i in Items limit: %}{{ i }}{% endfor %}", "*option 'limit:'*is missing a value*")]
    [InlineData("{% for i in (1..3) limit:-1 %}{{ i }}{% endfor %}",
        "*option 'limit:'*must be a whole number from 0 to 1000 or a variable, but is '-1'*")]
    [InlineData("{% assign n = -1 %}{% for i in (1..3) limit: n %}{{ i }}{% endfor %}",
        "*option 'limit:' of the '{% for i %}' loop must be a whole number from 0 to 1000, but is '-1'*")]
    [InlineData("{% for i in (1..3) offset:continue %}{{ i }}{% endfor %}", "*'offset: continue' is not supported*")]
    [InlineData("{% for i in (1..1) %}{{ i | divided_by: 0 }}{% endfor %}",
        ErrorPrefix + "rendering failed: Attempted to divide by zero.")]
    [InlineData("### r\n# @name r\nPOST https://example.com/r\n\nuse {% raw %} and no close {{ApiKey}} end\n",
        ErrorPrefix + "missing '{% endraw %}' for the '{% raw %}' at (5:5).")]
    [InlineData("{% assign a = 1 %}\n{% liquid echo a %}\n### r\nPOST https://example.com/after\n",
        ErrorPrefix + "the '{% liquid %}' tag at (2:1) is not supported. Write each statement as a separate tag " +
        "instead, for example '{% assign a = 1 %}'.")]
    [InlineData("{% assign Tenants = Tenants | where: \"Name\", \"Acme\" %}\n{% for t in Tenants %}### r\n" +
        "POST https://example.com/{{ t.Name }}\n{% endfor %}",
        ErrorPrefix + "the loop source 'Tenants' at (2:1) is reassigned with '{% assign Tenants %}', which is not " +
        "supported, because the loop would still read the TeaPie variable. Rename the assigned name, or prepare the " +
        "collection in the '-init.csx' script.")]
    [InlineData("{% for i in (1..2) %}A{% else %}B{% endfor %}",
        ErrorPrefix + "the loop over the numeric range '(1..2)' at (1:1) has an '{% else %}' branch, which is not " +
        "supported for a numeric range. Remove the '{% else %}' branch, or loop over a collection variable (e.g. set " +
        "in the '-init.csx' script) or an inline list such as (1, 2, 3).")]
    [InlineData("{% for i in (1..n) %}{{ i }}{% endfor %}",
        ErrorPrefix + "numeric range '(1..n)' is not supported: both bounds must be whole numbers from 0 to 2147483647 " +
        "written directly in the file, such as (1..5). A variable, an expression, a negative or a decimal number " +
        "cannot be a bound.")]
    [InlineData("{% for i in (\"a\", \"b\" %}{{ i }}{% endfor %}",
        ErrorPrefix + "inline collection '(\"a\", \"b\"' has unbalanced parentheses.")]
    [InlineData("GET x\r\n  {%- for i (1..2) -%}\r\n{% endfor %}",
        ErrorPrefix + "invalid '{% for %}' tag '{%- for i (1..2) -%}' at (2:3). Write it on one line as " +
        "'{% for <item> in <collection> %}', for example '{% for car in Cars %}'.")]
    [InlineData("{% assign d = 1 %}\n{\"fmt\":\"{%Y-%m-%d}\"}",
        ErrorPrefix + "failed to parse template: * at (2:12)\nSource:\n{\"fmt\":\"{%Y-%m-%d}\"}\n" + RawHint)]
    [InlineData("{% if true %}\nGET https://example.test\n",
        ErrorPrefix + "failed to parse template: * at (3:1)\nSource:\n(end of file)\n" + RawHint)]
    [InlineData("### Setup\nPOST {{ApiGatewayBaseUrl}}/init\n\n{% for t in Tenants %}\n{{ NotInScope }}\n{% badtag %}\n" +
        "{% endfor %}", ErrorPrefix + "failed to parse template: * at (6:1)\nSource:\n{% badtag %}\n" + RawHint)]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses %}{{ l }}{% endfor %}{% badtag %}{% endfor %}",
        ErrorPrefix + "failed to parse template: * at (1:69)\nSource:\n{% for c in Companies %}{% for l in c.Licenses %}" +
        "{{ l }}{% endfor %}{% badtag %}{% endfor %}\n" + RawHint)]
    [InlineData("{% assign a = 1 %}\n[\"y300\", \"{%Y\", \"y300\"]",
        ErrorPrefix + "failed to parse template: * at (2:310)\nSource:\n...y93\", \"{%Y\", \"y96...\n" + RawHint)]
    [InlineData("{% for i in Items x5000 %}",
        ErrorPrefix + "missing '{% endfor %}' for the loop starting with '{% for i in Items x82...'.")]
    [InlineData("{% assign a = 1 %}{% endfor x5000 %}",
        ErrorPrefix + "found a stray or malformed loop tag '{% endfor x90...' that is not part of a valid " +
        "'{% for %}' ... '{% endfor %}' block.")]
    [InlineData("{% for i in (x5000) %}{% endfor %}",
        ErrorPrefix + "literal 'x100...' inside an inline collection is not a valid quoted string, number, or boolean. " +
        "Use a double-quoted string (\"value\"), a number, or a lowercase boolean (true/false).")]
    public void ThrowAClearErrorForInvalidTemplatesOrData(string content, string expectedMessage)
    {
        var act = () => Expand(ExpandRuns(content));

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Match(ExpandRuns(expectedMessage));
    }

    [Fact]
    public void ThrowAClearErrorWhenScanningTheFileTimesOut()
    {
        // The '{% for %}' tag pattern is quadratic in the length of a line that never closes the tag.
        var content = "{% for i in (1..1) %}{{ i }}{% endfor %}\n{% for x in a" + new string(' ', 100_000);

        var act = () => Expand(content);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(ErrorPrefix + "scanning the file took longer than*");
    }

    // One row per limit, plus the ways around it that were decided on. 'null' stands for the default limits.
    [Theory]
    [InlineData(nameof(TemplatingLimits.MaxExpandedRequests), 3,
        "{% for i in (1..2) %}{% for j in (1..2) %}" + OneRequestWithCrlf + "{% endfor %}{% endfor %}",
        "loop over '(1..2)' would expand to more than 3 requests, counted across all its nesting levels.")]
    [InlineData(nameof(TemplatingLimits.MaxExpandedRequests), 3,
        "{% if true %}{% for i in (1..2) %}" + OneRequest + OneRequest + "{% endfor %}{% endif %}",
        "loop over '(1..2)' would expand to more than 3 requests*")]
    [InlineData(nameof(TemplatingLimits.MaxExpandedRequests), 3,
        "{% for i in (1..3) %}" + OneRequest + "{% endfor %}{% for i in (1..3) %}" + OneRequest + "{% endfor %}", null)]
    [InlineData(nameof(TemplatingLimits.MaxExpandedRequests), 3,
        "### r\nPOST https://example.test\n\n[{% for i in Items %}{{ i }},{% endfor %}0]\n", null)]
    [InlineData(nameof(TemplatingLimits.MaxExpandedRequests), 3,
        "{% for i in (1..2) %}{% if i == 2 %}{{ Missing.Value }}{% endif %}" +
        OneRequest + OneRequest + OneRequest + OneRequest + OneRequest + "{% endfor %}",
        "loop over '(1..2)' would expand to more than 3 requests*")]
    [InlineData(null, 0, "{% for i in (1..501) %}" + OneRequest + OneRequest + "{% endfor %}",
        "loop over '(1..501)' would expand to more than 1000 requests*")]
    [InlineData(nameof(TemplatingLimits.MaxRenderSteps), 100,
        "{% for i in (1..30) %}{{ i }}{% endfor %}mid{% for j in (1..30) %}{{ j }}{% endfor %}",
        "template exceeded the maximum of 100 rendering steps*")]
    [InlineData(nameof(TemplatingLimits.MaxNestingDepth), 3,
        "{% for a in (1..1) %}{% for b in (1..1) %}{% for c in (1..1) %}{% for d in (1..1) %}x" +
        "{% endfor %}{% endfor %}{% endfor %}{% endfor %}",
        "loops are nested 4 levels deep, exceeding the maximum of 3*")]
    [InlineData(nameof(TemplatingLimits.MaxLoopItems), 2, "{% for i in (1..3) limit: 1 %}{{ i }}{% endfor %}",
        "loop over '(1..3)' has 3 items, exceeding the maximum of 2 items per loop.")]
    [InlineData(nameof(TemplatingLimits.MaxLoopItems), 2,
        "{% for c in Companies %}{% for l in c.Licenses %}{{ l }}{% endfor %}{% endfor %}",
        "loop over 'c.Licenses' has more than 2 items (c[0]), exceeding the maximum of 2 items per loop.")]
    [InlineData(nameof(TemplatingLimits.MaxExpandedLength), 50,
        "{% for i in (1..3) %}0123456789{% endfor %}{% for j in (1..3) %}0123456789{% endfor %}",
        "the loops in the file would expand to more than 50 characters*")]
    [InlineData(nameof(TemplatingLimits.MaxExpandedLength), 50,
        TenCharacters + "{% assign s = s | append: s %}{% assign s = s | append: s %}{% assign s = s | append: s %}",
        "filter 'append' produced a text longer than 50 characters*")]
    [InlineData(nameof(TemplatingLimits.MaxListItems), 5,
        "{% assign a = 'x' | split: ',' %}{% for i in (1..3) %}{% assign a = a | concat: a %}{% endfor %}",
        "filter 'concat' produced a list with more than 5 items*")]
    [InlineData(nameof(TemplatingLimits.MaxExpandedLength), 50,
        TenCharacters + "{% for i in (1..3) %}{% capture s %}{{ s }}{{ s }}{% endcapture %}{% endfor %}",
        "'{% capture s %}' produced a text longer than 50 characters*")]
    public void EnforceEachLimit(string? limit, int value, string content, string? expectedMessage)
    {
        var act = () => Expand(content, CreateLimits(limit, value));

        if (expectedMessage is null)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<InvalidOperationException>().Which.Message.Should().Match(ErrorPrefix + expectedMessage);
        }
    }

    [Theory]
    [InlineData("bob\n###\nDELETE https://example.test/injected")]
    [InlineData("bob\rX-Admin: true")]
    [InlineData("{{apiKey}}")]
    public void ReplaceDataValuesThatWouldChangeTheFileWithAReference(string value)
    {
        var variables = CreateVariables();
        variables.SetVariable("Unsafe", new object[] { new { Name = value } });
        const string content = "{% for u in Unsafe %}\n###\nGET https://example.test/{{ u.Name }}/{{ u.Name | append: \"!\" }}\n" +
            "{% assign n = u.Name %}{{ n }}{% capture c %}{{ n }}{% endcapture %}{% assign m = c %}{{ m }}{% echo n %}" +
            "{% cycle n %}{{ n | raw }}{% endfor %}";

        var result = CreateExpander(variables).Expand(content, FilePath);

        result.Content.Should().Be(
            "\n###\nGET https://example.test/{{__teapie_value_1}}/{{__teapie_value_2}}\n" +
            "{{__teapie_value_1}}{{__teapie_value_1}}{{__teapie_value_1}}{{__teapie_value_1}}{{__teapie_value_1}}");
        result.DataValues.Should().Equal(
            new Dictionary<string, string> { ["__teapie_value_1"] = value, ["__teapie_value_2"] = value + "!" });
    }

    [Theory]
    [InlineData("{% for t in Tenants %}{{ forloop.index | prepend: \"{{Temp.CompanyId_\" | append: \"}}\" }}{% endfor %}",
        "{{Temp.CompanyId_1}}{{Temp.CompanyId_2}}")]
    [InlineData("{% for u in Unsafe %}{{ u.Note | json }}|{{ u.Note | url_encode }}|{{ u.Note | strip_newlines }}|" +
        "{% if u.Note contains 'line2' %}found{% endif %}{% endfor %}", "\"line1\\nline2\"|line1%0Aline2|line1line2|found")]
    public void KeepTemplateTextAndValuesThatCannotChangeTheFileAsTheyAre(string content, string expected)
    {
        var variables = CreateVariables();
        variables.SetVariable("Unsafe", new object[] { new { Note = "line1\nline2" } });

        var result = CreateExpander(variables).Expand(content, FilePath);

        result.Content.Should().Be(expected);
        result.DataValues.Should().BeEmpty();
    }

    [Theory]
    [InlineData("{% for i in (1..1) %}{% assign url = \"{{ApiGatewayBaseUrl}}\" %}{{ url }}{% endfor %}",
        "{{ApiGatewayBaseUrl}}")]
    [InlineData("{% for c in Secrets %}{{ c | json }}{% endfor %}", "{\"Name\":\"{{apiKey}}\"}")]
    public void ReferenceBracesThatAreNotWrittenInTheSameExpression(string content, string value)
    {
        var variables = CreateVariables();
        variables.SetVariable("Secrets", new object[] { new { Name = "{{apiKey}}" } });

        var result = CreateExpander(variables).Expand(content, FilePath);

        result.Content.Should().Be("{{__teapie_value_1}}");
        result.DataValues.Should().Equal(new Dictionary<string, string> { ["__teapie_value_1"] = value });
    }

    [Fact]
    public void KeepAssignsLocalToTheFileAndWarnOnceWhenOneHidesATeaPieVariable()
    {
        var variables = CreateVariables();
        var logger = new RecordingLogger<TemplateExpander>();
        const string content =
            "{% assign Label = 'free' %}{% assign Greeting = 'a' %}{% for c in Companies %}{% assign Greeting = c.Name %}" +
            "{% for l in c.Licenses %}{% assign inner = l %}{% endfor %}{% endfor %}{{ Label }} {{ Greeting }}" +
            "{% raw %}{% assign Flag = 1 %}{% endraw %}{% comment %}{% assign SkipSeed = 1 %}{% endcomment %}";

        var result = CreateExpander(variables, logger: logger).Expand(content, FilePath).Content;

        result.Should().Be("free Globex{% assign Flag = 1 %}");
        logger.Entries.Where(entry => entry.Level == LogLevel.Warning).Should().ContainSingle().Which.Message.Should().Be(
            $"'{{% assign Greeting %}}' in '{FilePath}' hides the TeaPie variable 'Greeting' within this file; the " +
            "variable itself is not changed. Rename the assigned name if you meant to use the variable.");
        variables.GetVariable<string>("Greeting").Should().Be("Hi");
        variables.ContainsVariable("Label").Should().BeFalse();
        variables.ContainsVariable("inner").Should().BeFalse();
        variables.ContainsVariable("__teapie_loop_guard").Should().BeFalse();
        variables.ContainsVariable("__teapie_loop_index_0").Should().BeFalse();
    }

    [Theory]
    [InlineData(NewCompaniesLoop + "{% endfor %}{% endfor %}", InnerLoopEmptyEverywhereWarning)]
    [InlineData(NewCompaniesLoop + "{% else %}{% endfor %}{% endfor %}", InnerLoopEmptyEverywhereWarning)]
    [InlineData(NewCompaniesLoop + "{% else %}### None\nPOST https://example.test/none\n{% endfor %}{% endfor %}", null)]
    [InlineData(NewCompaniesLoop + "{% else %}{% if Flag %}### None\nPOST https://example.test/none\n{% endif %}" +
        "{% endfor %}{% endfor %}", "Loop over 'NewCompanies" + LoopWithoutRequestsWarningEnd)]
    [InlineData(NewCompaniesLoop + "{% if u %}### A\nPOST https://example.test/a\n{% else %}### B\n" +
        "POST https://example.test/b\n{% endif %}{% endfor %}{% endfor %}", InnerLoopEmptyEverywhereWarning)]
    [InlineData("{% for i in Items %}{% if i > 99 %}### R\nPOST https://example.test/{{ i }}\n{% endif %}{% endfor %}",
        "Loop over 'Items" + LoopWithoutRequestsWarningEnd)]
    [InlineData("### Create\nPOST https://example.test/items\n\n[{% for i in Items limit: 0 %}{{ i }}{% endfor %}]\n", null)]
    [InlineData("{% if Flag %}{% for i in Items %}### R\nPOST https://example.test/{{ i }}\n{% endfor %}{% endif %}", null)]
    public void WarnWhenALoopGeneratesNoRequests(string content, string? expectedWarning)
    {
        var entries = ExpandAndCollectLogEntries(content);

        entries.Where(entry => entry.Level == LogLevel.Warning).Select(entry => entry.Message).Should().BeEquivalentTo(
            expectedWarning is null ? [] : [expectedWarning]);
    }

    private static string Expand(string content) => CreateExpander(CreateVariables()).Expand(content, FilePath).Content;

    private static string Expand(string content, TemplatingLimits limits)
        => CreateExpander(CreateVariables(), limits).Expand(content, FilePath).Content;

    private static List<(LogLevel Level, string Message)> ExpandAndCollectLogEntries(string content)
    {
        var logger = new RecordingLogger<TemplateExpander>();
        CreateExpander(CreateVariables(), logger: logger).Expand(content, FilePath);
        return logger.Entries;
    }

    private static TemplatingLimits CreateLimits(string? limit, int value) => limit switch
    {
        null => new TemplatingLimits(),
        nameof(TemplatingLimits.MaxExpandedRequests) => new TemplatingLimits { MaxExpandedRequests = value },
        nameof(TemplatingLimits.MaxRenderSteps) => new TemplatingLimits { MaxRenderSteps = value },
        nameof(TemplatingLimits.MaxNestingDepth) => new TemplatingLimits { MaxNestingDepth = value },
        nameof(TemplatingLimits.MaxLoopItems) => new TemplatingLimits { MaxLoopItems = value },
        nameof(TemplatingLimits.MaxExpandedLength) => new TemplatingLimits { MaxExpandedLength = value },
        nameof(TemplatingLimits.MaxListItems) => new TemplatingLimits { MaxListItems = value },
        _ => throw new ArgumentOutOfRangeException(nameof(limit), limit, "Unknown templating limit.")
    };

    private static TeaPieVariables CreateVariables()
    {
        var variables = new TeaPieVariables();
        variables.SetVariable("Tenants", new object[] { new { Name = "Acme" }, new { Name = "Globex" } });
        variables.SetVariable("t", "loop variable shadows this");
        variables.SetVariable("Temp.FreePartners", new[] { "a", "b" });
        variables.SetVariable("Items", new[] { 1, 2, 3, 4, 5 });
        variables.SetVariable("EmptyList", Array.Empty<string>());
        variables.SetVariable("AString", "abc123");
        variables.SetVariable("Root", new { Children = new[] { "c1" } });
        variables.SetVariable("Companies", new object[]
        {
            new
            {
                Name = "Acme",
                Licenses = new[] { "A1", "A2", "A3" },
                Groups = new Dictionary<string, string[]> { ["basic"] = ["AB"] }
            },
            new
            {
                Name = "Globex",
                Licenses = Array.Empty<string>(),
                Groups = new Dictionary<string, string[]> { ["basic"] = ["GB"] }
            }
        });
        variables.SetVariable("Records", new object[]
        {
            new Dictionary<string, object> { ["Name"] = "Rec", ["Licenses"] = new[] { "R1", "R2" } }
        });
        variables.SetVariable("NewCompanies", new object[]
        {
            new { Customers = Array.Empty<string>() },
            new { Customers = Array.Empty<string>() }
        });
        variables.SetVariable("Regions", new object[]
        {
            new
            {
                Name = "EU",
                Countries = new object[]
                {
                    new { Name = "SK", Cities = new object[] { new { Name = "BA", Streets = new[] { "Main" } } } },
                    new
                    {
                        Name = "CZ",
                        Cities = new object[]
                        {
                            new { Name = "Praha", Streets = new[] { "Narodni" } },
                            new { Name = "Brno", Streets = Array.Empty<string>() }
                        }
                    }
                }
            }
        });
        variables.SetVariable("Customers", ((dynamic)(
            "{\"items\":[{\"name\":\"A\",\"address\":{\"city\":\"BA\"},\"ids\":[1,2]},{\"name\":\"B\",\"ids\":[]}]}"
                .ToExpando())).items);
        variables.SetVariable("BadCompanies", new object[]
        {
            new { Licenses = new[] { "A1" } },
            new { Licenses = "NOT-A-LIST" }
        });
        variables.SetVariable("NullCompanies", new object[] { new { Licenses = (string[]?)null } });
        variables.SetVariable("SecretIds", new[] { "S3CR3T" }, Constants.SecretVariableTag);
        variables.SetVariable("ApiKey", "S3CR3T-KEY", Constants.SecretVariableTag);
        variables.GlobalVariables.Set("ShadowedIds", new[] { "PUBLIC" });
        variables.TestCaseVariables.Set("ShadowedIds", new[] { "S3CR3T" }, Constants.SecretVariableTag);
        variables.GlobalVariables.Set("Shared", "global");
        variables.EnvironmentVariables.Set("Shared", "environment");
        variables.TestCaseVariables.Set("Shared", "test-case");
        variables.SetVariable("Greeting", "Hi");
        variables.SetVariable("PartnerCount", 10);
        variables.SetVariable("MaxLicenses", 1);
        variables.SetVariable("NameKey", "Name");
        variables.SetVariable("ListKey", "Licenses");
        variables.SetVariable("SkipSeed", false);
        variables.SetVariable("Flag", false);
        variables.SetVariable("my-var", "hello");
        variables.SetVariable("my-flag", true);
        variables.CollectionVariables.Set<object?>("MaybeNull", null);
        return variables;
    }
}
