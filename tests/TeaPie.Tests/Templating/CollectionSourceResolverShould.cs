using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class CollectionSourceResolverShould
{
    public static TheoryData<string, object?[]> InlineLiterals => new()
    {
        { "(\"new\", \"used\")", ["new", "used"] },
        { "(\"a,b\", \"c\")", ["a,b", "c"] },
        { "(\"Acme\", 42, true, 1.5)", ["Acme", 42, true, 1.5] },
        { "(123456789012345678)", [123456789012345678L] },
        { "()", [] },
        { "(   )", [] }
    };

    [Theory]
    [MemberData(nameof(InlineLiterals))]
    public void ResolveAnInlineLiteralListIntoItsTypedItems(string expression, object?[] expected)
    {
        var source = CreateResolver().Resolve(expression);

        source.ItemCount.Should().Be(expected.Length);
        source.Collection.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ResolveANamedCollectionVariable()
    {
        var source = CreateResolver().Resolve("FreePartners");

        source.ItemCount.Should().Be(3);
        source.Collection.Should().NotBeNull();
    }

    [Theory]
    [InlineData("(1..5)", 5)]
    [InlineData("(0..2147483647)", int.MaxValue)]
    public void ResolveANumericRangeToItsItemCountWithoutACollection(string expression, int expectedCount)
    {
        var source = CreateResolver().Resolve(expression);

        source.ItemCount.Should().Be(expectedCount);
        source.Collection.Should().BeNull();
    }

    [Theory]
    [InlineData("DoesNotExist", "*DoesNotExist*was not found*")]
    [InlineData("NotACollection", "*must be a collection*")]
    [InlineData("(1, oops, 3)", "*oops*")]
    [InlineData("(\"a\", \"b\"", "*unbalanced*")]
    [InlineData("(1,,3)", "*empty item*")]
    [InlineData("(1, , 3)", "*empty item*")]
    [InlineData("(1..99999999999)", "*99999999999*")]
    [InlineData("(99999999999..99999999999)", "*99999999999*")]
    public void ThrowAClearErrorForAnUnresolvableSource(string expression, string expectedMessage)
    {
        var act = () => CreateResolver().Resolve(expression);

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedMessage);
    }

    private static CollectionSourceResolver CreateResolver()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("FreePartners", new List<string> { "01245", "012426", "012427" });
        variables.SetVariable("NotACollection", 42);
        return new CollectionSourceResolver(variables);
    }
}
