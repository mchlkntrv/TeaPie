using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class LoopBlockScannerShould
{
    [Fact]
    public void FindSingleLoopBlockWithVariableNameAndSourceExpression()
    {
        const string content = "before\n{% for car in Cars %}BODY{% endfor %}\nafter";
        var scanner = new LoopBlockScanner();

        var blocks = scanner.FindLoopBlocks(content);

        blocks.Should().HaveCount(1);
        blocks[0].LoopVariableName.Should().Be("car");
        blocks[0].SourceExpression.Should().Be("Cars");
        blocks[0].Body.Should().Be("BODY");
        blocks[0].SourceExpressionStartIndex.Should().Be(content.IndexOf("Cars", StringComparison.Ordinal));
        blocks[0].SourceExpressionRawLength.Should().Be("Cars".Length);
    }

    [Fact]
    public void FindMultipleSequentialLoopBlocksInDocumentOrder()
    {
        const string content = "{% for a in As %}A{% endfor %}mid{% for b in Bs %}B{% endfor %}";
        var scanner = new LoopBlockScanner();

        var blocks = scanner.FindLoopBlocks(content);

        blocks.Should().HaveCount(2);
        blocks[0].LoopVariableName.Should().Be("a");
        blocks[1].LoopVariableName.Should().Be("b");
        blocks[0].StartIndex.Should().BeLessThan(blocks[1].StartIndex);
    }

    [Fact]
    public void ThrowWhenEndforTagIsMissing()
    {
        const string content = "{% for car in Cars %}BODY, no closing tag";
        var scanner = new LoopBlockScanner();

        var act = () => scanner.FindLoopBlocks(content);

        act.Should().Throw<InvalidOperationException>().WithMessage("*endfor*");
    }

    [Fact]
    public void FindNestedLoopBlocksInsteadOfRejectingThem()
    {
        const string content = "{% for outer in Outers %}{% for inner in Inners %}BODY{% endfor %}{% endfor %}";
        var scanner = new LoopBlockScanner();

        var blocks = scanner.FindLoopBlocks(content);

        blocks.Should().HaveCount(2);
        var outer = blocks.Single(b => b.LoopVariableName == "outer");
        var inner = blocks.Single(b => b.LoopVariableName == "inner");
        inner.SourceExpression.Should().Be("Inners");
        inner.Body.Should().Be("BODY");
        outer.SourceExpression.Should().Be("Outers");
        outer.StartIndex.Should().BeLessThan(inner.StartIndex);
        (inner.StartIndex + inner.Length).Should().BeLessThanOrEqualTo(outer.StartIndex + outer.Length);
    }

    [Fact]
    public void FindDeeplyNestedLoopBlocksAcrossThreeLevels()
    {
        const string content =
            "{% for a in As %}{% for b in Bs %}{% for c in Cs %}BODY{% endfor %}{% endfor %}{% endfor %}";
        var scanner = new LoopBlockScanner();

        var blocks = scanner.FindLoopBlocks(content);

        blocks.Should().HaveCount(3);
        blocks.Select(b => b.LoopVariableName).Should().BeEquivalentTo(["a", "b", "c"]);
        blocks.Single(b => b.LoopVariableName == "c").Body.Should().Be("BODY");
    }

    [Fact]
    public void ThrowIdentifyingTheOutermostUnclosedLoopWhenOnlyItIsMissingAnEndfor()
    {
        const string content = "{% for a in As %}{% for b in Bs %}BODY{% endfor %}";
        var scanner = new LoopBlockScanner();

        var act = () => scanner.FindLoopBlocks(content);

        act.Should().Throw<InvalidOperationException>().WithMessage("*endfor*for a in As*");
    }

    [Fact]
    public void ThrowWhenStrayEndforTagHasNoPrecedingForTag()
    {
        const string content = "before\n{% endfor %}\nafter";
        var scanner = new LoopBlockScanner();

        var act = () => scanner.FindLoopBlocks(content);

        act.Should().Throw<InvalidOperationException>().WithMessage("*endfor*");
    }

    [Fact]
    public void ThrowWhenForTagSyntaxIsMalformed()
    {
        const string content = "{% for car Cars %}BODY{% endfor %}";
        var scanner = new LoopBlockScanner();

        var act = () => scanner.FindLoopBlocks(content);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FindWellFormedWhitespaceControlLoopBlock()
    {
        const string content = "{%- for car in Cars -%}BODY{%- endfor -%}";
        var scanner = new LoopBlockScanner();

        var blocks = scanner.FindLoopBlocks(content);

        blocks.Should().HaveCount(1);
        blocks[0].LoopVariableName.Should().Be("car");
        blocks[0].SourceExpression.Should().Be("Cars");
        blocks[0].Body.Should().Be("BODY");
    }
}
