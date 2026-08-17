using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class CollectionSourceResolverShould
{
    [Fact]
    public void ResolveNamedCollectionVariable()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("FreePartners", new List<string> { "01245", "012426", "012427" });
        var resolver = new CollectionSourceResolver(variables);

        var source = resolver.Resolve("FreePartners");

        source.ItemCount.Should().Be(3);
        source.Collection.Should().NotBeNull();
    }
}
