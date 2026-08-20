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

    [Fact]
    public void ResolveInlineLiteralListOfQuotedStrings()
    {
        var resolver = new CollectionSourceResolver(new global::TeaPie.Variables.Variables());

        var source = resolver.Resolve("(\"new\", \"used\", \"certified\")");

        source.ItemCount.Should().Be(3);
        source.Collection.Should().BeEquivalentTo(new List<object?> { "new", "used", "certified" });
    }

    [Fact]
    public void ResolveInlineLiteralListOfNumbers()
    {
        var resolver = new CollectionSourceResolver(new global::TeaPie.Variables.Variables());

        var source = resolver.Resolve("(1, 2, 3)");

        source.ItemCount.Should().Be(3);
        source.Collection.Should().BeEquivalentTo(new List<object?> { 1, 2, 3 });
    }

    [Fact]
    public void ResolveInlineLiteralListOfBooleans()
    {
        var resolver = new CollectionSourceResolver(new global::TeaPie.Variables.Variables());

        var source = resolver.Resolve("(true, false)");

        source.ItemCount.Should().Be(2);
        source.Collection.Should().BeEquivalentTo(new List<object?> { true, false });
    }

    [Fact]
    public void ResolveInlineLiteralListWithMixedTypes()
    {
        var resolver = new CollectionSourceResolver(new global::TeaPie.Variables.Variables());

        var source = resolver.Resolve("(\"Acme\", 42, true)");

        source.ItemCount.Should().Be(3);
        source.Collection.Should().BeEquivalentTo(new List<object?> { "Acme", 42, true });
    }

    [Fact]
    public void ResolveEmptyInlineLiteralListToZeroItems()
    {
        var resolver = new CollectionSourceResolver(new global::TeaPie.Variables.Variables());

        var source = resolver.Resolve("()");

        source.ItemCount.Should().Be(0);
    }

    [Fact]
    public void ThrowWhenInlineLiteralListContainsAnUnparseableToken()
    {
        var resolver = new CollectionSourceResolver(new global::TeaPie.Variables.Variables());

        var act = () => resolver.Resolve("(1, oops, 3)");

        act.Should().Throw<InvalidOperationException>().WithMessage("*oops*");
    }

    [Fact]
    public void StillResolveNumericRangeWhenBothFormsLookLikeParens()
    {
        var resolver = new CollectionSourceResolver(new global::TeaPie.Variables.Variables());

        var source = resolver.Resolve("(1..5)");

        source.ItemCount.Should().Be(5);
        source.Collection.Should().BeNull();
    }

    [Fact]
    public void ResolveWhitespaceOnlyInlineLiteralListToZeroItems()
    {
        var resolver = new CollectionSourceResolver(new global::TeaPie.Variables.Variables());

        var source = resolver.Resolve("(   )");

        source.ItemCount.Should().Be(0);
    }
}
