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
    public void ThrowWhenLoopsAreNested()
    {
        const string content = "{% for a in As %}{% for b in Bs %}BODY{% endfor %}{% endfor %}";
        var scanner = new LoopBlockScanner();

        var act = () => scanner.FindLoopBlocks(content);

        act.Should().Throw<InvalidOperationException>().WithMessage("*nested*");
    }
}
