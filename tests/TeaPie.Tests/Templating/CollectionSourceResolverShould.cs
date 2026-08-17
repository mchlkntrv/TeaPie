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

    [Fact]
    public void ResolveNumericRangeWithoutAnyCollectionVariable()
    {
        var resolver = new CollectionSourceResolver(new global::TeaPie.Variables.Variables());

        var source = resolver.Resolve("(1..5)");

        source.ItemCount.Should().Be(5);
        source.Collection.Should().BeNull();
    }

    [Fact]
    public void ThrowWhenCollectionVariableIsNotFound()
    {
        var resolver = new CollectionSourceResolver(new global::TeaPie.Variables.Variables());

        var act = () => resolver.Resolve("DoesNotExist");

        act.Should().Throw<InvalidOperationException>().WithMessage("*DoesNotExist*");
    }

    [Fact]
    public void ThrowWhenVariableIsNotEnumerable()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("NotACollection", 42);
        var resolver = new CollectionSourceResolver(variables);

        var act = () => resolver.Resolve("NotACollection");

        act.Should().Throw<InvalidOperationException>().WithMessage("*collection*");
    }
}
