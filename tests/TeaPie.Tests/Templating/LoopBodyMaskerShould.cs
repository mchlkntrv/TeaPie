using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class LoopBodyMaskerShould
{
    private static string ApplyMask(string content)
    {
        var blocks = new LoopBlockScanner().FindLoopBlocks(content);
        var edits = new LoopBodyMasker().FindMaskEdits(content, blocks);
        return TextEditApplier.Apply(content, edits);
    }

    [Fact]
    public void MaskATeaPieTokenThatDoesNotBelongToAnyLoopScope()
    {
        const string content = "{{ApiGatewayBaseUrl}}";

        var result = ApplyMask(content);

        result.Should().Be("{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}");
    }

    [Fact]
    public void LeaveTheLoopVariableAndForloopTokensInsideTheirOwnLoopBodyUnmasked()
    {
        const string content = "{% for item in Items %}{{ item.Name }}{{ forloop.index }}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Be(content);
    }

    [Fact]
    public void LeaveAnInLoopAssignTargetUnmaskedAnywhereInTheSameLoopBodyRegardlessOfOrder()
    {
        const string content =
            "{% for item in Items %}{{ label }}{% assign label = item.Name %}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Be(content);
    }

    [Fact]
    public void MaskATeaPieTokenInsideAnIfBlockThatDoesNotBelongToLoopScope()
    {
        const string content =
            "{% for item in Items %}{% if item.Name == \"Acme\" %}{{ApiGatewayBaseUrl}}{% endif %}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Be(
            "{% for item in Items %}{% if item.Name == \"Acme\" %}" +
            "{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}{% endif %}{% endfor %}");
    }

    [Fact]
    public void LeaveATopLevelAssignTargetUnmaskedAfterItsOwnDeclaration()
    {
        const string content = "{% assign gatewayName = \"gw\" %}{{ gatewayName }}";

        var result = ApplyMask(content);

        result.Should().Be(content);
    }

    [Fact]
    public void MaskATopLevelTokenReferencingAnAssignTargetDeclaredLaterInTheFile()
    {
        const string content = "{{ gatewayName }}{% assign gatewayName = \"gw\" %}";

        var result = ApplyMask(content);

        result.Should().Be(
            "{% raw %}{{ gatewayName }}{% endraw %}{% assign gatewayName = \"gw\" %}");
    }

    [Fact]
    public void LeaveATopLevelAssignTargetDeclaredBeforeALoopUnmaskedInsideThatLoopsBody()
    {
        const string content =
            "{% assign gatewayName = \"gw\" %}{% for item in Items %}{{ gatewayName }}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Be(content);
    }

    [Fact]
    public void NotLeakAnInLoopAssignTargetIntoASiblingLoopsBody()
    {
        const string content =
            "{% for a in Items %}{% assign shared = a.Name %}{% endfor %}" +
            "{% for b in Items %}{{ shared }}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Be(
            "{% for a in Items %}{% assign shared = a.Name %}{% endfor %}" +
            "{% for b in Items %}{% raw %}{{ shared }}{% endraw %}{% endfor %}");
    }

    [Fact]
    public void NotLeakAnInLoopAssignTargetIntoTopLevelTextAfterTheLoopEnds()
    {
        const string content =
            "{% for a in Items %}{% assign shared = a.Name %}{% endfor %}{{ shared }}";

        var result = ApplyMask(content);

        result.Should().Be(
            "{% for a in Items %}{% assign shared = a.Name %}{% endfor %}" +
            "{% raw %}{{ shared }}{% endraw %}");
    }

    [Fact]
    public void FindTopLevelAssignTargetNamesButNotInLoopOnes()
    {
        const string content =
            "{% assign topLevel = \"x\" %}{% for item in Items %}{% assign inLoop = item.Name %}{% endfor %}";
        var blocks = new LoopBlockScanner().FindLoopBlocks(content);

        var names = new LoopBodyMasker().FindTopLevelAssignTargetNames(content, blocks);

        names.Should().BeEquivalentTo(new[] { "topLevel" });
    }

    [Fact]
    public void MaskATeaPieTokenButLeaveTheLoopVariableTokenUntouchedWhenBothAppearInTheSameBody()
    {
        const string content =
            "{% for tenant in Items %}POST {{ApiGatewayBaseUrl}}/companies { \"name\": \"{{ tenant.Name }}\" }{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Contain("{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}");
        result.Should().Contain("{{ tenant.Name }}");
        result.Should().NotContain("{% raw %}{{ tenant.Name }}{% endraw %}");
    }

    [Fact]
    public void NotSplitADynamicNamingExpressionAtEmbeddedLiteralBraces()
    {
        const string content =
            "{% for tenant in Items %}POST {{ApiGatewayBaseUrl}}/companies/" +
            "{{ forloop.index | prepend: \"{{Temp.Attachments.CompanyId_\" | append: \"}}\" }}" +
            "/licenses{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Contain(
            "{{ forloop.index | prepend: \"{{Temp.Attachments.CompanyId_\" | append: \"}}\" }}");
        result.Should().Contain("{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}");
    }

    [Fact]
    public void MaskAVariableThatOnlySharesAPrefixWithTheLoopVariable()
    {
        const string content = "{% for tenant in Items %}{{tenantSomethingElse}}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Contain("{% raw %}{{tenantSomethingElse}}{% endraw %}");
    }

    [Fact]
    public void NotDoubleWrapATeaPieTokenAlreadyInsideAUserAuthoredRawBlock()
    {
        const string content = "{% for tenant in Items %}{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Be(content);
    }

    [Fact]
    public void LeaveALoopVariableTokenUntouchedInsideAUserAuthoredRawBlock()
    {
        const string content = "{% for tenant in Items %}{% raw %}{{ tenant.Name }}{% endraw %}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Be(content);
    }

    [Fact]
    public void StillMaskATeaPieTokenOutsideAnUnrelatedRawBlockInTheSameLoopBody()
    {
        const string content =
            "{% for tenant in Items %}{% raw %}{{ tenant.Name }}{% endraw %}{{ApiGatewayBaseUrl}}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Be(
            "{% for tenant in Items %}{% raw %}{{ tenant.Name }}{% endraw %}" +
            "{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}{% endfor %}");
    }

    [Fact]
    public void LeaveAnAssignedVariableTokenUntouchedAlongsideTheLoopVariable()
    {
        const string content =
            "{% for tenant in Items %}{% assign greeting = \"Hello\" %}{{ greeting }}, {{ tenant.Name }}!{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Be(content);
    }

    [Fact]
    public void StillMaskATeaPieTokenNotMatchingAnyAssignedName()
    {
        const string content =
            "{% for tenant in Items %}{% assign greeting = \"Hello\" %}{{ greeting }}{{ApiGatewayBaseUrl}}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Contain("{{ greeting }}");
        result.Should().Contain("{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}");
    }

    [Fact]
    public void MaskAVariableThatOnlySharesAPrefixWithAnAssignedName()
    {
        const string content = "{% for tenant in Items %}{% assign greeting = \"Hi\" %}{{greetingSomethingElse}}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Contain("{% raw %}{{greetingSomethingElse}}{% endraw %}");
    }

    [Fact]
    public void LeaveATeaPieLookingTokenInsideAnAssignTagsStringLiteralUntouched()
    {
        const string content = "{% for tenant in Items %}{% assign url = \"{{ApiGatewayBaseUrl}}\" %}{{ url }}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Contain("{% assign url = \"{{ApiGatewayBaseUrl}}\" %}");
        result.Should().NotContain("{% raw %}{{ApiGatewayBaseUrl}}{% endraw %}");
    }

    [Fact]
    public void NotTreatAnAssignInsideAUserAuthoredRawBlockAsABoundName()
    {
        const string content =
            "{% for tenant in Items %}{% raw %}{% assign greeting = \"Hi\" %}{% endraw %}{{ greeting }}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Contain("{% raw %}{% assign greeting = \"Hi\" %}{% endraw %}");
        result.Should().Contain("{% raw %}{{ greeting }}{% endraw %}");
    }

    [Fact]
    public void LeaveALoopScopedTokenInsideAnIfBlockUntouched()
    {
        const string content = "{% for tenant in Items %}{% if forloop.first %}{{ tenant.Name }}{% endif %}{% endfor %}";

        var result = ApplyMask(content);

        result.Should().Be(content);
    }
}
