using FluentAssertions;
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

    // Was: KeepStepBudgetsIndependentAcrossMultipleLoopBlocksInOneFile, asserting NotThrow(). The old
    // per-block splice-and-render architecture gave each loop block its own fresh 200,000-step render
    // budget, so two loops of 1000 x 100 tags each (~100,000 render steps apiece - see
    // ThrowWhenRenderStepsExceedTheMaximum above, where 1000 x 205 tags is enough to exceed 200,000,
    // confirming a rough ~1-step-per-{{ }}-expression-per-iteration calibration) stayed independently
    // under the cap. The whole-file single-render rewrite intentionally shares ONE 200,000-step budget
    // across the entire file (an already-accepted, documented trade-off - no realistic file gets
    // remotely close to this limit). Under the shared budget, this file's combined ~200,000 render
    // steps (just over the cap, once loop/iteration overhead beyond the raw tag count is added in) now
    // exceeds it, so this documents the new, correct, shared-budget behavior instead.
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

    // Was: ThrowWhenResolvedItemCountDisagreesWithTheActuallyRenderedCollection. The old per-block
    // "rendered empty output" guard compared the resolver's reported item count against whether the
    // spliced-in block actually produced output, to catch resolver/render disagreements. The
    // whole-file single-render rewrite removes that guard entirely (it doesn't map onto a single
    // whole-file render, and was already known to misdiagnose legitimately-empty output elsewhere -
    // see the "misdiagnosing guard" test below). A mock resolver reporting ItemCount=3 alongside an
    // actually-empty collection is no longer detected as an error: the collection (empty) drives the
    // render, so this now renders successfully to an empty string.
    [Fact]
    public void RenderEmptyWithoutThrowingWhenResolvedItemCountDisagreesWithTheActuallyRenderedCollection()
    {
        const string content = "{% for x in Weird %}[{{ x }}]{% endfor %}";
        var resolver = Substitute.For<ICollectionSourceResolver>();
        resolver.Resolve("Weird").Returns(new LoopSource(new List<object>(), 3));
        var expander = new TemplateExpander(
            new LoopBlockScanner(), new LoopBodyMasker(), resolver, new VariablesFluidModelBuilder(), new global::TeaPie.Variables.Variables());

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
    // TemplateOptions.Undefined the way {{ }} interpolation does — it silently yields nil.
    // Previously (per-block splice-and-render architecture) this loop body having no other literal
    // text meant the block spliced in as empty output, which tripped the old "rendered empty output"
    // guard and threw. The whole-file single-render rewrite removes that guard entirely (it doesn't
    // map onto a single whole-file render, and was already known to misdiagnose legitimately-empty
    // output - see the "misdiagnosing guard" test further below), so this scenario now renders
    // successfully to an empty string instead of throwing.
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
        // Known limitation (Fluid 2.31.0): a typo'd assign RHS name silently renders as an empty
        // string with no error at all. Previously (per-block splice-and-render architecture) this
        // scenario already rendered successfully because the old "rendered empty output" guard only
        // fired when the whole loop body produced no output - the surrounding literal text ("X"/"Y")
        // kept it out of scope. Step E1's whole-file single-render rewrite removed that guard
        // entirely (it doesn't map onto a single whole-file render), so this now renders successfully
        // unconditionally, not just when there's other literal text present - see the sibling test
        // above (RenderEmptyWithoutThrowingWhenAnAssignRightHandSideIsUndefinedAndTheEntireBodyRenders
        // Empty), which previously would have thrown under the old guard and now also renders
        // successfully.
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
    public void RenderEmptyWithoutThrowingWhenADottedAssignRightHandSideRendersAnEmptyBody()
    {
        // Companion to the test above. Previously (per-block splice-and-render architecture), with no
        // other literal text in the body, the failure that surfaced was the generic "rendered empty
        // output" guard, whose message never mentioned the dotted variable the author actually meant.
        // The whole-file single-render rewrite removes that guard entirely (see the comment on
        // RenderEmptyWithoutThrowingWhenAnAssignRightHandSideIsUndefinedAndTheEntireBodyRendersEmpty
        // above), so this now renders successfully to an empty string instead of throwing.
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
        // Unlike dotted names (see the dotted-name test above), a hyphen in a TeaPie variable name
        // does NOT split into a subtraction expression here: Fluid 2.31.0's identifier grammar accepts
        // '-' as a valid identifier character, so "my-var" parses as one bare identifier and resolves
        // against the bridge model exactly like any other name. Verified empirically.
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
        // Fluid 2.31.0's {% if %} evaluates condition truthiness directly and never routes an
        // undefined bare name through TemplateOptions.Undefined (that callback only fires for {{ }}
        // output interpolation). This differs from the spec's original Step B assumption ("condition
        // referencing a name that resolves to neither the loop model nor IVariables still throws via
        // Undefined") — corrected here: it silently evaluates to falsy, matching standard Liquid
        // semantics where nil/undefined is falsy.
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
        // Same root cause as TemplateExpanderShould's assign-side dotted-name characterization
        // (Step A): Fluid parses "Temp.FreePartners" in an expression as member access on a root
        // identifier "Temp", which is absent from the bridge model (keyed by the literal dotted
        // string) — so the condition is falsy, not an error.
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
        // Regression guard: confirm the if/unless work above did not weaken the existing Undefined
        // callback for {{ }} interpolation (Phase 1 behavior) when the branch that references it
        // actually renders. Uses tenant.TypoField (a loop-scoped member access, so LoopBodyMasker
        // does not mask it away) rather than a bare undefined name — a bare name like NoSuchVariable
        // is masked into {% raw %} and never reaches Fluid at all, regardless of if/unless, so it
        // can't regression-test this. Mirrors the existing ThrowWhenLoopItemFieldIsUndefined test.
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

        // Wording updated for the whole-file single-render rewrite: parse errors are no longer
        // attributed to a specific loop block (there is no per-block parse step anymore) - the
        // whole transformed file is parsed once, so the message is now generically "failed to
        // parse template".
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

    // Was: ThrowAMisdiagnosingGuardMessageWhenAnUnlessFiltersOutEveryLoopItem. {% unless %}/{% if %}
    // can legitimately filter out every item in a loop (this is the spec's own motivating use case for
    // adding conditionals — "skip a request if X"), but the old per-block "rendered empty output"
    // guard (added before if/unless existed, to catch masking/naming-collision bugs) could not
    // distinguish that from a real engine bug, so it threw a message that wrongly claimed "a
    // templating engine issue (e.g. a naming collision)" for perfectly correct, fully-filtered output.
    // The whole-file single-render rewrite removes that guard entirely — it doesn't map onto a single
    // whole-file render, and this misfire is exactly why it was already known to be unsound — so this
    // scenario now renders successfully to an empty string instead of throwing a misdiagnosing error.
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

        // Fluid.Core 2.31.0 represents numeric literals internally as NumberValue and unwraps them
        // via FluidValue.ToObjectValue() as System.Decimal, regardless of the literal's own shape -
        // this is a documented type-coercion detail of persisting a Fluid-computed value, not a bug.
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
        // Known, documented name-collision risk (spec S5/S12): reusing the same identifier as both a
        // top-level assign target and an in-loop assign target elsewhere in the file means the
        // TemplateContext.Assigned hook's last-write-wins capture ends up persisting whichever one
        // executed last during the render - here, the loop's final iteration. Avoid reusing names
        // across top-level and in-loop assigns to sidestep this entirely.
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
        // Before this step, each {% for %} block got its own fresh 200,000-step budget (one
        // TemplateContext/render call per block). The whole-file single-render architecture shares
        // ONE 200,000-step budget across every loop and top-level tag in the file combined. Two small
        // sibling loops, each far under the limit individually, must still succeed when combined.
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
        // Removing the old blocks.Count == 0 early return (any file containing "{%" is now fully
        // Fluid-parsed, not just files with recognized {% for %} loops) means a JSON/text body that
        // merely looks like it could contain a Fluid tag, but doesn't use real Fluid syntax, now
        // throws a parse error instead of passing through untouched.
        const string content = "{\"template\": \"Hello {% name %}!\"}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>().WithMessage("*wrap it in*raw*");
    }

    [Fact]
    public void RenderSuccessfullyWhenBodyCoincidentallyContainsValidFluidSyntax()
    {
        // Intentional consequence of Step E1's design, not a bug: per spec §11's approved scope,
        // byte-identical passthrough is only guaranteed for files with NO Fluid tags at all. Any file
        // containing "{%" is now fully Fluid-parsed, so a JSON/text body that coincidentally contains
        // valid-looking Liquid syntax (here, a well-formed {% if %}/{% endif %}) is evaluated as a
        // template and rendered, rather than being left alone as plain text. This is a real, documented
        // behavior difference from before this branch.
        const string content = "{\"tpl\":\"{% if user %}hi{% endif %}\"}";
        var expander = CreateExpander();

        var act = () => expander.Expand(content, "test.http");

        act.Should().NotThrow();
    }

    [Fact]
    public void EagerlyResolveALoopsSourceEvenWhenItIsGuardedByAFalseTopLevelIfCondition()
    {
        // Known limitation (not fixed by nested-loop support either - this is a top-level
        // if/pre-scan-ordering problem, orthogonal to nesting depth. Tracked as a separate
        // follow-up, not part of any currently-planned step): the pre-scan's zero-item/
        // MaxExpandedRequests/source-resolution guards run before any Fluid evaluation, so they don't
        // know a {% for %} sits inside a {% if %} whose condition will be false at render time. This
        // defeats the "skip a request if X" motivating use case (spec S3 OQ#1) for exactly the
        // for-inside-if composition - a real gap, pinned here so it's a tracked decision rather than
        // an undiscovered surprise.
        const string content = "{% if SeedEnabled %}{% for p in SeededPartners %}{{ p.Name }}{% endfor %}{% endif %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("SeedEnabled", false);
        // Note: SeededPartners is deliberately never set.

        var act = () => CreateExpander(variables).Expand(content, "test.http");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*collection variable 'SeededPartners'*was not found*");
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
        // forloop.parentloop does not exist in Fluid.Core (verified via reflection against both
        // the pinned 2.31.0 and the newest published 2.40.0 at the time of checking). The
        // documented workaround: capture the outer loop's index into a variable via
        // {% assign %} before entering the inner loop — an outer-loop assign is visible inside
        // the nested inner loop body (see LoopBodyMaskerShould's nested-scope tests, Task 3).
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
        // Reuses the exact mechanism the pre-existing (single-level)
        // ThrowWhenLoopItemFieldIsUndefined test already proves: an undefined member access
        // inside {{ }} interpolation routes through TemplateOptions.Undefined, which throws.
        // This avoids relying on an unverified assumption about how Fluid's `for` tag treats a
        // null/missing collection (stock Liquid/Fluid renders zero iterations for that case,
        // silently, matching the "empty inner collection" test elsewhere in this plan — that is
        // NOT an error and must not be confused with this test's actual undefined-field trigger).
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
}
