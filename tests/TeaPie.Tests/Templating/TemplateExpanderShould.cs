using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using static TeaPie.Tests.Templating.TemplatingTestHelpers;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class TemplateExpanderShould
{
    [Fact]
    public void RenderATopLevelIfBlockOutsideAnyLoop()
    {
        const string content = "{% if true %}YES{% else %}NO{% endif %}";

        var result = CreateExpander().Expand(content, "test.http");

        result.Should().Be("YES");
    }

    [Fact]
    public void RenderATopLevelAssignAndUseItInSubsequentTopLevelInterpolation()
    {
        const string content = "{% assign greeting = \"hi\" %}{{ greeting }}";

        var result = CreateExpander().Expand(content, "test.http");

        result.Should().Be("hi");
    }

    [Fact]
    public void ExpandLoopOverNamedCollectionIntoOneCopyPerItem()
    {
        const string content =
            "{% for partner in FreePartners %}### item {{ forloop.index }}: {{ partner }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("FreePartners", new List<string> { "01245", "012426", "012427" });

        var expander = CreateExpander(variables);

        var result = expander.Expand(content, "test.http");

        result.Should().Be("### item 1: 01245### item 2: 012426### item 3: 012427");
    }

    [Fact]
    public void ExpandLoopOverDottedCollectionVariableName()
    {
        const string content = "{% for partner in Temp.FreePartners %}{{ partner }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Temp.FreePartners", new List<string> { "01245", "012426", "012427" });
        var expander = CreateExpander(variables);

        var result = expander.Expand(content, "test.http");

        result.Should().Be("01245012426012427");
    }

    [Fact]
    public void ReturnContentUnchangedWhenNoLoopTagIsPresent()
    {
        const string content = "POST {{ApiGatewayBaseUrl}}/companies\n\n{ \"name\": \"Acme\" }";
        var expander = CreateExpander();

        var result = expander.Expand(content, "test.http");

        result.Should().BeSameAs(content);
    }

    [Fact]
    public void ExpandWhitespaceControlLoopOverNumericRangeCorrectly()
    {
        const string content = "{%- for x in (1..2) -%}[{{ x }}]{%- endfor -%}";
        var expander = CreateExpander();

        var result = expander.Expand(content, "test.http");

        result.Should().Be("[1][2]");
    }

    [Fact]
    public void ThrowWhenStrayEndforTagIsPresentAlongsideAValidLoop()
    {
        const string content = "{% for a in (1..2) %}[{{ a }}]{% endfor %}\n{% endfor %}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*endfor*");
    }

    [Fact]
    public void IncludeTheRequestFilePathWhenALoopTagIsMalformed()
    {
        const string content = "{% for a in (1..2) %}[{{ a }}]{% endfor %}\n{% endfor %}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "loops/malformed-req.http");

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("loops/malformed-req.http");
        exception.Message.Should().Contain("endfor");
    }

    [Fact]
    public void ExpandNumericRangeWithoutAnyVariable()
    {
        const string content = "{% for i in (1..3) %}[{{ i }}]{% endfor %}";
        var expander = CreateExpander();

        var result = expander.Expand(content, "test.http");

        result.Should().Be("[1][2][3]");
    }

    [Fact]
    public void LeaveMaskedTeaPieTokenIntactEvenWithUndefinedHookActive()
    {
        const string content = "{% for tenant in Tenants %}POST {{ApiGatewayBaseUrl}}/x/{{ tenant.Name }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" } });
        var expander = CreateExpander(variables);

        var result = expander.Expand(content, "test.http");

        result.Should().Be("POST {{ApiGatewayBaseUrl}}/x/Acme");
    }

    [Fact]
    public void ThrowWhenLoopItemFieldIsUndefined()
    {
        const string content = "{% for tenant in Tenants %}{{ tenant.TypoField }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" } });
        var expander = CreateExpander(variables);

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*TypoField*");
    }

    [Fact]
    public void ThrowWhenExpansionWouldExceedMaxExpandedRequests()
    {
        const string content = "{% for i in (1..2000) %}[{{ i }}]{% endfor %}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*1000*");
    }

    [Fact]
    public void ExpandLoopWithExactlyMaxExpandedRequestsItems()
    {
        const string content = "{% for i in (1..1000) %}[{{ i }}]{% endfor %}";
        var expander = CreateExpander();

        var result = expander.Expand(content, "test.http");

        result.Count(c => c == '[').Should().Be(1000);
        result.Should().Contain("[1]").And.Contain("[1000]");
    }

    [Fact]
    public void ThrowWhenLoopExceedsMaxExpandedRequestsByExactlyOne()
    {
        const string content = "{% for i in (1..1001) %}[{{ i }}]{% endfor %}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*1001*1000*");
    }

    [Fact]
    public void ThrowTemplatingErrorInsteadOfOverflowExceptionWhenNumericRangeUpperBoundIsTooLarge()
    {
        const string content = "{% for i in (1..99999999999) %}[{{ i }}]{% endfor %}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*99999999999*");
    }

    [Fact]
    public void AllowTwoIndependentLoopBlocksToEachReachMaxExpandedRequestsWithoutACumulativeLimit()
    {
        const string content =
            "{% for i in (1..1000) %}[{{ i }}]{% endfor %}" +
            "mid" +
            "{% for j in (1..1000) %}[{{ j }}]{% endfor %}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "test.http");

        act.Should().NotThrow();
    }

    [Fact]
    public void ExpandLoopWithManyExpressionsPerItemWhenStillWithinTheRenderStepLimit()
    {
        var repeatedTag = string.Concat(Enumerable.Repeat("{{ i }}", 100));
        var content = $"{{% for i in (1..1000) %}}{repeatedTag}{{% endfor %}}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "test.http");

        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowWhenRenderStepsExceedTheMaximum()
    {
        var repeatedTag = string.Concat(Enumerable.Repeat("{{ i }}", 205));
        var content = $"{{% for i in (1..1000) %}}{repeatedTag}{{% endfor %}}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*rendering steps*");
    }

    [Fact]
    public void ThrowWhenTwoLoopBlocksTogetherExceedTheSharedRenderStepBudget()
    {
        var heavyTag = string.Concat(Enumerable.Repeat("{{ i }}", 100));
        var content =
            $"{{% for i in (1..1000) %}}{heavyTag}{{% endfor %}}" +
            "mid" +
            $"{{% for j in (1..1000) %}}{heavyTag}{{% endfor %}}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*rendering steps*");
    }

    [Fact]
    public void RenderEmptyWithoutThrowingWhenResolvedItemCountDisagreesWithTheActuallyRenderedCollection()
    {
        const string content = "{% for x in Weird %}[{{ x }}]{% endfor %}";
        var resolver = Substitute.For<ICollectionSourceResolver>();
        resolver.Resolve("Weird").Returns(new LoopSource(new List<object>(), 3));
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), resolver, new VariablesFluidModelBuilder(),
            new global::TeaPie.Variables.Variables(), new TemplatingLimits(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TemplateExpander>.Instance);

        var result = expander.Expand(content, "test.http");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ThrowWhenCollectionIsEmpty()
    {
        const string content = "{% for partner in FreePartners %}{{ partner }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("FreePartners", new List<string>());
        var expander = CreateExpander(variables);

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*zero items*");
    }

    [Fact]
    public void LeaveANoArgumentFunctionTokenIntactAfterExpansionInsideALoop()
    {
        const string content = "{% for tenant in Tenants %}{{$guid}}-{{ tenant.Name }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" }, new { Name = "Globex" } });
        var expander = CreateExpander(variables);

        var result = expander.Expand(content, "test.http");

        result.Should().Be("{{$guid}}-Acme{{$guid}}-Globex");
    }

    [Fact]
    public void LeaveAFunctionTokenWithAPlainArgumentIntactAfterExpansionInsideALoop()
    {
        const string content = "{% for tenant in Tenants %}{{$randomInt 1 100}}-{{ tenant.Name }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" } });
        var expander = CreateExpander(variables);

        var result = expander.Expand(content, "test.http");

        result.Should().Be("{{$randomInt 1 100}}-Acme");
    }

    [Fact]
    public void LeaveAFunctionTokenWithANestedVariableArgumentIntactAfterExpansionInsteadOfThrowingAParseError()
    {
        const string content =
            "{% for tenant in Tenants %}{{$add {{MyNumber}} 2}}-{{ tenant.Name }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" }, new { Name = "Globex" } });
        var expander = CreateExpander(variables);

        var result = expander.Expand(content, "test.http");

        result.Should().Be("{{$add {{MyNumber}} 2}}-Acme{{$add {{MyNumber}} 2}}-Globex");
    }

    [Fact]
    public void LeaveSeveralFunctionTokensIntactAlongsideTheLoopVariableAcrossIterations()
    {
        const string content =
            "{% for tenant in Tenants %}{{$guid}}:{{ tenant.Name }}:{{$add {{MyNumber}} 2}}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" }, new { Name = "Globex" } });
        var expander = CreateExpander(variables);

        var result = expander.Expand(content, "test.http");

        result.Should().Be("{{$guid}}:Acme:{{$add {{MyNumber}} 2}}{{$guid}}:Globex:{{$add {{MyNumber}} 2}}");
    }

    [Fact]
    public void LeaveTwoAdjacentFunctionTokensWithNestedArgumentsIntactWithNoSeparatorBetweenThem()
    {
        const string content = "{% for tenant in Tenants %}{{$add {{X}} 1}}{{$add {{Y}} 1}}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        var expander = CreateExpander(variables);

        var result = expander.Expand(content, "test.http");

        result.Should().Be("{{$add {{X}} 1}}{{$add {{Y}} 1}}");
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
        var expander = CreateExpander(variables);

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
        var expander = CreateExpander();

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
        var expander = CreateExpander();

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
        var expander = CreateExpander(variables);

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
        var expander = CreateExpander();

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
        var expander = CreateExpander(variables);

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
        var expander = CreateExpander(variables);

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
        var expander = CreateExpander(variables);

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*failed to parse*raw*");
    }

    [Theory]
    [InlineData("{{ forloop.index }}", "1", "2")]
    [InlineData("{{ forloop.index0 }}", "0", "1")]
    [InlineData("{{ forloop.first }}", "true", "false")]
    [InlineData("{{ forloop.last }}", "false", "true")]
    public void ExpandEachForloopFieldInsideAnAtNameDeclaration(string token, string firstValue, string secondValue)
    {
        var content = "{% for tenant in Tenants %}# @name Create" + token + "\n{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { }, new { } });
        var expander = CreateExpander(variables);

        var result = expander.Expand(content, "test.http");

        result.Should().Contain("# @name Create" + firstValue);
        result.Should().Contain("# @name Create" + secondValue);
    }

    [Fact]
    public void ExpandAllForloopFieldsTogetherInsideAnAtNameDeclaration()
    {
        const string content =
            "{% for tenant in Tenants %}" +
            "# @name Item{{ forloop.index }}_{{ forloop.index0 }}_{{ forloop.first }}_{{ forloop.last }}\n" +
            "{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { }, new { } });
        var expander = CreateExpander(variables);

        var result = expander.Expand(content, "test.http");

        result.Should().Contain("# @name Item1_0_true_false");
        result.Should().Contain("# @name Item2_1_false_true");
    }

    [Fact]
    public void ExpandForloopIndexInsideAnAtNameDeclarationCombinedWithARealisticRequestBody()
    {
        const string content =
            "{% for partner in Partners %}" +
            "### Create partner {{ forloop.index }}: {{ partner.Name }}\n" +
            "# @name CreatePartner{{ forloop.index }}\n" +
            "## TEST-EXPECT-STATUS: [201]\n" +
            "POST https://example.com/partners\n" +
            "Content-Type: application/json\n\n" +
            "{ \"name\": \"{{ partner.Name }}\" }\n" +
            "{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Partners", new List<object> { new { Name = "Acme" }, new { Name = "Globex" } });
        var expander = CreateExpander(variables);

        var result = expander.Expand(content, "test.http");

        result.Should().Contain("# @name CreatePartner1");
        result.Should().Contain("# @name CreatePartner2");
        result.Should().NotContain("{{ forloop.index }}");
    }

    [Fact]
    public void ResolveAssignedVariableFromALiteralInsideALoopBody()
    {
        const string content =
            "{% for tenant in Tenants %}{% assign greeting = \"Hello\" %}{{ greeting }}, {{ tenant.Name }}!{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" }, new { Name = "Globex" } });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("Hello, Acme!Hello, Globex!");
    }

    [Fact]
    public void ResolveAssignedVariableDerivedFromABridgedIVariablesValue()
    {
        const string content =
            "{% for tenant in Tenants %}{% assign greeting = Greeting %}{{ greeting }}, {{ tenant.Name }}!{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" } });
        variables.CollectionVariables.Set("Greeting", "Hi there");

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("Hi there, Acme!");
    }

    [Fact]
    public void UseAnAssignedVariableAcrossMultipleUsagesLaterInTheSameIterationBody()
    {
        const string content =
            "{% for tenant in Tenants %}{% assign label = \"VIP\" %}{{ label }}: {{ tenant.Name }} ({{ label }}){% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" } });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("VIP: Acme (VIP)");
    }

    [Fact]
    public void RenderEmptyWithoutThrowingWhenAnAssignRightHandSideIsUndefinedAndTheEntireBodyRendersEmpty()
    {
        const string content = "{% for tenant in Tenants %}{% assign greeting = NoSuchVariable %}{{ greeting }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().BeEmpty();
    }

    [Fact]
    public void SilentlyRenderEmptyWithoutThrowingWhenAnAssignRightHandSideIsUndefinedAndTheBodyHasSurroundingLiteralText()
    {
        const string content = "{% for tenant in Tenants %}X{% assign greeting = NoSuchVariable %}{{ greeting }}Y{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("XY");
    }

    [Fact]
    public void PreserveATeaPieLookingSubstringInsideAnAssignRightHandSideStringLiteral()
    {
        const string content = "{% for tenant in Tenants %}{% assign url = \"{{ApiGatewayBaseUrl}}\" %}{{ url }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("{{ApiGatewayBaseUrl}}");
    }

    [Fact]
    public void PreferTheLoopVariableOverABridgedIVariablesValueWithTheSameName()
    {
        const string content = "{% for tenant in Tenants %}{{ tenant.Name }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" } });
        variables.CollectionVariables.Set("tenant", "should-not-be-seen");

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("Acme");
    }

    [Fact]
    public void LetAnAssignTargetShadowAnExistingTeaPieVariableNameAsADocumentedCollisionRisk()
    {
        const string content =
            "{% for tenant in Tenants %}{% assign ApiGatewayBaseUrl = \"local-override\" %}{{ ApiGatewayBaseUrl }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        variables.SetVariable("ApiGatewayBaseUrl", "https://real.example.com");

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("local-override");
    }

    [Fact]
    public void SilentlyYieldNilWhenAnAssignRightHandSideReferencesADottedTeaPieVariableNameAsADocumentedLimitation()
    {
        const string content = "{% for tenant in Tenants %}[{% assign x = Temp.FreePartners %}{{ x }}]{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        variables.SetVariable("Temp.FreePartners", new List<string> { "a" });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[]");
    }

    [Fact]
    public void RenderEmptyWithoutThrowingWhenADottedAssignRightHandSideRendersAnEmptyBody()
    {
        const string content = "{% for tenant in Tenants %}{% assign x = Temp.FreePartners %}{{ x }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        variables.SetVariable("Temp.FreePartners", new List<string> { "a" });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveAnAssignRightHandSideReferencingAHyphenatedTeaPieVariableName()
    {
        const string content = "{% for tenant in Tenants %}[{% assign x = my-var %}{{ x }}]{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        variables.SetVariable("my-var", "hello");

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[hello]");
    }

    [Fact]
    public void TreatAHyphenatedTeaPieVariableNameAsTruthyInAnIfCondition()
    {
        const string content = "{% for tenant in Tenants %}{% if my-flag %}YES{% else %}NO{% endif %}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        variables.SetVariable("my-flag", true);

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("YES");
    }

    [Fact]
    public void RenderEmptyWithoutThrowingWhenAnAssignRightHandSideResolvesToAVariableExplicitlySetToNull()
    {
        const string content = "{% for tenant in Tenants %}[{% assign x = MaybeNull %}{{ x }}]{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        variables.CollectionVariables.Set<object?>("MaybeNull", null);

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[]");
    }

    [Fact]
    public void RenderTheIfBranchWhenTheConditionIsTrueAndTheElseBranchWhenItIsFalse()
    {
        const string content =
            "{% for tenant in Tenants %}" +
            "{% if tenant.Name == \"Acme\" %}MATCH{% else %}NOMATCH{% endif %}" +
            "{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" }, new { Name = "Globex" } });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("MATCHNOMATCH");
    }

    [Fact]
    public void FallThroughAnElsifChainToTheMatchingBranch()
    {
        const string content =
            "{% for tenant in Tenants %}" +
            "{% if tenant.Name == \"Acme\" %}A{% elsif tenant.Name == \"Globex\" %}G{% else %}O{% endif %}" +
            "{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object>
        {
            new { Name = "Acme" }, new { Name = "Globex" }, new { Name = "Initech" }
        });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("AGO");
    }

    [Fact]
    public void UseForloopFirstAndLastInsideAnIfCondition()
    {
        const string content =
            "{% for i in (1..3) %}" +
            "{% if forloop.first %}FIRST{% elsif forloop.last %}LAST{% else %}MID{% endif %}" +
            "{% endfor %}";

        var result = CreateExpander().Expand(content, "test.http");

        result.Should().Be("FIRSTMIDLAST");
    }

    [Fact]
    public void EvaluateAnIfConditionOverABridgedIVariablesValue()
    {
        const string content =
            "{% for tenant in Tenants %}{% if PartnerCount > 5 %}BIG{% else %}SMALL{% endif %}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        variables.CollectionVariables.Set("PartnerCount", 10);

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("BIG");
    }

    [Fact]
    public void EvaluateAnIfConditionOverABridgedIVariablesValueThatIsFalse()
    {
        const string content =
            "{% for tenant in Tenants %}{% if PartnerCount > 5 %}BIG{% else %}SMALL{% endif %}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        variables.CollectionVariables.Set("PartnerCount", 2);

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("SMALL");
    }

    [Fact]
    public void ResolveAnAssignTargetDeclaredInBothBranchesOfAnIfBlockAndUsedAfterEndif()
    {
        const string content =
            "{% for tenant in Tenants %}" +
            "{% if PartnerCount > 5 %}{% assign label = \"big\" %}{% else %}{% assign label = \"small\" %}{% endif %}" +
            "{{ label }}" +
            "{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        variables.CollectionVariables.Set("PartnerCount", 10);

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("big");
    }

    [Fact]
    public void TreatAnUndefinedNameInAnIfConditionAsFalsyWithoutThrowing()
    {
        const string content =
            "{% for tenant in Tenants %}{% if NoSuchVariable %}YES{% else %}NO{% endif %}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("NO");
    }

    [Fact]
    public void TreatADottedTeaPieVariableNameInAnIfConditionAsFalsyAsADocumentedLimitation()
    {
        const string content =
            "{% for tenant in Tenants %}{% if Temp.FreePartners %}YES{% else %}NO{% endif %}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        variables.SetVariable("Temp.FreePartners", new List<string> { "a" });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("NO");
    }

    [Fact]
    public void TreatAVariableExplicitlySetToNullInAnIfConditionAsFalsy()
    {
        const string content =
            "{% for tenant in Tenants %}{% if MaybeNull %}YES{% else %}NO{% endif %}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        variables.CollectionVariables.Set<object?>("MaybeNull", null);

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("NO");
    }

    [Fact]
    public void StillThrowForAnUndefinedLoopItemMemberInsideAnIfBranch()
    {
        const string content =
            "{% for tenant in Tenants %}{% if true %}{{ tenant.TypoField }}{% endif %}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" } });

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*TypoField*");
    }

    [Fact]
    public void ThrowAClearParseErrorWhenEndifIsMissing()
    {
        const string content = "{% for tenant in Tenants %}{% if tenant.Name %}X{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" } });

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*failed to parse template*");
    }

    [Fact]
    public void RenderTheUnlessBodyOnlyWhenTheConditionIsFalse()
    {
        const string content =
            "{% for tenant in Tenants %}{% unless tenant.Name == \"Acme\" %}NOT-ACME{% endunless %}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" }, new { Name = "Globex" } });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("NOT-ACME");
    }

    [Fact]
    public void RenderTheElseBranchOfUnlessWhenTheConditionIsTrue()
    {
        const string content =
            "{% for tenant in Tenants %}" +
            "{% unless tenant.Name == \"Acme\" %}NOT-ACME{% else %}IS-ACME{% endunless %}" +
            "{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" }, new { Name = "Globex" } });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("IS-ACMENOT-ACME");
    }

    [Fact]
    public void RenderEmptyWithoutThrowingWhenAnUnlessFiltersOutEveryLoopItem()
    {
        const string content =
            "{% for tenant in Tenants %}{% unless tenant.Name == \"Acme\" %}X{% endunless %}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" }, new { Name = "Acme" } });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().BeEmpty();
    }

    [Fact]
    public void FallThroughATopLevelElsifChainToTheMatchingBranch()
    {
        const string content =
            "{% if Environment == \"prod\" %}P{% elsif Environment == \"staging\" %}S{% else %}D{% endif %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Environment", "staging");

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("S");
    }

    [Fact]
    public void RenderATopLevelUnlessBlock()
    {
        const string content = "{% unless SkipSeed %}SEEDING{% endunless %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("SkipSeed", false);

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("SEEDING");
    }

    [Fact]
    public void EvaluateATopLevelIfConditionOverABridgedIVariablesValue()
    {
        const string content = "{% if PartnerCount > 5 %}BIG{% else %}SMALL{% endif %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.CollectionVariables.Set("PartnerCount", 10);

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("BIG");
    }

    [Fact]
    public void TreatAnUndefinedNameInATopLevelIfConditionAsFalsyWithoutThrowing()
    {
        const string content = "{% if NoSuchVariable %}YES{% else %}NO{% endif %}";

        var result = CreateExpander().Expand(content, "test.http");

        result.Should().Be("NO");
    }

    [Fact]
    public void UseATopLevelAssignTargetInAnIfConditionOverAValueDerivedFromABridgedVariable()
    {
        const string content =
            "{% assign isBig = false %}" +
            "{% if PartnerCount > 5 %}{% assign isBig = true %}{% endif %}" +
            "{% if isBig %}BIG{% else %}SMALL{% endif %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.CollectionVariables.Set("PartnerCount", 10);

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("BIG");
    }

    [Fact]
    public void RenderATopLevelAssignConsumedByASubsequentLoopInTheSameFile()
    {
        const string content =
            "{% assign label = \"seeded\" %}" +
            "{% for item in Items %}{{ label }}-{{ item.Name }};{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Items", new List<object> { new { Name = "A" }, new { Name = "B" } });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("seeded-A;seeded-B;");
    }

    [Fact]
    public void PersistATopLevelAssignedStringValueToCollectionVariables()
    {
        const string content = "{% assign PartnerLabel = \"free\" %}";
        var variables = new global::TeaPie.Variables.Variables();

        CreateExpander(variables).Expand(content, "test.http");

        variables.GetVariable<string>("PartnerLabel").Should().Be("free");
    }

    [Fact]
    public void PersistATopLevelAssignedNumberAsDecimal()
    {
        const string content = "{% assign PartnerCount = 5 %}";
        var variables = new global::TeaPie.Variables.Variables();

        CreateExpander(variables).Expand(content, "test.http");

        variables.GetVariable<decimal>("PartnerCount").Should().Be(5m);
    }

    [Fact]
    public void NotPersistAnInLoopAssignTarget()
    {
        const string content = "{% for item in Items %}{% assign label = item.Name %}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Items", new List<object> { new { Name = "A" } });

        CreateExpander(variables).Expand(content, "test.http");

        variables.ContainsVariable("label").Should().BeFalse();
    }

    [Fact]
    public void DocumentThatANameSharedBetweenATopLevelAndAnInLoopAssignTargetCollidesAndPersistsTheLoopsLastValue()
    {
        const string content =
            "{% assign shared = \"top-level\" %}" +
            "{% for item in Items %}{% assign shared = item.Name %}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Items", new List<object> { new { Name = "last-item" } });

        CreateExpander(variables).Expand(content, "test.http");

        variables.GetVariable<string>("shared").Should().Be("last-item");
    }

    [Fact]
    public void MakeATopLevelAssignInTheHttpSectionOfATpFileVisibleAfterExpansionForTheTestSectionToRead()
    {
        const string tpFileContent =
            "--- INIT\n" +
            "tp.SetVariable(\"Products\", new[] { new { Name = \"Widget\" } });\n" +
            "\n" +
            "--- HTTP\n" +
            "{% assign ProductCount = Products.size %}\n" +
            "### Seed products\n" +
            "POST {{ApiBaseUrl}}/posts\n" +
            "\n" +
            "--- TEST\n" +
            "var count = tp.GetVariable<int>(\"ProductCount\");\n" +
            "\n" +
            "--- END";

        var context = new global::TeaPie.TestCases.TpParsingContext(tpFileContent, "cross-section-test");
        new global::TeaPie.TestCases.TpFileParser().Parse(context);
        var httpSection = context.Definitions[0].HttpContent;

        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Products", new List<object> { new { Name = "Widget" } });

        CreateExpander(variables).Expand(httpSection, "cross-section-test.tp");

        variables.GetVariable<decimal>("ProductCount").Should().Be(1m);
    }

    [Fact]
    public void ShareOneRenderStepBudgetAcrossMultipleSiblingLoopsInTheSameFileWithoutExceedingIt()
    {
        const string content =
            "{% for a in ItemsA %}{{ a.Name }}{% endfor %}" +
            "{% for b in ItemsB %}{{ b.Name }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("ItemsA", Enumerable.Range(1, 50).Select(i => (object)new { Name = $"A{i}" }).ToList());
        variables.SetVariable("ItemsB", Enumerable.Range(1, 50).Select(i => (object)new { Name = $"B{i}" }).ToList());

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowAParseErrorWithARawTagHintWhenBodyContainsUnknownFluidSyntax()
    {
        const string content = "{\"template\": \"Hello {% name %}!\"}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*wrap it in*raw*");
    }

    [Fact]
    public void RenderSuccessfullyWhenBodyCoincidentallyContainsValidFluidSyntax()
    {
        const string content = "{\"tpl\":\"{% if user %}hi{% endif %}\"}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "test.http");

        act.Should().NotThrow();
    }

    [Fact]
    public void EagerlyResolveALoopsSourceEvenWhenItIsGuardedByAFalseTopLevelIfCondition()
    {
        const string content = "{% if SeedEnabled %}{% for p in SeededPartners %}{{ p.Name }}{% endfor %}{% endif %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("SeedEnabled", false);

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*collection variable 'SeededPartners'*was not found*");
    }

    [Theory]
    [InlineData(
        "{% for p in SeededPartners %}{{ p.Name }}{% endfor %}",
        "collection variable 'SeededPartners'", "was not found")]
    [InlineData(
        "{% for p in NotACollection %}{{ p }}{% endfor %}",
        "variable 'NotACollection'", "must be a collection")]
    [InlineData(
        "{% for s in (\"a\",) %}{{ s }}{% endfor %}",
        "inline collection", "empty item")]
    public void IncludeTheRequestFilePathWhenACollectionSourceFailsToResolve(
        string content, string firstDetail, string secondDetail)
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("NotACollection", 42);

        var act = () => CreateExpander(variables).Expand(content, "loops/partners-req.http");

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("loops/partners-req.http");
        exception.Message.Should().Contain(firstDetail);
        exception.Message.Should().Contain(secondDetail);
    }

    [Fact]
    public void ExpandANestedLoopWhoseInnerSourceIsAMemberOfTheOuterLoopVariable()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[]
        {
            new { Name = "Acme", Licenses = new[] { "BASIC", "PRO" } },
            new { Name = "Globex", Licenses = new[] { "BASIC" } }
        });
        const string content =
            "{% for company in Companies %}{% for license in company.Licenses %}" +
            "[{{ company.Name }}:{{ license }}]" +
            "{% endfor %}{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[Acme:BASIC][Acme:PRO][Globex:BASIC]");
    }

    [Fact]
    public void NotThrowCollectionVariableNotFoundForAnInnerLoopSourceDerivedFromTheOuterLoopVariable()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[] { new { Licenses = new string[0] } });
        const string content =
            "{% for company in Companies %}{% for license in company.Licenses %}{{ license }}{% endfor %}{% endfor %}";

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().NotThrow();
    }

    [Fact]
    public void RenderNothingForAnOuterItemWhoseNestedInnerCollectionIsEmptyWithoutThrowing()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[]
        {
            new { Name = "Empty", Licenses = new string[0] },
            new { Name = "Acme", Licenses = new[] { "BASIC" } }
        });
        const string content =
            "{% for company in Companies %}{% for license in company.Licenses %}" +
            "[{{ company.Name }}:{{ license }}]{% endfor %}{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[Acme:BASIC]");
    }

    [Fact]
    public void ThrowWhenAnInnerDynamicLoopSourceIsNotACollection()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[] { new { Name = "Acme", Licenses = "BASIC" } });
        const string content =
            "{% for company in Companies %}{% for license in company.Licenses %}" +
            "[{{ company.Name }}:{{ license }}]{% endfor %}{% endfor %}";

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*variable 'company.Licenses'*must be a collection*");
    }

    [Fact]
    public void ThrowWhenAnInnerDynamicLoopSourceIsNotACollectionForALaterOuterItem()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[]
        {
            new { Name = "Acme", Licenses = new[] { "BASIC" } },
            new { Name = "Globex", Licenses = "NOT-A-LIST" }
        });
        const string content =
            "{% for company in Companies %}{% for license in company.Licenses %}" +
            "[{{ company.Name }}:{{ license }}]{% endfor %}{% endfor %}";

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*variable 'company.Licenses'*must be a collection*company[1]*");
    }

    [Fact]
    public void ThrowWhenAnInnerDynamicLoopSourcePropertyIsNull()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[] { new { Name = "Acme", Licenses = (string[]?)null } });
        const string content =
            "{% for company in Companies %}{% for license in company.Licenses %}" +
            "[{{ company.Name }}:{{ license }}]{% endfor %}{% endfor %}";

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*variable 'company.Licenses'*must be a collection*");
    }

    [Fact]
    public void ThrowForTheUnsupportedRequiredLoopOption()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[] { new { Name = "Acme", Licenses = new[] { "BASIC" } } });
        const string content =
            "{% for company in Companies %}{% for license in company.Licenses required %}" +
            "{{ license }}{% endfor %}{% endfor %}";

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*test.http*unsupported option 'required'*'limit:', 'offset:' and 'reversed'*");
    }

    [Fact]
    public void ResolveTeaPieVariablesUsedAsIndexerInAnInnerDynamicLoopSource()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[]
        {
            new { Name = "Acme", Groups = new Dictionary<string, string[]> { ["basic"] = ["A", "B"] } }
        });
        variables.SetVariable("GroupKey", "basic");
        const string content =
            "{% for company in Companies %}{% for license in company.Groups[GroupKey] %}" +
            "[{{ license }}]{% endfor %}{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[A][B]");
    }

    private static string FormatRequest(string name)
        => $"### {name}\nGET https://example.test/{name}\n\n";

    [Fact]
    public void RenderSuccessfullyWhenANestedLoopTreesCombinedRequestCountStaysWithinMaxExpandedRequests()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Outers", Enumerable.Range(1, 10)
            .Select(_ => (object)new { Items = Enumerable.Range(1, 10).ToList() }).ToList());
        const string content =
            "{% for outer in Outers %}{% for item in outer.Items %}" +
            "### r{{ forloop.index }}\nGET https://example.test/{{ item }}\n\n" +
            "{% endfor %}{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Split("###", StringSplitOptions.None).Length.Should().Be(101);
        result.Should().NotContain("\u0000");
    }

    [Fact]
    public void ThrowWhenANestedLoopTreesCombinedRequestCountExceedsMaxExpandedRequests()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Outers", Enumerable.Range(1, 50)
            .Select(_ => (object)new { Items = Enumerable.Range(1, 30).ToList() }).ToList());
        const string content =
            "{% for outer in Outers %}{% for item in outer.Items %}" +
            "### r{{ forloop.index }}\nGET https://example.test/{{ item }}\n\n" +
            "{% endfor %}{% endfor %}";

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*1500*1000*Outers*");
    }

    [Fact]
    public void AllowAStandaloneLoopSiblingToACappedNestedTreeToStillReachMaxExpandedRequestsIndependently()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Outers", Enumerable.Range(1, 10)
            .Select(_ => (object)new { Items = Enumerable.Range(1, 10).ToList() }).ToList());
        var content =
            "{% for outer in Outers %}{% for item in outer.Items %}" +
            FormatRequest("nested{{ forloop.index }}") +
            "{% endfor %}{% endfor %}" +
            "{% for i in (1..1000) %}" + FormatRequest("standalone{{ i }}") + "{% endfor %}";

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().NotThrow();
    }

    [Fact]
    public void RenderEmptyOutputWithoutThrowingWhenANestedLoopTreeIsInsideAFalseTopLevelIfCondition()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Flag", false);
        variables.SetVariable("Outers", new object[] { new { Items = new[] { "x" } } });
        const string content =
            "{% if Flag %}{% for outer in Outers %}{% for item in outer.Items %}" +
            "[{{ item }}]{% endfor %}{% endfor %}{% endif %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be(string.Empty);
    }

    [Fact]
    public void ExposeTheOuterLoopIndexInsideANestedLoopBodyViaAssignWorkaround()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Outers", new object[]
        {
            new { Items = new[] { "x", "y" } },
            new { Items = new[] { "z" } }
        });
        const string content =
            "{% for outer in Outers %}{% assign outerIndex = forloop.index %}" +
            "{% for item in outer.Items %}" +
            "[{{ outerIndex }}.{{ forloop.index }}:{{ item }}]" +
            "{% endfor %}{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[1.1:x][1.2:y][2.1:z]");
    }

    [Fact]
    public void SupportAnIfConditionInsideANestedInnerLoopBody()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Outers", new object[]
        {
            new { Items = new[] { "keep", "skip" } }
        });
        const string content =
            "{% for outer in Outers %}{% for item in outer.Items %}" +
            "{% if item == \"keep\" %}[{{ item }}]{% endif %}" +
            "{% endfor %}{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[keep]");
    }

    [Fact]
    public void RenderTheOuterLoopIndexInsideAnAtNameStyleTokenInANestedLoopViaAssignWorkaround()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Outers", new object[] { new { Items = new[] { "a", "b" } } });
        const string content =
            "{% for outer in Outers %}{% assign outerIndex = forloop.index %}" +
            "{% for item in outer.Items %}" +
            "@name Create{{ outerIndex }}_{{ forloop.index }}" +
            "{% endfor %}{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Contain("@name Create1_1").And.Contain("@name Create1_2");
    }

    [Fact]
    public void NotPersistAnInLoopAssignInsideANestedInnerLoopToIVariables()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Outers", new object[] { new { Items = new[] { "a" } } });
        const string content =
            "{% for outer in Outers %}{% for item in outer.Items %}" +
            "{% assign flag = true %}{{ flag }}" +
            "{% endfor %}{% endfor %}";

        CreateExpander(variables).Expand(content, "test.http");

        variables.ContainsVariable("flag").Should().BeFalse();
    }

    [Fact]
    public void RenderThreeLevelsOfNestedLoopsWithDynamicSourcesAtEveryLevel()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Regions", new object[]
        {
            new
            {
                Name = "EU",
                Countries = new object[]
                {
                    new { Name = "SK", Cities = new[] { "Bratislava", "Kosice" } }
                }
            }
        });
        const string content =
            "{% for region in Regions %}" +
            "{% for country in region.Countries %}" +
            "{% for city in country.Cities %}" +
            "{% assign marker = true %}" +
            "[{{ region.Name }}/{{ country.Name }}/{{ city }}]" +
            "{% endfor %}" +
            "{% endfor %}" +
            "{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[EU/SK/Bratislava][EU/SK/Kosice]");
        variables.ContainsVariable("marker").Should().BeFalse();
    }

    private const string FourLevelContent =
        "{% for region in Regions %}" +
        "{% for country in region.Countries %}" +
        "{% for city in country.Cities %}" +
        "{% for street in city.Streets %}" +
        "[{{ region.Name }}/{{ country.Name }}/{{ city.Name }}/{{ street }}]" +
        "{% endfor %}" +
        "{% endfor %}" +
        "{% endfor %}" +
        "{% endfor %}";

    private static object[] CreateFourLevelRegions(string[] lastCityStreets)
        =>
        [
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
                            new { Name = "Brno", Streets = lastCityStreets }
                        }
                    }
                }
            }
        ];

    [Fact]
    public void RenderFourLevelsOfNestedLoopsWithDynamicSources()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Regions", CreateFourLevelRegions(["Masarykova"]));

        var result = CreateExpander(variables).Expand(FourLevelContent, "test.http");

        result.Should().Be("[EU/SK/BA/Main][EU/CZ/Praha/Narodni][EU/CZ/Brno/Masarykova]");
    }

    [Fact]
    public void LogTheFullOuterPathWhenASourceFourLevelsDeepIsEmpty()
    {
        var logger = new RecordingLogger<TemplateExpander>();
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Regions", CreateFourLevelRegions([]));

        var result = CreateExpander(variables, logger: logger).Expand(FourLevelContent, "test.http");

        result.Should().Be("[EU/SK/BA/Main][EU/CZ/Praha/Narodni]");
        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Debug
            && entry.Message.Contains("'city.Streets'")
            && entry.Message.Contains("region[0].country[1].city[1]"));
    }

    [Fact]
    public void ThrowWithTheFullOuterPathWhenAThirdLevelDynamicSourceIsNotACollection()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Regions", new object[]
        {
            new
            {
                Name = "EU",
                Countries = new object[]
                {
                    new { Name = "SK", Cities = new[] { "BA" } },
                    new { Name = "CZ", Cities = "Praha" }
                }
            }
        });
        const string content =
            "{% for region in Regions %}{% for country in region.Countries %}{% for city in country.Cities %}" +
            "[{{ city }}]{% endfor %}{% endfor %}{% endfor %}";

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*variable 'country.Cities'*must be a collection*(region[0].country[1])*");
    }

    [Fact]
    public void LogTheOuterPathOfAThirdLevelSourceThatReferencesAGrandparentLoopVariable()
    {
        var logger = new RecordingLogger<TemplateExpander>();
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[]
        {
            new { Name = "Acme", Users = new[] { "u1" }, Roles = new string[0] }
        });
        const string content =
            "{% for company in Companies %}{% for user in company.Users %}" +
            "{% for role in company.Roles %}[{{ user }}:{{ role }}]{% endfor %}" +
            "{% endfor %}{% endfor %}";

        CreateExpander(variables, logger: logger).Expand(content, "test.http");

        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Debug
            && entry.Message.Contains("'company.Roles'")
            && entry.Message.Contains("company[0].user[0]"));
    }

    [Fact]
    public void RenderOnlyTheOuterItemsWhoseDeepSourceIsNonEmpty()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Regions", new object[]
        {
            new
            {
                Name = "EU",
                Countries = new object[]
                {
                    new { Name = "SK", Cities = new object[] { new { Name = "BA", Streets = new string[0] } } },
                    new { Name = "CZ", Cities = new object[] { new { Name = "Brno", Streets = new[] { "Hlavni" } } } }
                }
            }
        });
        const string content =
            "{% for region in Regions %}{% for country in region.Countries %}" +
            "{% for city in country.Cities %}{% for street in city.Streets %}" +
            "[{{ country.Name }}/{{ street }}]" +
            "{% endfor %}{% endfor %}{% endfor %}{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[CZ/Hlavni]");
    }

    [Fact]
    public void ResolveTeaPieVariablesUsedAsIndexerInAThirdLevelDynamicSource()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Regions", new object[]
        {
            new
            {
                Countries = new object[]
                {
                    new { Name = "SK", Groups = new Dictionary<string, string[]> { ["big"] = ["BA", "KE"] } }
                }
            }
        });
        variables.SetVariable("GroupKey", "big");
        const string content =
            "{% for region in Regions %}{% for country in region.Countries %}" +
            "{% for city in country.Groups[GroupKey] %}[{{ country.Name }}/{{ city }}]" +
            "{% endfor %}{% endfor %}{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[SK/BA][SK/KE]");
    }

    private static global::TeaPie.Variables.Variables CreateCompanyVariables()
    {
        var variables = new global::TeaPie.Variables.Variables();
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
                Licenses = new string[0],
                Groups = new Dictionary<string, string[]> { ["basic"] = ["GB"], ["none"] = [] }
            }
        });
        return variables;
    }

    private static List<(LogLevel Level, string Message)> ExpandAndCollectLogEntries(
        string content, global::TeaPie.Variables.Variables variables)
    {
        var logger = new RecordingLogger<TemplateExpander>();
        CreateExpander(variables, logger: logger).Expand(content, "requests/seed.http");
        return logger.Entries;
    }

    [Fact]
    public void ThrowAClearErrorForAFilterInAnInnerLoopSource()
    {
        const string content =
            "{% for company in Companies %}{% for license in company.Licenses | first %}" +
            "{{ license }}{% endfor %}{% endfor %}";

        var act = () => CreateExpander(CreateCompanyVariables()).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*test.http*filters are not supported*'company.Licenses | first'*");
    }

    [Fact]
    public void ThrowForAMissingInnerSourceProperty()
    {
        const string content =
            "{% for company in Companies %}{% for license in company.Nope %}{{ license }}{% endfor %}{% endfor %}";

        var act = () => CreateExpander(CreateCompanyVariables()).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*test.http*'Nope' is undefined*");
    }

    [Fact]
    public void RenderTheFluidElseBranchForAnEmptyInnerSource()
    {
        const string content =
            "{% for company in Companies %}{% for license in company.Licenses %}{{ license }}" +
            "{% else %}[{{ company.Name }}:none]{% endfor %}{% endfor %}";

        var result = CreateExpander(CreateCompanyVariables()).Expand(content, "test.http");

        result.Should().Be("A1A2A3[Globex:none]");
    }

    [Fact]
    public void ApplyLimitOffsetAndReversedOnATopLevelLoop()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Items", new[] { 1, 2, 3, 4, 5 });
        const string content = "{% for i in Items offset: 1 limit: 3 reversed %}{{ i }}{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("432");
    }

    [Fact]
    public void ApplyLimitOffsetAndReversedOnATopLevelNumericRange()
    {
        const string content = "{% for i in (1..5) limit: 2 reversed %}{{ i }}{% endfor %}";

        var result = CreateExpander().Expand(content, "test.http");

        result.Should().Be("21");
    }

    [Fact]
    public void ApplyLimitAndReversedOnAnInnerDynamicLoop()
    {
        const string content =
            "{% for company in Companies limit: 1 %}{% for license in company.Licenses limit: 2 reversed %}" +
            "{{ license }}{% endfor %}{% endfor %}";

        var result = CreateExpander(CreateCompanyVariables()).Expand(content, "test.http");

        result.Should().Be("A2A1");
    }

    [Fact]
    public void ApplyAVariableLimitOnAnInnerDynamicLoop()
    {
        var variables = CreateCompanyVariables();
        variables.SetVariable("MaxLicenses", 1);
        const string content =
            "{% for company in Companies %}{% for license in company.Licenses limit: MaxLicenses %}" +
            "{{ license }}{% endfor %}{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("A1");
    }

    [Fact]
    public void HonorALiteralLimitWhenCheckingTheTopLevelExpansionMaximum()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Items", Enumerable.Range(1, 10).ToList());
        var limits = new TemplatingLimits { MaxExpandedRequests = 3 };
        const string content = "{% for i in Items limit: 3 %}{{ i }}{% endfor %}";

        var result = CreateExpander(variables, limits).Expand(content, "test.http");

        result.Should().Be("123");
    }

    [Fact]
    public void StillEnforceTheTopLevelExpansionMaximumWhenTheLimitIsTooLarge()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Items", Enumerable.Range(1, 10).ToList());
        var limits = new TemplatingLimits { MaxExpandedRequests = 3 };
        const string content = "{% for i in Items limit: 4 %}{{ i }}{% endfor %}";

        var act = () => CreateExpander(variables, limits).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*loop over 'Items' would expand to 4 requests*");
    }

    [Theory]
    [InlineData("reversed")]
    [InlineData("offset: 1")]
    public void ReportTheSourcePositionOfAReversedOrOffsetOuterItemInTheLogPath(string options)
    {
        var content =
            $"{{% for company in Companies {options} %}}{{% for license in company.Licenses %}}" +
            "{{ license }}{% endfor %}{% endfor %}";

        var entries = ExpandAndCollectLogEntries(content, CreateCompanyVariables());

        entries.Should().ContainSingle(entry => entry.Message.Contains("for company[1],"));
    }

    [Theory]
    [InlineData("offset: 1 reversed")]
    [InlineData("reversed offset: Skip")]
    [InlineData("offset: 1 limit: 2 reversed")]
    public void ReportTheSourcePositionOfAnOffsetAndReversedOuterItemInTheLogPath(string options)
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Skip", 1);
        variables.SetVariable("Companies", new object[]
        {
            new { Licenses = new[] { "A1" } },
            new { Licenses = new[] { "B1" } },
            new { Licenses = new string[0] },
            new { Licenses = new[] { "D1" } }
        });
        var content =
            $"{{% for company in Companies {options} %}}{{% for license in company.Licenses %}}" +
            "{{ license }}{% endfor %}{% endfor %}";

        var entries = ExpandAndCollectLogEntries(content, variables);

        entries.Should().ContainSingle(entry => entry.Message.Contains("for company[2],"));
    }

    [Fact]
    public void RecognizeAnIndexerDirectlyOnTheLoopVariableAsADynamicSource()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[]
        {
            new Dictionary<string, object> { ["Licenses"] = new[] { "A1" } }
        });
        variables.SetVariable("Key", "Licenses");
        const string content =
            "{% for company in Companies %}{% for license in company[Key] %}{{ license }}{% endfor %}" +
            "{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("A1");
    }

    [Fact]
    public void RenderAnIndexerDirectlyOnTheLoopVariableInsideTheLoopBody()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[] { new Dictionary<string, object> { ["Name"] = "Acme" } });
        variables.SetVariable("Key", "Name");
        const string content = "{% for company in Companies %}[{{ company[Key] }}]{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[Acme]");
    }

    [Fact]
    public void SeeATopLevelAssignUsedAsAnIndexerInsideAnInnerLoopSource()
    {
        const string content =
            "{% assign group = 'basic' %}{% for company in Companies %}" +
            "{% for license in company.Groups[group] %}{{ license }}{% endfor %}{% endfor %}";

        var result = CreateExpander(CreateCompanyVariables()).Expand(content, "test.http");

        result.Should().Be("ABGB");
    }

    [Fact]
    public void SeeAnIndexerAssignedInsideTheOuterLoopBodyInAnInnerLoopSource()
    {
        const string content =
            "{% for company in Companies %}{% assign group = 'none' %}" +
            "{% for license in company.Groups[group] %}{{ license }}{% endfor %}{% endfor %}";

        var entries = ExpandAndCollectLogEntries(content, CreateCompanyVariables());

        entries.Should().Contain(entry =>
            entry.Level == LogLevel.Debug && entry.Message.Contains("'company.Groups[group]'")
            && entry.Message.Contains("for company[0],"));
    }

    [Fact]
    public void KeepRejectingATopLevelAssignAsALoopSource()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Root", new { Children = new[] { "c1" } });
        const string content =
            "{% assign items = Root %}{% for item in items.Children %}{{ item }}{% endfor %}";

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*collection variable 'items.Children'*was not found*");
    }

    [Fact]
    public void PreserveWhitespaceControlOnAnInnerLoopThatGetsAGuard()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[] { new { Licenses = new[] { "A1", "A2" } } });
        const string content =
            "{% for company in Companies %}\n  {%- for license in company.Licenses -%}\n" +
            "  [{{ license }}]\n  {%- endfor -%}\n{% endfor %}";
        const string withoutGuard =
            "{% for company in Companies %}\n  {%- for license in (\"A1\", \"A2\") -%}\n" +
            "  [{{ license }}]\n  {%- endfor -%}\n{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be(CreateExpander(variables).Expand(withoutGuard, "test.http"));
    }

    [Fact]
    public void LogOnlyAtDebugLevelWhenAnInnerSourceIsEmptyForSomeOuterItems()
    {
        var logger = new RecordingLogger<TemplateExpander>();
        const string content =
            "{% for company in Companies %}{% for license in company.Licenses %}{{ license }}{% endfor %}{% endfor %}";

        CreateExpander(CreateCompanyVariables(), logger: logger).Expand(content, "requests/seed.http");

        logger.Entries.Should().ContainSingle().Which.Should().Match<(LogLevel Level, string Message)>(entry =>
            entry.Level == LogLevel.Debug
            && entry.Message.Contains("'company.Licenses'")
            && entry.Message.Contains("requests/seed.http")
            && entry.Message.Contains("company[1]"));
    }

    [Fact]
    public void WarnWhenAnInnerSourceIsEmptyForEveryOuterItem()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[]
        {
            new { Name = "New1", Customers = new string[0] },
            new { Name = "New2", Customers = new string[0] }
        });
        const string content =
            "{% for company in Companies %}{% for customer in company.Customers %}" +
            "### Add customer\nPOST https://example.test/{{ customer }}\n{% endfor %}{% endfor %}";

        var entries = ExpandAndCollectLogEntries(content, variables);

        entries.Where(entry => entry.Level == LogLevel.Warning).Should().ContainSingle().Which.Message
            .Should().Contain("'company.Customers'").And.Contain("requests/seed.http")
            .And.Contain("every one of its 2 outer item(s)");
        entries.Count(entry => entry.Level == LogLevel.Debug).Should().Be(2);
    }

    [Fact]
    public void WarnAboutADeepSourceEmptyForEveryOuterItemAcrossAllNestingLevels()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Regions", new object[]
        {
            new { Countries = new object[] { new { Cities = new object[] { new { Streets = new string[0] } } } } }
        });
        const string content =
            "{% for region in Regions %}{% for country in region.Countries %}{% for city in country.Cities %}" +
            "{% for street in city.Streets %}{{ street }}{% endfor %}{% endfor %}{% endfor %}{% endfor %}";

        var entries = ExpandAndCollectLogEntries(content, variables);

        entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("'city.Streets'"));
    }

    [Theory]
    [InlineData("{% for i in Items offset: 10 %}### R{{ i }}\nPOST https://example.test/{{ i }}\n{% endfor %}")]
    [InlineData("{% for i in Items limit: Zero %}### R{{ i }}\nPOST https://example.test/{{ i }}\n{% endfor %}")]
    [InlineData("{% for i in Items %}{% if i > 99 %}### R{{ i }}\nPOST https://example.test/{{ i }}\n{% endif %}" +
        "{% endfor %}")]
    public void WarnWhenALoopWhoseBodyDeclaresRequestsGeneratesNone(string content)
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Items", new[] { 1, 2, 3 });
        variables.SetVariable("Zero", 0);

        var entries = ExpandAndCollectLogEntries(content, variables);

        entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains("'Items'")
            && entry.Message.Contains("requests/seed.http")
            && entry.Message.Contains("generated no requests"));
    }

    [Fact]
    public void NotWarnAboutALoopInsideARequestBodyThatRendersNothing()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Items", new[] { 1, 2, 3 });
        const string content =
            "### Create\nPOST https://example.test/items\n\n[{% for i in Items limit: 0 %}{{ i }}{% endfor %}]\n";

        var entries = ExpandAndCollectLogEntries(content, variables);

        entries.Should().BeEmpty();
    }

    [Fact]
    public void WarnOnlyOnceWhenAnInnerSourceEmptyForEveryOuterItemLeavesTheWholeTreeWithoutRequests()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[] { new { Customers = new string[0] } });
        const string content =
            "{% for company in Companies %}{% for customer in company.Customers %}" +
            "### Add\nPOST https://example.test/{{ customer }}\n{% endfor %}{% endfor %}";

        var entries = ExpandAndCollectLogEntries(content, variables);

        entries.Where(entry => entry.Level == LogLevel.Warning).Should().ContainSingle()
            .Which.Message.Should().Contain("'company.Customers'");
    }

    [Fact]
    public void NotLogAnythingWhenEveryInnerSourceIsNonEmpty()
    {
        var logger = new RecordingLogger<TemplateExpander>();
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[] { new { Licenses = new[] { "A1" } } });
        const string content =
            "{% for company in Companies %}{% for license in company.Licenses %}{{ license }}{% endfor %}{% endfor %}";

        CreateExpander(variables, logger: logger).Expand(content, "test.http");

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public void NotLeakInternalGuardVariablesIntoTeaPieVariables()
    {
        var variables = CreateCompanyVariables();
        const string content =
            "{% for company in Companies %}{% for license in company.Licenses %}{{ license }}{% endfor %}{% endfor %}";

        CreateExpander(variables).Expand(content, "test.http");

        variables.ContainsVariable("__teapie_loop_guard").Should().BeFalse();
        variables.ContainsVariable("__teapie_loop_index_0").Should().BeFalse();
    }

    [Fact]
    public void ReassignAnOuterLoopScopedAssignFreshOnEveryOuterIteration()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Outers", new object[]
        {
            new { Name = "first", Items = new[] { "x" } },
            new { Name = "second", Items = new[] { "y" } }
        });
        const string content =
            "{% for outer in Outers %}{% assign label = outer.Name %}" +
            "{% for item in outer.Items %}[{{ label }}:{{ item }}]{% endfor %}{% endfor %}";

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[first:x][second:y]");
    }

    [Fact]
    public void PropagateAnErrorFromAnInnerIterationAsAFailureOfTheWholeNestedLoop()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Outers", new object[]
        {
            new { Items = new object[] { new { Name = "ok" } } },
            new { Items = new object[] { new { Name = "also-ok" } } }
        });
        const string content =
            "{% for outer in Outers %}{% for item in outer.Items %}{{ item.TypoField }}{% endfor %}{% endfor %}";

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*TypoField*");
    }

    [Fact]
    public void ReportPositionAndSourceFromTheOriginalFileForAFluidParseErrorOnTheFirstLine()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" } });
        const string content = "{% for tenant in Tenants %}{{ NotInScope }}{% badtag %}{% endfor %}";
        var expected = FindOriginalPosition(content, "{% badtag %}");

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("test.http");
        ExtractReportedPosition(exception.Message).Should().Be(expected);
        exception.Message.Should().Contain("Source:\n" + content);
        exception.Message.Should().NotContain("__teapie_loop_");
    }

    [Fact]
    public void ReportTheCorrectLineFromTheOriginalFileForAFluidParseErrorAfterSeveralLines()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" } });
        const string content =
            "### Setup\n" +
            "POST {{ApiGatewayBaseUrl}}/init\n\n" +
            "{% for tenant in Tenants %}\n" +
            "{{ NotInScope }}\n" +
            "{% badtag %}\n" +
            "{% endfor %}";
        var expected = FindOriginalPosition(content, "{% badtag %}");

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("test.http");
        ExtractReportedPosition(exception.Message).Should().Be(expected);
        exception.Message.Should().Contain("Source:\n{% badtag %}");
    }

    [Fact]
    public void ReportTheCorrectPositionAndSourceForAFluidParseErrorOnALineAffectedByLoopMasking()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { Name = "Acme" } });
        const string content =
            "### Setup\n" +
            "POST {{ApiGatewayBaseUrl}}/init\n\n" +
            "{% for tenant in Tenants %}\n" +
            "{{ NotInScope }}{% badtag %}\n" +
            "{% endfor %}";
        var expected = FindOriginalPosition(content, "{% badtag %}");
        const string originalLine = "{{ NotInScope }}{% badtag %}";

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("test.http");
        ExtractReportedPosition(exception.Message).Should().Be(expected);
        exception.Message.Should().Contain("Source:\n" + originalLine);
        exception.Message.Should().NotContain("__teapie_loop_");
    }

    [Fact]
    public void ReportTheOriginalFileNameAndPositionForAFluidParseErrorInsideANestingRootLoop()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Outers", new List<object> { new { Items = new List<object> { new { Name = "x" } } } });
        const string content =
            "{% for outer in Outers %}{% for inner in outer.Items %}{{ inner.Name }}{% endfor %}" +
            "{% badtag %}{% endfor %}";
        var expected = FindOriginalPosition(content, "{% badtag %}");

        var act = () => CreateExpander(variables).Expand(content, "requests/scenario.http");

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("requests/scenario.http");
        ExtractReportedPosition(exception.Message).Should().Be(expected);
        exception.Message.Should().Contain("Source:\n" + content);
        exception.Message.Should().NotContain("__teapie_loop_");
    }
}
