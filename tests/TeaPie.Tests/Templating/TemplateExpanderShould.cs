using FluentAssertions;
using NSubstitute;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class TemplateExpanderShould
{
    [Fact]
    public void ExpandLoopOverNamedCollectionIntoOneCopyPerItem()
    {
        const string content =
            "{% for partner in FreePartners %}### item {{ forloop.index }}: {{ partner }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("FreePartners", new List<string> { "01245", "012426", "012427" });

        var expander = new TemplateExpander(new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(variables));

        var result = expander.Expand(content, "test.http");

        result.Should().Be("### item 1: 01245### item 2: 012426### item 3: 012427");
    }

    [Fact]
    public void ExpandLoopOverDottedCollectionVariableName()
    {
        const string content = "{% for partner in Temp.FreePartners %}{{ partner }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Temp.FreePartners", new List<string> { "01245", "012426", "012427" });
        var expander = new TemplateExpander(new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(variables));

        var result = expander.Expand(content, "test.http");

        result.Should().Be("01245012426012427");
    }

    [Fact]
    public void ReturnContentUnchangedWhenNoLoopTagIsPresent()
    {
        const string content = "POST {{ApiGatewayBaseUrl}}/companies\n\n{ \"name\": \"Acme\" }";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var result = expander.Expand(content, "test.http");

        result.Should().BeSameAs(content);
    }

    [Fact]
    public void ExpandWhitespaceControlLoopOverNumericRangeCorrectly()
    {
        const string content = "{%- for x in (1..2) -%}[{{ x }}]{%- endfor -%}";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var result = expander.Expand(content, "test.http");

        result.Should().Be("[1][2]");
    }

    [Fact]
    public void ThrowWhenStrayEndforTagIsPresentAlongsideAValidLoop()
    {
        const string content = "{% for a in (1..2) %}[{{ a }}]{% endfor %}\n{% endfor %}";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*endfor*");
    }

    [Fact]
    public void ExpandNumericRangeWithoutAnyVariable()
    {
        const string content = "{% for i in (1..3) %}[{{ i }}]{% endfor %}";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var result = expander.Expand(content, "test.http");

        result.Should().Be("[1][2][3]");
    }

    [Fact]
    public void LeaveMaskedTeaPieTokenIntactEvenWithUndefinedHookActive()
    {
        const string content = "{% for tenant in Tenants %}POST {{ApiGatewayBaseUrl}}/x/{{ tenant.Name }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" } });
        var expander = new TemplateExpander(new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(variables));

        var result = expander.Expand(content, "test.http");

        result.Should().Be("POST {{ApiGatewayBaseUrl}}/x/Acme");
    }

    [Fact]
    public void ThrowWhenLoopItemFieldIsUndefined()
    {
        const string content = "{% for tenant in Tenants %}{{ tenant.TypoField }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" } });
        var expander = new TemplateExpander(new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(variables));

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*TypoField*");
    }

    [Fact]
    public void ThrowWhenExpansionWouldExceedMaxExpandedRequests()
    {
        const string content = "{% for i in (1..2000) %}[{{ i }}]{% endfor %}";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*1000*");
    }

    [Fact]
    public void ExpandLoopWithExactlyMaxExpandedRequestsItems()
    {
        const string content = "{% for i in (1..1000) %}[{{ i }}]{% endfor %}";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var result = expander.Expand(content, "test.http");

        result.Count(c => c == '[').Should().Be(1000);
        result.Should().Contain("[1]").And.Contain("[1000]");
    }

    [Fact]
    public void ThrowWhenLoopExceedsMaxExpandedRequestsByExactlyOne()
    {
        const string content = "{% for i in (1..1001) %}[{{ i }}]{% endfor %}";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*1001*1000*");
    }

    [Fact]
    public void AllowTwoIndependentLoopBlocksToEachReachMaxExpandedRequestsWithoutACumulativeLimit()
    {
        const string content =
            "{% for i in (1..1000) %}[{{ i }}]{% endfor %}" +
            "mid" +
            "{% for j in (1..1000) %}[{{ j }}]{% endfor %}";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var act = () => expander.Expand(content, "test.http");

        act.Should().NotThrow();
    }

    [Fact]
    public void ExpandLoopWithManyExpressionsPerItemWhenStillWithinTheRenderStepLimit()
    {
        var repeatedTag = string.Concat(Enumerable.Repeat("{{ i }}", 100));
        var content = $"{{% for i in (1..1000) %}}{repeatedTag}{{% endfor %}}";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var act = () => expander.Expand(content, "test.http");

        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowWhenRenderStepsExceedTheMaximum()
    {
        var repeatedTag = string.Concat(Enumerable.Repeat("{{ i }}", 205));
        var content = $"{{% for i in (1..1000) %}}{repeatedTag}{{% endfor %}}";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*rendering steps*");
    }

    [Fact]
    public void KeepStepBudgetsIndependentAcrossMultipleLoopBlocksInOneFile()
    {
        var heavyTag = string.Concat(Enumerable.Repeat("{{ i }}", 100));
        var content =
            $"{{% for i in (1..1000) %}}{heavyTag}{{% endfor %}}" +
            "mid" +
            $"{{% for j in (1..1000) %}}{heavyTag}{{% endfor %}}";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var act = () => expander.Expand(content, "test.http");

        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowWhenResolvedItemCountDisagreesWithTheActuallyRenderedCollection()
    {
        const string content = "{% for x in Weird %}[{{ x }}]{% endfor %}";
        var resolver = Substitute.For<ICollectionSourceResolver>();
        resolver.Resolve("Weird").Returns(new LoopSource(new List<object>(), 3));
        var expander = new TemplateExpander(new LoopBlockScanner(), new LoopBodyMasker(), resolver);

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*resolved 3 item(s) but rendered empty output*");
    }

    [Fact]
    public void ThrowWhenCollectionIsEmpty()
    {
        const string content = "{% for partner in FreePartners %}{{ partner }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("FreePartners", new List<string>());
        var expander = new TemplateExpander(new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(variables));

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*zero items*");
    }

    [Fact]
    public void ThrowParseErrorForLiteralNestedBracesInsteadOfSilentlyCorrupting()
    {
        const string content =
            "{% for tenant in Tenants %}" +
            "{{Temp.CompanyId_{{ forloop.index }}}}" +
            "{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        var expander = new TemplateExpander(new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(variables));

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*failed to parse*");
    }

    [Fact]
    public void PreserveLiteralTextBeforeAndAfterLoopBlockUnchanged()
    {
        const string content =
            "### Setup\n" +
            "POST {{ApiGatewayBaseUrl}}/init\n\n" +
            "{% for i in (1..2) %}[{{ i }}]{% endfor %}\n" +
            "### Teardown\n" +
            "POST {{ApiGatewayBaseUrl}}/cleanup";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var result = expander.Expand(content, "test.http");

        result.Should().Be(
            "### Setup\n" +
            "POST {{ApiGatewayBaseUrl}}/init\n\n" +
            "[1][2]\n" +
            "### Teardown\n" +
            "POST {{ApiGatewayBaseUrl}}/cleanup");
    }

    [Fact]
    public void ExpandTwoIndependentLoopBlocksSeparatedByLiteralText()
    {
        const string content =
            "{% for a in (1..2) %}A{{ a }}{% endfor %}mid{% for b in (1..2) %}B{{ b }}{% endfor %}";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var result = expander.Expand(content, "test.http");

        result.Should().Be("A1A2midB1B2");
    }

    [Fact]
    public void ExpandDynamicNamingPatternBuiltWithPrependAppendFilters()
    {
        const string content =
            "{% for tenant in Tenants %}" +
            "### Create company ({{ tenant.Label }})\n" +
            "## TEST-JSON-HAS-ID-PROPERTY: {{ forloop.index | prepend: \"Temp.Attachments.CompanyId_\" }}\n" +
            "POST {{ApiGatewayBaseUrl}}/companies\n\n" +
            "### Set license for company ({{ tenant.Label }})\n" +
            "POST {{ApiGatewayBaseUrl}}/companies/" +
            "{{ forloop.index | prepend: \"{{Temp.Attachments.CompanyId_\" | append: \"}}\" }}" +
            "/licenses\n" +
            "{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Label = "Tenant A" }, new { Label = "Tenant B" } });
        var expander = new TemplateExpander(new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(variables));

        var result = expander.Expand(content, "test.http");

        result.Should().Contain("### Create company (Tenant A)");
        result.Should().Contain("## TEST-JSON-HAS-ID-PROPERTY: Temp.Attachments.CompanyId_1");
        result.Should().Contain("POST {{ApiGatewayBaseUrl}}/companies/{{Temp.Attachments.CompanyId_1}}/licenses");
        result.Should().Contain("### Create company (Tenant B)");
        result.Should().Contain("## TEST-JSON-HAS-ID-PROPERTY: Temp.Attachments.CompanyId_2");
        result.Should().Contain("POST {{ApiGatewayBaseUrl}}/companies/{{Temp.Attachments.CompanyId_2}}/licenses");
    }

    [Fact]
    public void ExpandLoopOverInlineLiteralListIntoOneCopyPerItem()
    {
        const string content =
            "{% for status in (\"new\", \"used\", \"certified\") %}### item {{ forloop.index }}: {{ status }}{% endfor %}";
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(new global::TeaPie.Variables.Variables()));

        var result = expander.Expand(content, "test.http");

        result.Should().Be("### item 1: new### item 2: used### item 3: certified");
    }

    [Fact]
    public void PreserveUserAuthoredRawBlockAroundLoopVariableAsLiteralTextAcrossIterations()
    {
        const string content =
            "{% for partner in Partners %}" +
            "POST {{ApiGatewayBaseUrl}}/partners\n" +
            "{ \"name\": \"{{ partner.Name }}\", \"literal\": \"{% raw %}{{ partner.Name }}{% endraw %}\" }" +
            "{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Partners", new List<object> { new { Name = "Acme Corp" }, new { Name = "Globex Inc" } });
        var expander = new TemplateExpander(new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(variables));

        var result = expander.Expand(content, "test.http");

        result.Should().Be(
            "POST {{ApiGatewayBaseUrl}}/partners\n" +
            "{ \"name\": \"Acme Corp\", \"literal\": \"{{ partner.Name }}\" }" +
            "POST {{ApiGatewayBaseUrl}}/partners\n" +
            "{ \"name\": \"Globex Inc\", \"literal\": \"{{ partner.Name }}\" }");
    }

    [Fact]
    public void PreserveUserAuthoredRawBlockAroundTeaPieVariableInsideLoopBody()
    {
        const string content =
            "{% for partner in Partners %}" +
            "{ \"name\": \"{{ partner.Name }}\", \"literal\": \"{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}\" }" +
            "{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Partners", new List<object> { new { Name = "Acme Corp" } });
        var expander = new TemplateExpander(new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(variables));

        var result = expander.Expand(content, "test.http");

        result.Should().Be("{ \"name\": \"Acme Corp\", \"literal\": \"{{ApiGatewayBaseUrl}}\" }");
    }

    [Fact]
    public void ThrowClearParseErrorForUnclosedUserAuthoredRawBlock()
    {
        const string content =
            "{% for partner in Partners %}" +
            "{ \"literal\": \"{% raw %}{{ partner.Name }}\" }" +
            "{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Partners", new List<object> { new { Name = "Acme Corp" } });
        var expander = new TemplateExpander(new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(variables));

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*failed to parse*raw*");
    }
}
