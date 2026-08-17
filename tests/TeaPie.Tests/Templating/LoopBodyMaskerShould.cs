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
}
