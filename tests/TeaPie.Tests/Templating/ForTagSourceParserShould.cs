using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class ForTagSourceParserShould
{
    private const int MaxOptionValue = 1000;

    [Fact]
    public void ParseABareCollectionWithoutOptions()
    {
        var source = ForTagSourceParser.Parse("company.Licenses", MaxOptionValue);

        source.CollectionExpression.Should().Be("company.Licenses");
        source.FluidOptions.Should().BeEmpty();
        source.IsReversed.Should().BeFalse();
        source.OffsetExpression.Should().BeNull();
    }

    [Theory]
    [InlineData("items limit: 2 offset: 1 reversed")]
    [InlineData("items reversed offset: 1 limit: 2")]
    [InlineData("items reversed limit:2 offset:1")]
    public void ParseAllOptionsInAnyOrder(string expression)
    {
        var source = ForTagSourceParser.Parse(expression, MaxOptionValue);

        source.CollectionExpression.Should().Be("items");
        source.IsReversed.Should().BeTrue();
        source.OffsetExpression.Should().Be("1");
        source.FluidOptions.Should().Contain("limit: 2").And.Contain("offset: 1").And.Contain("reversed");
    }

    [Fact]
    public void KeepAVariableLimitAsAFluidOption()
    {
        var source = ForTagSourceParser.Parse("items limit: MaxItems", MaxOptionValue);

        source.FluidOptions.Should().Be(" limit: MaxItems");
    }

    [Theory]
    [InlineData("company[\"a b\"] reversed", "company[\"a b\"]")]
    [InlineData("company[ key ] reversed", "company[ key ]")]
    [InlineData("(\"a b\", \"c\") reversed", "(\"a b\", \"c\")")]
    [InlineData("(1..5) reversed", "(1..5)")]
    public void KeepWhitespaceInsideBracketsQuotesAndParenthesesPartOfTheCollection(string expression, string expected)
    {
        var source = ForTagSourceParser.Parse(expression, MaxOptionValue);

        source.CollectionExpression.Should().Be(expected);
        source.IsReversed.Should().BeTrue();
    }

    [Fact]
    public void NotTreatAPipeInsideAQuotedInlineLiteralAsAFilter()
    {
        var source = ForTagSourceParser.Parse("(\"a|b\", \"c\")", MaxOptionValue);

        source.CollectionExpression.Should().Be("(\"a|b\", \"c\")");
    }

    [Theory]
    [InlineData("company.Licenses | first")]
    [InlineData("company.Licenses|required")]
    [InlineData("company.Licenses | where: \"active\", true limit: 2")]
    public void RejectFiltersInTheSource(string expression)
    {
        var act = () => ForTagSourceParser.Parse(expression, MaxOptionValue);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*filters are not supported*'limit:', 'offset:' and 'reversed'*");
    }

    [Theory]
    [InlineData("items mandatory", "mandatory")]
    [InlineData("items required", "required")]
    public void RejectAnUnknownOption(string expression, string option)
    {
        var act = () => ForTagSourceParser.Parse(expression, MaxOptionValue);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*unsupported option '{option}'*");
    }

    [Theory]
    [InlineData("items reversed reversed", "reversed")]
    [InlineData("items limit: 1 limit: 2", "limit:")]
    public void RejectADuplicatedOption(string expression, string option)
    {
        var act = () => ForTagSourceParser.Parse(expression, MaxOptionValue);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*option '{option}' is specified more than once*");
    }

    [Fact]
    public void RejectAnOptionWithoutAValue()
    {
        var act = () => ForTagSourceParser.Parse("items limit:", MaxOptionValue);

        act.Should().Throw<InvalidOperationException>().WithMessage("*option 'limit:'*is missing a value*");
    }
}
