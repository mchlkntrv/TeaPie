using Microsoft.Extensions.Logging;
using TeaPieDraft.Pipelines.Base;

namespace TeaPieDraft.Pipelines.StructureExploration.Collection;

internal class PrintTestCaseOrderStep : IPipelineStep<CollectionExplorationContext>
{
    public async Task<CollectionExplorationContext> ExecuteAsync(
        CollectionExplorationContext context,
        CancellationToken cancellationToken = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (context.TestCases is null) throw new ArgumentNullException(nameof(context.TestCases));

        var logger = Application.UserContext!.Logger;
        logger.LogInformation("Test cases will run in following order:");
        var i = 0;
        foreach (var testCase in context.TestCases)
        {
            i++;
            logger.LogInformation("{order}. '{testCase}'", i, testCase.Value.RelativePath);
        }

        return await Task.FromResult(context);
    }
}
