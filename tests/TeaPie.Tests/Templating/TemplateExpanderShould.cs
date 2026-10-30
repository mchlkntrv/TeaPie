using FluentAssertions;
using Microsoft.Extensions.Logging;
using static TeaPie.Tests.Templating.TemplatingTestHelpers;
using TeaPie.Templating;
using TeaPieVariables = global::TeaPie.Variables.Variables;

namespace TeaPie.Tests.Templating;

public class TemplateExpanderShould
{
    private const string FilePath = "requests/seed.http";
    private const string BigOutersRequestLimitError = "*loop over 'BigOuters' would expand to more than 1000 requests*";

    private const string FourLevelContent =
        "{% for region in Regions %}{% for country in region.Countries %}" +
        "{% for city in country.Cities %}{% for street in city.Streets %}" +
        "[{{ region.Name }}/{{ country.Name }}/{{ city.Name }}/{{ street }}]" +
        "{% endfor %}{% endfor %}{% endfor %}{% endfor %}";

    [Fact]
    public void ReturnContentUnchangedWhenNoFluidTagIsPresent()
    {
        const string content = "POST {{ApiGatewayBaseUrl}}/companies\n\n{ \"name\": \"Acme\" }";

        var result = CreateExpander().Expand(content, FilePath).Content;

        result.Should().BeSameAs(content);
        ExpandAndCollectLogEntries(content).Should().BeEmpty();
    }

    [Theory]
    [InlineData("POST https://example.test\n\n{\"fmt\":\"{%Y-%m-%d}\"}")]
    [InlineData("{\"template\": \"Hello {% name %}!\"}")]
    [InlineData("{% comment %}note{% endcomment %}")]
    [InlineData("before\n{% endfor %}\nafter")]
    [InlineData("{% For x in Xs %}{{ x }}{% endFor %}")]
    [InlineData("{% format %}{% iffy %}{% assigned %}{% raw_x %}")]
    public void ReturnContentWithoutASupportedTagUnchangedAndWarnAboutIt(string content)
    {
        var logger = new RecordingLogger<TemplateExpander>();

        var result = CreateExpander(CreateVariables(), logger: logger).Expand(content, FilePath).Content;

        result.Should().BeSameAs(content);
        logger.Entries.Should().ContainSingle().Which.Should().Match<(LogLevel Level, string Message)>(entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains(FilePath)
            && entry.Message.Contains("sent unchanged"));
    }

    [Theory]
    [InlineData("{% raw %}{%Y-%m-%d}{% endraw %}", "{%Y-%m-%d}")]
    [InlineData("{% raw %}{% for x in y %}{% endraw %}\nGET https://example.test", "{% for x in y %}\nGET https://example.test")]
    [InlineData("{% comment %}{% for x in Xs %}{% endcomment %}GET https://example.test", "GET https://example.test")]
    [InlineData("{% for i in (1..2) %}[{{ i }}{% raw %}{% endfor %}{% endraw %}]{% endfor %}",
        "[1{% endfor %}][2{% endfor %}]")]
    [InlineData("{%- if true -%}YES{%- endif -%}", "YES")]
    [InlineData("{%unless false%}YES{%endunless%}", "YES")]
    [InlineData("{% assign x = 1 %}{{ x }}", "1")]
    public void TreatEachSupportedTagAsATemplate(string content, string expected)
        => Expand(content).Should().Be(expected);

    [Theory]
    [InlineData("{% if true %}YES{% else %}NO{% endif %}", "YES")]
    [InlineData("{% assign g = 'hi' %}{{ g }}", "hi")]
    [InlineData("{% if Environment == 'prod' %}P{% elsif Environment == 'staging' %}S{% else %}D{% endif %}", "S")]
    [InlineData("{% unless SkipSeed %}SEED{% endunless %}", "SEED")]
    [InlineData("{% if PartnerCount > 5 %}BIG{% else %}SMALL{% endif %}", "BIG")]
    [InlineData("{% if NoSuchVariable %}YES{% else %}NO{% endif %}", "NO")]
    [InlineData("{% assign leaked = SecretIds %}[{{ leaked }}]{% if SecretIds %}YES{% endif %}", "[]")]
    [InlineData("{% assign big = false %}{% if PartnerCount > 5 %}{% assign big = true %}{% endif %}" +
        "{% if big %}BIG{% endif %}", "BIG")]
    [InlineData("{% assign label = 'seeded' %}{% for t in Tenants %}{{ label }}-{{ t.Name }};{% endfor %}",
        "seeded-Acme;seeded-Globex;")]
    [InlineData("{\"tpl\":\"{% if user %}hi{% endif %}\"}", "{\"tpl\":\"\"}")]
    [InlineData("100% {not a template}", "100% {not a template}")]
    [InlineData("Syntax looks like {% raw %}{% this %}{% endraw %}.", "Syntax looks like {% this %}.")]
    public void RenderTopLevelConditionsAndAssignments(string content, string expected)
        => Expand(content).Should().Be(expected);

    [Theory]
    [InlineData("{% for t in Tenants %}{{ forloop.index }}:{{ t.Name }};{% endfor %}", "1:Acme;2:Globex;")]
    [InlineData("{% for p in Temp.FreePartners %}{{ p }}{% endfor %}", "ab")]
    [InlineData("{% for i in (1..3) %}[{{ i }}]{% endfor %}", "[1][2][3]")]
    [InlineData("{%- for i in (1..2) -%}[{{ i }}]{%- endfor -%}", "[1][2]")]
    [InlineData("A\n\n{%- for i in (1..2) -%}\n[{{ i }}]\n{%- endfor -%}\n\nB", "A[1][2]B")]
    [InlineData("A\r\n\r\n{%- for i in (1..2) -%}\r\n[{{ i }}]\r\n{%- endfor -%}\r\n\r\nB", "A[1][2]B")]
    [InlineData("A \n{%- for i in (1..2) %}[{{ i }}]{% endfor %} \nB", "A[1][2] \nB")]
    [InlineData("A\n{% for i in (1..2) %}[{{ i }}]{% endfor -%}\n B", "A\n[1][2]B")]
    [InlineData("{% for i in (1..1) %}x{% endfor -%}\n \n{%- for j in (1..1) %}y{% endfor %}", "xy")]
    [InlineData("{% for s in (\"new\", \"used\") %}{{ forloop.index }}:{{ s }};{% endfor %}", "1:new;2:used;")]
    [InlineData("pre {% for a in (1..2) %}A{{ a }}{% endfor %} mid {% for b in Tenants %}{{ b.Name }}{% endfor %} post",
        "pre A1A2 mid AcmeGlobex post")]
    [InlineData("{% for t in Tenants %}\r\n### {{ t.Name }}\r\nGET x\r\n{% endfor %}\r\n### Plain\r\nGET y\r\n",
        "\r\n### Acme\r\nGET x\r\n\r\n### Globex\r\nGET x\r\n\r\n### Plain\r\nGET y\r\n")]
    [InlineData("{% for t in Tenants %}# @name Item{{ forloop.index }}_{{ forloop.index0 }}_{{ forloop.first }}_" +
        "{{ forloop.last }}\n{% endfor %}", "# @name Item1_0_true_false\n# @name Item2_1_false_true\n")]
    [InlineData("{% for t in Tenants %}{{ forloop.index | prepend: \"{{Temp.CompanyId_\" | append: \"}}\" }};{% endfor %}",
        "{{Temp.CompanyId_1}};{{Temp.CompanyId_2}};")]
    [InlineData("{% for t in Tenants %}{{ t.Name }}{% endfor %}", "AcmeGlobex")]
    public void ExpandLoopsOverEverySupportedSource(string content, string expected)
        => Expand(content).Should().Be(expected);

    [Theory]
    [InlineData("{% for i in Items offset: 1 limit: 3 reversed %}{{ i }}{% endfor %}", "432")]
    [InlineData("{% for i in (1..5) limit: 2 reversed %}{{ i }}{% endfor %}", "21")]
    [InlineData("{% for c in Companies limit: 1 %}{% for l in c.Licenses limit: 2 reversed %}{{ l }}{% endfor %}" +
        "{% endfor %}", "A2A1")]
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
    [InlineData("{% for t in Tenants %}{{{ t.Name }}}{{{ApiGatewayBaseUrl}}}{{{{$guid}}}};{% endfor %}",
        "{Acme}{{{ApiGatewayBaseUrl}}}{{{{$guid}}}};{Globex}{{{ApiGatewayBaseUrl}}}{{{{$guid}}}};")]
    [InlineData("{% for t in Tenants %}{{ \"p-\" | append: t.Name }}|{{- t.Name -}};{% endfor %}", "p-Acme|Acme;p-Globex|Globex;")]
    public void PreserveTeaPieSyntaxInsideLoopBodies(string content, string expected)
        => Expand(content).Should().Be(expected);

    [Theory]
    [InlineData("{% for t in Tenants %}{% if t.Name == 'Acme' %}A{% elsif t.Name == 'Globex' %}G{% else %}O{% endif %}" +
        "{% endfor %}", "AG")]
    [InlineData("{% for i in (1..3) %}{% if forloop.first %}F{% elsif forloop.last %}L{% else %}M{% endif %}{% endfor %}",
        "FML")]
    [InlineData("{% for t in Tenants %}{% unless t.Name == 'Acme' %}N{% else %}Y{% endunless %}{% endfor %}", "YN")]
    [InlineData("{% for t in Tenants %}{{% if t.Name == 'Acme' %}\"a\":1{% endif %}};{% endfor %}", "{\"a\":1};{};")]
    [InlineData("{% for t in Tenants %}{% assign g = Greeting %}{{ g }} {{ t.Name }};{% endfor %}", "Hi Acme;Hi Globex;")]
    [InlineData("{% for i in (1..1) %}{% if PartnerCount > 5 %}{% assign s = 'big' %}{% else %}{% assign s = 'small' %}" +
        "{% endif %}{{ s }}{% endfor %}", "big")]
    [InlineData("{% for i in (1..1) %}[{% assign a = NoSuch %}{{ a }}|{% assign b = MaybeNull %}{{ b }}|" +
        "{% assign c = Temp.FreePartners %}{{ c }}]{% endfor %}", "[||]")]
    [InlineData("{% for i in (1..1) %}{% if NoSuch %}1{% endif %}{% if MaybeNull %}2{% endif %}" +
        "{% if Temp.FreePartners %}3{% endif %}{% if my-flag %}4{% endif %}{% assign x = my-var %}{{ x }}{% endfor %}",
        "4hello")]
    public void ApplyConditionsAndAssignmentsInsideLoopBodies(string content, string expected)
        => Expand(content).Should().Be(expected);

    [Theory]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses %}[{{ c.Name }}:{{ l }}]{% endfor %}{% endfor %}",
        "[Acme:A1][Acme:A2][Acme:A3]")]
    [InlineData("{% for c in Companies %}{% assign ci = forloop.index %}{% for l in c.Licenses %}" +
        "{% if l != 'A2' %}[{{ ci }}.{{ forloop.index }}]{% endif %}{% endfor %}{% endfor %}", "[1.1][1.3]")]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses %}{{ l }}{% else %}[{{ c.Name }}:none]{% endfor %}" +
        "{% endfor %}", "A1A2A3[Globex:none]")]
    [InlineData("{% for my-c in Companies %}{% assign my-n = my-c.Name %}{% for l in my-c.Licenses %}[{{ my-n }}:{{ l }}]" +
        "{% endfor %}{% endfor %}", "[Acme:A1][Acme:A2][Acme:A3]")]
    [InlineData("{% for c in Companies %}{% assign n = c.Name %}{% for g in c.Groups['basic'] %}[{{ n }}:{{ g }}]" +
        "{% endfor %}{% endfor %}", "[Acme:AB][Globex:GB]")]
    [InlineData("{% for c in Companies %}{% for g in c.Groups[GroupKey] %}{{ g }}{% endfor %}{% endfor %}", "ABGB")]
    [InlineData("{% assign k = 'basic' %}{% for c in Companies %}{% for g in c.Groups[k] %}{{ g }}{% endfor %}" +
        "{% endfor %}", "ABGB")]
    [InlineData("{% for c in Companies %}{% assign k = 'basic' %}{% for g in c.Groups[k] %}{{ g }}{% endfor %}" +
        "{% endfor %}", "ABGB")]
    [InlineData("{% for r in Records %}{{ r[NameKey] }}:{% for l in r[ListKey] %}{{ l }}{% endfor %}{% endfor %}",
        "Rec:R1R2")]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses limit: 1 %}{% for g in c.Groups['basic'] %}" +
        "[{{ l }}:{{ g }}]{% endfor %}{% endfor %}{% endfor %}", "[A1:AB]")]
    [InlineData("{% if Flag %}{% for c in Companies %}{% for l in c.Licenses %}{{ l }}{% endfor %}{% endfor %}{% endif %}",
        "")]
    [InlineData(FourLevelContent, "[EU/SK/BA/Main][EU/CZ/Praha/Narodni]")]
    public void ExpandNestedLoops(string content, string expected)
        => Expand(content).Should().Be(expected);

    [Fact]
    public void PreserveWhitespaceControlOnAnInnerLoopThatGetsAGuard()
    {
        const string content =
            "{% for c in Companies limit: 1 %}\n  {%- for l in c.Licenses -%}\n" +
            "  [{{ l }}]\n  {%- endfor -%}\n{% endfor %}";
        const string withoutGuard =
            "{% for c in Companies limit: 1 %}\n  {%- for l in (\"A1\", \"A2\", \"A3\") -%}\n" +
            "  [{{ l }}]\n  {%- endfor -%}\n{% endfor %}";

        Expand(content).Should().Be(Expand(withoutGuard));
    }

    [Theory]
    [InlineData("{% for a in (1..2) %}{% endfor %}\n{% endfor %}", "*requests/seed.http*endfor*")]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses %}{% if true %}{{ c.Typo }}{% endif %}{% endfor %}" +
        "{% endfor %}", "*Typo*")]
    [InlineData("{% for i in (1..501) %}" + OneRequest + OneRequest + "{% endfor %}", "*'(1..501)' would expand to more than 1000*")]
    [InlineData("{% for c in BigOuters %}{% for i in c.Items %}### r\nGET https://example.test/{{ i }}\n{% endfor %}" +
        "{% endfor %}", BigOutersRequestLimitError)]
    [InlineData("\n{%- for c in BigOuters -%}\n{% for i in c.Items %}### r\nGET https://example.test/{{ i }}\n{% endfor %}" +
        "\n{%- endfor -%}\n", BigOutersRequestLimitError)]
    [InlineData("{% for c in SmallOuters %}{% for i in c.Items %}### r\nGET https://example.test/{{ i }}\n{% endfor %}" +
        "{% endfor %}{% for c in BigOuters %}{% for i in c.Items %}### r\nGET https://example.test/{{ i }}\n" +
        "{% endfor %}{% endfor %}", BigOutersRequestLimitError)]
    [InlineData("{% unless Flag %}{% for c in BigOuters %}{% for i in c.Items %}### r\nGET https://example.test/{{ i }}\n" +
        "{% endfor %}{% endfor %}{% endunless %}", BigOutersRequestLimitError)]
    [InlineData("{% case Environment %}{% when 'staging' %}{% for c in BigOuters %}{% for i in c.Items %}### r\n" +
        "GET https://example.test/{{ i }}\n{% endfor %}{% endfor %}{% endcase %}", BigOutersRequestLimitError)]
    [InlineData("{% if Flag %}{% elsif true %}{% for c in BigOuters %}{% for i in c.Items %}### r\n" +
        "GET https://example.test/{{ i }}\n{% endfor %}{% endfor %}{% endif %}", BigOutersRequestLimitError)]
    [InlineData("{% for i in (1..99999999999) %}{{ i }}{% endfor %}", "*99999999999*")]
    [InlineData("{% for p in EmptyList %}{{ p }}{% endfor %}", "*zero items*")]
    [InlineData("{% if Flag %}{% for p in Missing %}{{ p }}{% endfor %}{% endif %}",
        "*requests/seed.http*collection variable 'Missing'*was not found*")]
    [InlineData("{% for p in PartnerCount %}{{ p }}{% endfor %}",
        "*requests/seed.http*variable 'PartnerCount'*must be a collection*")]
    [InlineData("{% for s in (\"a\",) %}{{ s }}{% endfor %}", "*requests/seed.http*inline collection*empty item*")]
    [InlineData("{% for s in SecretIds %}{{ s }}{% endfor %}", "*requests/seed.http*'SecretIds'*tagged as 'secret'*")]
    [InlineData("{% for i in (1..1) %}{% for s in SecretIds %}{{ s }}{% endfor %}{% endfor %}",
        "*'SecretIds'*tagged as 'secret'*")]
    [InlineData("{% assign items = Root %}{% for item in items.Children %}{{ item }}{% endfor %}",
        "*collection variable 'items.Children'*was not found*")]
    [InlineData("{% for c in BadCompanies %}{% for l in c.Licenses %}{{ l }}{% endfor %}{% endfor %}",
        "*variable 'c.Licenses'*must be a collection*(c[1])*")]
    [InlineData("{% for c in NullCompanies %}{% for l in c.Licenses %}{{ l }}{% endfor %}{% endfor %}",
        "*variable 'c.Licenses'*must be a collection*")]
    [InlineData("{% for region in BadRegions %}{% for country in region.Countries %}{% for city in country.Cities %}" +
        "{{ city }}{% endfor %}{% endfor %}{% endfor %}",
        "*variable 'country.Cities'*must be a collection*(region[0].country[1])*")]
    [InlineData("{% for c in Companies %}{% for l in c.Nope %}{{ l }}{% endfor %}{% endfor %}", "*'Nope' is undefined*")]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses | first %}{{ l }}{% endfor %}{% endfor %}",
        "*requests/seed.http*filters are not supported*'c.Licenses | first'*")]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses required %}{{ l }}{% endfor %}{% endfor %}",
        "*requests/seed.http*unsupported option 'required'*'limit:', 'offset:' and 'reversed'*")]
    [InlineData("{% for i in (1..3) limit:-1 %}{{ i }}{% endfor %}",
        "*requests/seed.http*option 'limit:'*must be a whole number from 0 to 1000 or a variable, but is '-1'*")]
    [InlineData("{% for i in (1..3) offset: 2.5 %}{{ i }}{% endfor %}", "*option 'offset:'*but is '2.5'*")]
    [InlineData("{% for i in (1..3) limit: '2' %}{{ i }}{% endfor %}", "*option 'limit:'*but is ''2''*")]
    [InlineData("{% for i in (1..3) limit: 1001 %}{{ i }}{% endfor %}", "*option 'limit:'*but is '1001'*")]
    [InlineData("{% assign n = -1 %}{% for i in (1..3) limit: n %}{{ i }}{% endfor %}",
        "*requests/seed.http*option 'limit:' of the '{% for i %}' loop must be a whole number from 0 to 1000, but is '-1'*")]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses offset: Greeting %}{{ l }}{% endfor %}{% endfor %}",
        "*requests/seed.http*option 'offset:' of the '{% for l %}' loop*but is 'Hi'*")]
    [InlineData("{% for i in (1..3) offset:continue %}{{ i }}{% endfor %}",
        "*requests/seed.http*'offset: continue' is not supported*")]
    [InlineData("{% for i in (1..1) %}{{ i | divided_by: 0 }}{% endfor %}",
        "Templating error in 'requests/seed.http': rendering failed: Attempted to divide by zero.")]
    [InlineData("{% for i in (1..1) %}{{Temp.Id_{{ forloop.index }}}}{% endfor %}", "*failed to parse*")]
    [InlineData("{% for i in (1..1) %}{% raw %}{{ i }}{% endfor %}", "*failed to parse*raw*")]
    [InlineData("{% for t in Tenants %}{% if t.Name %}X{% endfor %}", "*failed to parse template*")]
    [InlineData("{% if true %}x{% endif %}{\"template\": \"Hello {% name %}!\"}", "*wrap it in*raw*")]
    [InlineData("{% assign d = 1 %}{\"fmt\":\"{%Y-%m-%d}\"}", "*wrap it in*raw*")]
    public void ThrowAClearErrorForInvalidTemplatesOrData(string content, string expectedMessage)
    {
        var act = () => Expand(content);

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedMessage);
    }

    [Fact]
    public void KeepTheOriginalExceptionWhenRenderingFails()
    {
        var act = () => Expand("{% for i in (1..1) %}{{ i | divided_by: 0 }}{% endfor %}");

        act.Should().Throw<InvalidOperationException>().WithInnerException<DivideByZeroException>();
    }

    [Fact]
    public void ShowTheRawHintOnItsOwnLineWhenParsingFails()
    {
        var act = () => Expand("{% assign d = 1 %}\n{\"fmt\":\"{%Y-%m-%d}\"}");

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Be(
            $"Templating error in '{FilePath}': failed to parse template: Unknown tag 'Y' at (2:12)\n" +
            "Source:\n" +
            "{\"fmt\":\"{%Y-%m-%d}\"}\n" +
            "If this file contains literal '{%' text that is not a TeaPie template tag, wrap it in " +
            "'{% raw %}...{% endraw %}'.");
    }

    [Fact]
    public void ThrowAClearErrorWhenScanningTheFileTimesOut()
    {
        // The '{% for %}' tag pattern is quadratic in the length of a line that never closes the tag.
        var content = "{% for i in (1..1) %}{{ i }}{% endfor %}\n{% for x in a" + new string(' ', 100_000);

        var act = () => Expand(content);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Templating error in '{FilePath}': scanning the file took longer than*");
    }

    public static TheoryData<string> ContentWithinLimits => new()
    {
        "{% for i in (1..1000) %}[{{ i }}]{% endfor %}",
        "{% for i in (1..1000) %}[{{ i }}]{% endfor %}mid{% for j in (1..1000) %}[{{ j }}]{% endfor %}",
        "{% for c in SmallOuters %}{% for i in c.Items %}### n\nGET https://example.test/{{ i }}\n{% endfor %}{% endfor %}" +
            "{% for i in (1..1000) %}### s\nGET https://example.test/{{ i }}\n{% endfor %}"
    };

    [Theory]
    [MemberData(nameof(ContentWithinLimits))]
    public void ExpandContentThatStaysWithinTheLimits(string content)
    {
        var act = () => Expand(content);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("{% for i in (1..30) %}{{ i }}{% endfor %}", true)]
    [InlineData("{% for i in (1..30) %}{{ i }}{{ i }}{{ i }}{{ i }}{% endfor %}", false)]
    [InlineData("{% for i in (1..30) %}{{ i }}{% endfor %}mid{% for j in (1..30) %}{{ j }}{% endfor %}", false)]
    public void ShareTheRenderStepBudgetAcrossTheWholeFile(string content, bool succeeds)
    {
        var act = () => Expand(content, new TemplatingLimits { MaxRenderSteps = 100 });

        if (succeeds)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<InvalidOperationException>()
                .WithMessage($"Templating error in '{FilePath}': template exceeded the maximum of 100 rendering steps*");
        }
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void RejectLoopsNestedDeeperThanTheMaximum(int depth, bool succeeds)
    {
        var content = string.Concat(Enumerable.Range(0, depth).Select(level => $"{{% for v{level} in (1..1) %}}")) +
            "x" + string.Concat(Enumerable.Repeat("{% endfor %}", depth));

        var act = () => Expand(content, new TemplatingLimits { MaxNestingDepth = 3 });

        if (succeeds)
        {
            act.Should().NotThrow().Which.Should().Be("x");
        }
        else
        {
            act.Should().Throw<InvalidOperationException>().WithMessage(
                $"Templating error in '{FilePath}': loops are nested 4 levels deep, exceeding the maximum of 3*");
        }
    }

    private const string OneRequest = "### r\nGET https://example.test/{{ i }}\n";

    [Theory]
    [InlineData("{% for i in Items limit: 3 %}" + OneRequest + "{% endfor %}", true)]
    [InlineData("{% for i in Items %}{% if i == 2 %}" + OneRequest + "{% endif %}{% endfor %}", true)]
    [InlineData("### r\nPOST https://example.test\n\n[{% for i in Items %}{{ i }},{% endfor %}0]\n", true)]
    [InlineData("{% for i in Items limit: 4 %}" + OneRequest + "{% endfor %}", false)]
    [InlineData("{% assign n = 4 %}{% for i in Items limit: n %}" + OneRequest + "{% endfor %}", false)]
    [InlineData("{% for i in (1..2) %}" + OneRequest + OneRequest + "{% endfor %}", false)]
    [InlineData("{% if true %}{% for i in (1..2) %}" + OneRequest + OneRequest + "{% endfor %}{% endif %}", false)]
    public void CountTheRequestsThatEachTopLevelLoopGenerates(string content, bool succeeds)
    {
        var act = () => Expand(content, new TemplatingLimits { MaxExpandedRequests = 3 });

        if (succeeds)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<InvalidOperationException>().WithMessage(
                $"Templating error in '{FilePath}': loop over '*' would expand to more than 3 requests*");
        }
    }

    [Fact]
    public void StopRenderingAsSoonAsALoopExceedsTheRequestLimit()
    {
        var content = "{% for i in (1..2) %}{% if i == 2 %}{{ Missing.Value }}{% endif %}" +
            string.Concat(Enumerable.Repeat(OneRequest, 5)) + "{% endfor %}";

        var act = () => Expand(content, new TemplatingLimits { MaxExpandedRequests = 3 });

        act.Should().Throw<InvalidOperationException>().WithMessage("*loop over '(1..2)' would expand to more than 3*");
    }

    [Theory]
    [InlineData("{% for i in (1..2) %}{% for j in (1..2) %}{{ j }}{% endfor %}{% endfor %}", null)]
    [InlineData("{% for i in (1..3) limit: 1 %}{{ i }}{% endfor %}",
        "*loop over '(1..3)' has 3 items, exceeding the maximum of 2 items per loop*")]
    [InlineData("{% for i in (1..1) %}{% for j in (1..2000000000) %}{{ j }}{% endfor %}{% endfor %}",
        "*loop over '(1..2000000000)' has 2000000000 items*")]
    [InlineData("{% for i in (\"a\", \"b\", \"c\") %}{{ i }}{% endfor %}", "*has 3 items*")]
    [InlineData("{% for i in Items %}{{ i }}{% endfor %}", "*loop over 'Items' has 5 items*")]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses %}{{ l }}{% endfor %}{% endfor %}",
        "*loop over 'c.Licenses' has more than 2 items (c[0]), exceeding the maximum of 2 items per loop*")]
    public void RejectLoopsWithMoreItemsThanTheMaximum(string content, string? expectedMessage)
    {
        var act = () => Expand(content, new TemplatingLimits { MaxLoopItems = 2 });

        if (expectedMessage is null)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<InvalidOperationException>().WithMessage($"Templating error in '{FilePath}': {expectedMessage}");
        }
    }

    [Theory]
    [InlineData("{% for i in (1..3) %}0123456789{% endfor %}", true)]
    [InlineData("{% for i in (1..6) %}0123456789{% endfor %}", false)]
    [InlineData("{% for i in (1..3) %}0123456789{% endfor %}{% for j in (1..3) %}0123456789{% endfor %}", false)]
    public void RejectLoopsWhoseOutputIsLongerThanTheMaximum(string content, bool succeeds)
    {
        var act = () => Expand(content, new TemplatingLimits { MaxExpandedLength = 50 });

        if (succeeds)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<InvalidOperationException>().WithMessage(
                $"Templating error in '{FilePath}': the loops in the file would expand to more than 50 characters*");
        }
    }

    private const string TenCharacters = "{% assign s = 'xxxxxxxxxx' %}";

    [Theory]
    [InlineData("{% assign s = 'xx' %}{% for i in (1..3) %}{% assign s = s | append: s %}{% endfor %}{{ s }}",
        "xxxxxxxxxxxxxxxx", null)]
    [InlineData("{% capture c %}{% for i in (1..2) %}{{ i }}{% endfor %}{% endcapture %}{% assign d = c %}{{ d }}", "12", null)]
    [InlineData(TenCharacters + "{% assign s = s | append: s %}{% assign s = s | append: s %}" +
        "{% assign s = s | append: s %}", null, "filter 'append' produced a text longer than 50 characters*")]
    [InlineData(TenCharacters + "{% for i in (1..3) %}{% assign s = s | prepend: s %}{% endfor %}", null,
        "filter 'prepend' produced a text longer than 50 characters*")]
    [InlineData("{% assign a = 'x' | split: ',' %}{% for i in (1..3) %}{% assign a = a | concat: a %}{% endfor %}",
        null, "filter 'concat' produced a list with more than 5 items*")]
    [InlineData(TenCharacters + "{% for i in (1..3) %}{% capture s %}{{ s }}{{ s }}{% endcapture %}{% endfor %}", null,
        "'{% capture s %}' produced a text longer than 50 characters*")]
    public void RejectValuesThatGrowLargerThanTheMaximum(string content, string? expected, string? expectedMessage)
    {
        var act = () => Expand(content, new TemplatingLimits { MaxExpandedLength = 50, MaxListItems = 5 });

        if (expectedMessage is null)
        {
            act.Should().NotThrow().Which.Should().Be(expected);
        }
        else
        {
            act.Should().Throw<InvalidOperationException>()
                .WithMessage($"Templating error in '{FilePath}': {expectedMessage}");
        }
    }

    [Theory]
    [InlineData("bob\n###\nDELETE https://example.test/injected")]
    [InlineData("bob\rX-Admin: true")]
    [InlineData("abc###def")]
    [InlineData("{{apiKey}}")]
    [InlineData("}}")]
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
    [InlineData("{% for t in Tenants %}{{ t.Name }}{% endfor %}", "AcmeGlobex")]
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
    [InlineData("{% for v in (\"{{A}}\") %}{{ v }}{% endfor %}", "{{A}}")]
    [InlineData("{% for i in (1..1) %}{% assign url = \"{{ApiGatewayBaseUrl}}\" %}{{ url }}{% endfor %}",
        "{{ApiGatewayBaseUrl}}")]
    [InlineData("{% for c in Secrets %}{{ c | json }}{% endfor %}", "{\"Name\":\"{{apiKey}}\"}")]
    [InlineData("{% for c in Secrets %}{{ c.Name | upcase }}{% endfor %}", "{{APIKEY}}")]
    public void ReferenceBracesThatAreNotWrittenInTheSameExpression(string content, string value)
    {
        var variables = CreateVariables();
        variables.SetVariable("Secrets", new object[] { new { Name = "{{apiKey}}" } });

        var result = CreateExpander(variables).Expand(content, FilePath);

        result.Content.Should().Be("{{__teapie_value_1}}");
        result.DataValues.Should().Equal(new Dictionary<string, string> { ["__teapie_value_1"] = value });
    }

    [Fact]
    public void KeepTopLevelAssignsLocalToTheFile()
    {
        var variables = CreateVariables();

        var result = CreateExpander(variables)
            .Expand("{% assign Label = 'free' %}{% assign Greeting = 'Overridden' %}{{ Label }} {{ Greeting }}", FilePath)
            .Content;

        result.Should().Be("free Overridden");
        variables.ContainsVariable("Label").Should().BeFalse();
        variables.GetVariable<string>("Greeting").Should().Be("Hi");
    }

    [Fact]
    public void NotPersistLoopScopedOrInternalVariables()
    {
        var variables = CreateVariables();
        const string content =
            "{% assign outer = 0 %}{% for c in Companies %}{% assign outer = 1 %}{% for l in c.Licenses %}" +
            "{% assign inner = 1 %}{% endfor %}{% endfor %}";

        CreateExpander(variables).Expand(content, FilePath);

        variables.ContainsVariable("outer").Should().BeFalse();
        variables.ContainsVariable("inner").Should().BeFalse();
        variables.ContainsVariable("__teapie_loop_guard").Should().BeFalse();
        variables.ContainsVariable("__teapie_loop_index_0").Should().BeFalse();
    }

    [Theory]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses %}{{ l }}{% endfor %}{% endfor %}", "'c.Licenses'", "c[1]")]
    [InlineData(FourLevelContent, "'city.Streets'", "region[0].country[1].city[1]")]
    [InlineData("{% for c in Companies limit: 1 %}{% for l in c.Licenses limit: 1 %}{% for g in c.Groups['none'] %}{{ g }}" +
        "{% endfor %}{% endfor %}{% endfor %}", "'c.Groups['none']'", "c[0].l[0]")]
    [InlineData("{% for b in Batch %}{% for l in b.Licenses %}{{ l }}{% endfor %}{% endfor %}", "'b.Licenses'", "b[2]")]
    [InlineData("{% for b in Batch reversed %}{% for l in b.Licenses %}{{ l }}{% endfor %}{% endfor %}",
        "'b.Licenses'", "b[2]")]
    [InlineData("{% for b in Batch offset: 1 %}{% for l in b.Licenses %}{{ l }}{% endfor %}{% endfor %}",
        "'b.Licenses'", "b[2]")]
    [InlineData("{% for b in Batch reversed offset: Skip %}{% for l in b.Licenses %}{{ l }}{% endfor %}{% endfor %}",
        "'b.Licenses'", "b[2]")]
    [InlineData("{% for b in Batch offset: 1 limit: 2 reversed %}{% for l in b.Licenses %}{{ l }}{% endfor %}{% endfor %}",
        "'b.Licenses'", "b[2]")]
    public void LogAtDebugLevelTheSourcePositionOfEachOuterItemWithAnEmptyInnerSource(
        string content, string source, string outerItemPath)
    {
        var entries = ExpandAndCollectLogEntries(content);

        entries.Where(entry => entry.Level == LogLevel.Debug).Should().ContainSingle().Which.Message
            .Should().Contain(source).And.Contain(FilePath).And.Contain($"for {outerItemPath},");
    }

    [Theory]
    [InlineData("{% for c in NewCompanies %}{% for u in c.Customers %}### Add\nPOST https://example.test/{{ u }}\n" +
        "{% endfor %}{% endfor %}", "'c.Customers'", "every one of its 2 outer item(s)")]
    [InlineData("{% for region in EmptyRegions %}{% for country in region.Countries %}{% for city in country.Cities %}" +
        "{% for street in city.Streets %}{{ street }}{% endfor %}{% endfor %}{% endfor %}{% endfor %}",
        "'city.Streets'", "every one of its")]
    [InlineData("{% for i in Items offset: 10 %}### R\nPOST https://example.test/{{ i }}\n{% endfor %}",
        "'Items'", "generated no requests")]
    [InlineData("{% for i in Items limit: Zero %}### R\nPOST https://example.test/{{ i }}\n{% endfor %}",
        "'Items'", "generated no requests")]
    [InlineData("{% for i in Items %}{% if i > 99 %}### R\nPOST https://example.test/{{ i }}\n{% endif %}{% endfor %}",
        "'Items'", "generated no requests")]
    public void WarnOnceWhenALoopGeneratesNoRequests(string content, string source, string detail)
    {
        var entries = ExpandAndCollectLogEntries(content);

        entries.Where(entry => entry.Level == LogLevel.Warning).Should().ContainSingle().Which.Message
            .Should().Contain(source).And.Contain(FilePath).And.Contain(detail);
    }

    [Theory]
    [InlineData("### Create\nPOST https://example.test/items\n\n[{% for i in Items limit: 0 %}{{ i }}{% endfor %}]\n")]
    [InlineData("{% for c in Companies limit: 1 %}{% for l in c.Licenses %}{{ l }}{% endfor %}{% endfor %}")]
    [InlineData("{% if Flag %}{% for i in Items %}### R\nPOST https://example.test/{{ i }}\n{% endfor %}{% endif %}")]
    public void NotLogAnythingWhenNoRequestsAreMissing(string content)
        => ExpandAndCollectLogEntries(content).Should().BeEmpty();

    [Theory]
    [InlineData("{% for t in Tenants %}{{ NotInScope }}{% badtag %}{% endfor %}",
        "{% for t in Tenants %}{{ NotInScope }}{% badtag %}{% endfor %}")]
    [InlineData("### Setup\nPOST {{ApiGatewayBaseUrl}}/init\n\n{% for t in Tenants %}\n{{ NotInScope }}\n{% badtag %}\n" +
        "{% endfor %}", "{% badtag %}")]
    [InlineData("### Setup\nPOST {{ApiGatewayBaseUrl}}/init\n\n{% for t in Tenants %}\n{{ NotInScope }}{% badtag %}\n" +
        "{% endfor %}", "{{ NotInScope }}{% badtag %}")]
    [InlineData("{% for c in Companies %}{% for l in c.Licenses %}{{ l }}{% endfor %}{% badtag %}{% endfor %}",
        "{% for c in Companies %}{% for l in c.Licenses %}{{ l }}{% endfor %}{% badtag %}{% endfor %}")]
    public void ReportFluidParseErrorsAtTheirPositionInTheOriginalFile(string content, string sourceLine)
    {
        var act = () => Expand(content);

        var message = act.Should().Throw<InvalidOperationException>().Which.Message;
        message.Should().Contain(FilePath).And.Contain("Source:\n" + sourceLine).And.NotContain("__teapie_loop_");
        ExtractReportedPosition(message).Should().Be(FindOriginalPosition(content, "{% badtag %}"));
    }

    private static string Expand(string content) => CreateExpander(CreateVariables()).Expand(content, FilePath).Content;

    private static List<(LogLevel Level, string Message)> ExpandAndCollectLogEntries(string content)
    {
        var logger = new RecordingLogger<TemplateExpander>();
        CreateExpander(CreateVariables(), logger: logger).Expand(content, FilePath);
        return logger.Entries;
    }

    private static string Expand(string content, TemplatingLimits limits)
        => CreateExpander(CreateVariables(), limits).Expand(content, FilePath).Content;

    private static TeaPieVariables CreateVariables()
    {
        var variables = new TeaPieVariables();
        variables.SetVariable("Tenants", new object[] { new { Name = "Acme" }, new { Name = "Globex" } });
        variables.SetVariable("t", "loop variable shadows this");
        variables.SetVariable("Temp.FreePartners", new[] { "a", "b" });
        variables.SetVariable("Items", new[] { 1, 2, 3, 4, 5 });
        variables.SetVariable("EmptyList", Array.Empty<string>());
        variables.SetVariable("Root", new { Children = new[] { "c1" } });
        variables.SetVariable("Companies", new object[]
        {
            new
            {
                Name = "Acme",
                Licenses = new[] { "A1", "A2", "A3" },
                Groups = new Dictionary<string, string[]> { ["basic"] = ["AB"], ["none"] = [] }
            },
            new
            {
                Name = "Globex",
                Licenses = Array.Empty<string>(),
                Groups = new Dictionary<string, string[]> { ["basic"] = ["GB"], ["none"] = [] }
            }
        });
        variables.SetVariable("Records", new object[]
        {
            new Dictionary<string, object> { ["Name"] = "Rec", ["Licenses"] = new[] { "R1", "R2" } }
        });
        variables.SetVariable("Batch", new object[]
        {
            new { Licenses = new[] { "A1" } },
            new { Licenses = new[] { "B1" } },
            new { Licenses = Array.Empty<string>() },
            new { Licenses = new[] { "D1" } }
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
        variables.SetVariable("EmptyRegions", new object[]
        {
            new { Countries = new object[] { new { Cities = new object[] { new { Streets = Array.Empty<string>() } } } } }
        });
        variables.SetVariable("BadCompanies", new object[]
        {
            new { Licenses = new[] { "A1" } },
            new { Licenses = "NOT-A-LIST" }
        });
        variables.SetVariable("NullCompanies", new object[] { new { Licenses = (string[]?)null } });
        variables.SetVariable("BadRegions", new object[]
        {
            new { Countries = new object[] { new { Cities = new[] { "BA" } }, new { Cities = "Praha" } } }
        });
        variables.SetVariable("SmallOuters", CreateOuters(10, 10));
        variables.SetVariable("BigOuters", CreateOuters(50, 30));
        variables.SetVariable("SecretIds", new[] { "S3CR3T" }, Constants.SecretVariableTag);
        variables.SetVariable("Greeting", "Hi");
        variables.SetVariable("PartnerCount", 10);
        variables.SetVariable("MaxLicenses", 1);
        variables.SetVariable("Skip", 1);
        variables.SetVariable("Zero", 0);
        variables.SetVariable("GroupKey", "basic");
        variables.SetVariable("NameKey", "Name");
        variables.SetVariable("ListKey", "Licenses");
        variables.SetVariable("Environment", "staging");
        variables.SetVariable("SkipSeed", false);
        variables.SetVariable("Flag", false);
        variables.SetVariable("my-var", "hello");
        variables.SetVariable("my-flag", true);
        variables.CollectionVariables.Set<object?>("MaybeNull", null);
        return variables;
    }

    private static List<object> CreateOuters(int outerCount, int innerCount)
        => Enumerable.Range(1, outerCount)
            .Select(_ => (object)new { Items = Enumerable.Range(1, innerCount).ToList() })
            .ToList();
}
