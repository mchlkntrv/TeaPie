using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class TemplateExpanderShould
{
    [Fact]
    public void ExpandLoopOverNamedCollectionIntoOneCopyPerItem()
    {
        const string content =
            "{% for partner in FreePartners %}### item {{ forloop.index }}: {{ partner }}{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("FreePartners", new List<string> { "01245", "012426", "012427" });

        var expander = new TemplateExpander(new LoopBlockScanner(), new LoopBodyMasker(), new CollectionSourceResolver(variables));

        var result = expander.Expand(content, "test.http");

        result.Should().Be("### item 1: 01245### item 2: 012426### item 3: 012427");
    }
}
