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
}
