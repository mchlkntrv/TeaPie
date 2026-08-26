using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class VariablesFluidModelBuilderShould
{
    [Fact]
    public void ReturnEmptyModelWhenNoVariablesAreSet()
    {
        var variables = new global::TeaPie.Variables.Variables();
        var builder = new VariablesFluidModelBuilder();

        var result = builder.Build(variables);

        result.Should().BeEmpty();
    }

    [Fact]
    public void IncludeAVariableSetOnASingleScope()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.CollectionVariables.Set("PartnerCount", 3);
        var builder = new VariablesFluidModelBuilder();

        var result = builder.Build(variables);

        result.Should().ContainKey("PartnerCount").WhoseValue.Should().Be(3);
    }

    [Fact]
    public void PreferHigherPriorityScopeWhenSameNameExistsOnMultipleLevels()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.GlobalVariables.Set("VariableOnMultipleLevels", "abc");
        variables.EnvironmentVariables.Set("VariableOnMultipleLevels", "def");
        variables.CollectionVariables.Set("VariableOnMultipleLevels", "ghi");
        variables.TestCaseVariables.Set("VariableOnMultipleLevels", "jkl");
        var builder = new VariablesFluidModelBuilder();

        var result = builder.Build(variables);

        result["VariableOnMultipleLevels"].Should().Be("jkl");
    }

    [Fact]
    public void StillExposeANameThatOnlyExistsOnALowerPriorityScope()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.GlobalVariables.Set("OnlyGlobal", "globalValue");
        var builder = new VariablesFluidModelBuilder();

        var result = builder.Build(variables);

        result["OnlyGlobal"].Should().Be("globalValue");
    }

    [Fact]
    public void PreserveNonStringValueTypesUnchanged()
    {
        var variables = new global::TeaPie.Variables.Variables();
        var partners = new List<string> { "Acme", "Globex" };
        variables.CollectionVariables.Set("Partners", partners);
        variables.CollectionVariables.Set("IsEnabled", true);
        variables.CollectionVariables.Set("Threshold", 5.5);
        var builder = new VariablesFluidModelBuilder();

        var result = builder.Build(variables);

        result["Partners"].Should().BeSameAs(partners);
        result["IsEnabled"].Should().Be(true);
        result["Threshold"].Should().Be(5.5);
    }

    [Fact]
    public void MergeDistinctNamesFromAllFourScopes()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.GlobalVariables.Set("FromGlobal", "g");
        variables.EnvironmentVariables.Set("FromEnvironment", "e");
        variables.CollectionVariables.Set("FromCollection", "c");
        variables.TestCaseVariables.Set("FromTestCase", "t");
        var builder = new VariablesFluidModelBuilder();

        var result = builder.Build(variables);

        result.Should().HaveCount(4);
        result["FromGlobal"].Should().Be("g");
        result["FromEnvironment"].Should().Be("e");
        result["FromCollection"].Should().Be("c");
        result["FromTestCase"].Should().Be("t");
    }

    [Fact]
    public void ExcludeVariablesTaggedAsSecret()
    {
        var variables = new global::TeaPie.Variables.Variables();
        variables.CollectionVariables.Set("AccessToken", "super-secret-token", Constants.SecretVariableTag);
        variables.CollectionVariables.Set("PartnerCount", 3);
        var builder = new VariablesFluidModelBuilder();

        var result = builder.Build(variables);

        result.Should().NotContainKey("AccessToken");
        result.Should().ContainKey("PartnerCount");
    }
}
