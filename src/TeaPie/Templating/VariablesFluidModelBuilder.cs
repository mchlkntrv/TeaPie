using TeaPie.Variables;

namespace TeaPie.Templating;

internal sealed class VariablesFluidModelBuilder : IVariablesFluidModelBuilder
{
    public IReadOnlyDictionary<string, object?> Build(IVariables variables)
    {
        var model = new Dictionary<string, object?>();

        foreach (var scope in GetScopesInAscendingPriorityOrder(variables))
        {
            // Defensive: a mocked IVariables can return null for an unconfigured scope; the production
            // Variables implementation never does.
            if (scope is null)
            {
                continue;
            }

            foreach (var variable in scope)
            {
                if (variable.HasTag(Constants.SecretVariableTag))
                {
                    continue;
                }

                model[variable.Name] = variable.GetValue<object>();
            }
        }

        return model;
    }

    // Ascending priority (last write wins) — the exact reverse of Variables.GetAllVariables()'s
    // first-match-wins order. Keep these two in sync if either changes.
    private static IEnumerable<VariablesCollection> GetScopesInAscendingPriorityOrder(IVariables variables) =>
    [
        variables.GlobalVariables,
        variables.EnvironmentVariables,
        variables.CollectionVariables,
        variables.TestCaseVariables
    ];
}
