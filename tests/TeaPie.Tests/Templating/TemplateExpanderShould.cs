using FluentAssertions;
using NSubstitute;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class TemplateExpanderShould
{
    private static TemplateExpander CreateExpander(global::TeaPie.Variables.IVariables? variables = null)
    {
        var vars = variables ?? new global::TeaPie.Variables.Variables();
        return new TemplateExpander(
            new LoopBlockScanner(),
            new LoopBodyMasker(),
            new CollectionSourceResolver(vars),
            new VariablesFluidModelBuilder(),
            vars);
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
    public void KeepStepBudgetsIndependentAcrossMultipleLoopBlocksInOneFile()
    {
        var heavyTag = string.Concat(Enumerable.Repeat("{{ i }}", 100));
        var content =
            $"{{% for i in (1..1000) %}}{heavyTag}{{% endfor %}}" +
            "mid" +
            $"{{% for j in (1..1000) %}}{heavyTag}{{% endfor %}}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "test.http");

        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowWhenResolvedItemCountDisagreesWithTheActuallyRenderedCollection()
    {
        const string content = "{% for x in Weird %}[{{ x }}]{% endfor %}";
        var resolver = Substitute.For<ICollectionSourceResolver>();
        resolver.Resolve("Weird").Returns(new LoopSource(new List<object>(), 3));
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), resolver, new VariablesFluidModelBuilder(), new global::TeaPie.Variables.Variables());

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*resolved 3 item(s) but rendered empty output*");
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

    // Fluid 2.31.0's {% assign %} does not route an undefined bare-name right-hand side through
    // TemplateOptions.Undefined the way {{ }} interpolation does — it silently yields nil. The throw
    // observed here therefore comes from the unrelated "rendered empty output" guard, which only fires
    // because this loop body has no other literal text. See the companion test below for the real behavior.
    [Fact]
    public void FailWithAGenericGuardMessageWhenAnAssignRightHandSideIsUndefinedAndTheEntireBodyRendersEmpty()
    {
        const string content = "{% for tenant in Tenants %}{% assign greeting = NoSuchVariable %}{{ greeting }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        // Pin the guard message explicitly: a bare Throw<InvalidOperationException>() here would
        // also pass if the throw came from a genuine Undefined error, hiding the gap this documents.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*rendered empty output*")
            .Which.Message.Should().NotContain("NoSuchVariable");
    }

    [Fact]
    public void SilentlyRenderEmptyWithoutThrowingWhenAnAssignRightHandSideIsUndefinedAndTheBodyHasSurroundingLiteralText()
    {
        // Known limitation (Fluid 2.31.0): once there is any other literal text in the loop body,
        // the "rendered empty output" guard no longer fires, so a typo'd assign RHS name silently
        // renders as an empty string with no error at all. Deferred to Step B (spec §10), which
        // already owns the general "Undefined routing inside {% %} tag expressions" question.
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
        // TeaPie variable names may contain '.' (e.g. "Temp.FreePartners"), which Fluid parses as
        // member access rather than as one bare identifier. The bridge model is keyed by the literal
        // dotted name, so Fluid looks for a member "FreePartners" on a root identifier "Temp" and
        // finds nothing. Verified empirically: this does NOT surface as a TemplateOptions.Undefined
        // error naming "Temp" — Fluid 2.31.0's {% assign %} does not route an undefined right-hand
        // side through Undefined at all (see the undefined-name tests above), so the assignment
        // silently yields nil and renders as an empty string.
        // Known, documented limitation (spec §7 Step 0); not fixed by this step.
        const string content = "{% for tenant in Tenants %}[{% assign x = Temp.FreePartners %}{{ x }}]{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        variables.SetVariable("Temp.FreePartners", new List<string> { "a" });

        var result = CreateExpander(variables).Expand(content, "test.http");

        result.Should().Be("[]");
    }

    [Fact]
    public void FailWithAGuardMessageThatNeverNamesTheDottedVariableWhenADottedAssignRightHandSideRendersAnEmptyBody()
    {
        // Companion to the test above: with no other literal text in the body, the failure that does
        // surface is the generic "rendered empty output" guard, whose message never mentions the
        // dotted variable the author actually meant — so the diagnostic is unhelpful, not merely
        // confusingly named.
        const string content = "{% for tenant in Tenants %}{% assign x = Temp.FreePartners %}{{ x }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Tenants", new List<object> { new { } });
        variables.SetVariable("Temp.FreePartners", new List<string> { "a" });

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*rendered empty output*")
            .Which.Message.Should().NotContain("FreePartners");
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
}
