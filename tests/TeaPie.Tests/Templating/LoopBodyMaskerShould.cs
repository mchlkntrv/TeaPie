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
}
