using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class LoopBodyMaskerShould
{
    [Fact]
    public void MaskTeaPieTokenButLeaveLoopVariableTokenUntouched()
    {
        const string body = "POST {{ApiGatewayBaseUrl}}/companies { \"name\": \"{{ tenant.Name }}\" }";
        var masker = new LoopBodyMasker();

        var result = masker.Mask(body, "tenant");

        result.Should().Contain("{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}");
        result.Should().Contain("{{ tenant.Name }}");
        result.Should().NotContain("{% raw %}{{ tenant.Name }}{% endraw %}");
    }

    [Fact]
    public void LeaveForloopTokenUntouched()
    {
        const string body = "### item {{ forloop.index }}";
        var masker = new LoopBodyMasker();

        var result = masker.Mask(body, "partner");

        result.Should().Be(body);
    }

    [Fact]
    public void NotSplitDynamicNamingExpressionAtEmbeddedLiteralBraces()
    {
        const string body =
            "POST {{ApiGatewayBaseUrl}}/companies/" +
            "{{ forloop.index | prepend: \"{{Temp.Attachments.CompanyId_\" | append: \"}}\" }}" +
            "/licenses";
        var masker = new LoopBodyMasker();

        var result = masker.Mask(body, "tenant");

        result.Should().Contain(
            "{{ forloop.index | prepend: \"{{Temp.Attachments.CompanyId_\" | append: \"}}\" }}");
        result.Should().Contain("{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}");
    }

    [Fact]
    public void MaskVariableThatOnlySharesAPrefixWithTheLoopVariable()
    {
        const string body = "{{tenantSomethingElse}}";
        var masker = new LoopBodyMasker();

        var result = masker.Mask(body, "tenant");

        result.Should().Be("{% raw %}{{tenantSomethingElse}}{% endraw %}");
    }

    [Fact]
    public void NotDoubleWrapATeaPieTokenAlreadyInsideAUserAuthoredRawBlock()
    {
        const string body = "{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}";
        var masker = new LoopBodyMasker();

        var result = masker.Mask(body, "tenant");

        result.Should().Be(body);
    }

    [Fact]
    public void LeaveALoopVariableTokenUntouchedInsideAUserAuthoredRawBlock()
    {
        const string body = "{% raw %}{{ tenant.Name }}{% endraw %}";
        var masker = new LoopBodyMasker();

        var result = masker.Mask(body, "tenant");

        result.Should().Be(body);
    }

    [Fact]
    public void StillMaskATeaPieTokenOutsideAnUnrelatedRawBlockInTheSameBody()
    {
        const string body = "{% raw %}{{ tenant.Name }}{% endraw %}{{ApiGatewayBaseUrl}}";
        var masker = new LoopBodyMasker();

        var result = masker.Mask(body, "tenant");

        result.Should().Be("{% raw %}{{ tenant.Name }}{% endraw %}{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}");
    }

    [Fact]
    public void LeaveAnAssignedVariableTokenUntouched()
    {
        const string body = "{% assign greeting = \"Hello\" %}{{ greeting }}, {{ tenant.Name }}!";
        var masker = new LoopBodyMasker();

        var result = masker.Mask(body, "tenant");

        result.Should().Be(body);
    }

    [Fact]
    public void StillMaskATeaPieTokenNotMatchingAnyAssignedName()
    {
        const string body = "{% assign greeting = \"Hello\" %}{{ greeting }}{{ApiGatewayBaseUrl}}";
        var masker = new LoopBodyMasker();

        var result = masker.Mask(body, "tenant");

        result.Should().Contain("{{ greeting }}");
        result.Should().Contain("{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}");
    }

    [Fact]
    public void MaskAVariableThatOnlySharesAPrefixWithAnAssignedName()
    {
        const string body = "{% assign greeting = \"Hi\" %}{{greetingSomethingElse}}";
        var masker = new LoopBodyMasker();

        var result = masker.Mask(body, "tenant");

        result.Should().Contain("{% raw %}{{greetingSomethingElse}}{% endraw %}");
    }

    [Fact]
    public void LeaveATeaPieLookingTokenInsideAnAssignTagsStringLiteralUntouched()
    {
        const string body = "{% assign url = \"{{ApiGatewayBaseUrl}}\" %}{{ url }}";
        var masker = new LoopBodyMasker();

        var result = masker.Mask(body, "tenant");

        result.Should().Contain("{% assign url = \"{{ApiGatewayBaseUrl}}\" %}");
        result.Should().NotContain("{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}");
    }

    [Fact]
    public void NotTreatAnAssignInsideAUserAuthoredRawBlockAsABoundName()
    {
        const string body = "{% raw %}{% assign greeting = \"Hi\" %}{% endraw %}{{ greeting }}";
        var masker = new LoopBodyMasker();

        var result = masker.Mask(body, "tenant");

        result.Should().Contain("{% raw %}{% assign greeting = \"Hi\" %}{% endraw %}");
        result.Should().Contain("{% raw %}{{ greeting }}{% endraw %}");
    }
}
