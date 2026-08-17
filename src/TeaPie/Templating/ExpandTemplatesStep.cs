using TeaPie.Pipelines;
using TeaPie.TestCases;

namespace TeaPie.Templating;

internal sealed class ExpandTemplatesStep(
    ITestCaseExecutionContextAccessor testCaseExecutionContextAccessor,
    ITemplateExpander templateExpander) : IPipelineStep
{
    public async Task Execute(ApplicationContext context, CancellationToken cancellationToken = default)
    {
        ExecutionContextValidator.Validate(
            testCaseExecutionContextAccessor, out TestCaseExecutionContext testCaseExecutionContext, "expand templates");
        ExecutionContextValidator.ValidateParameter(
            testCaseExecutionContext.RequestsFileContent, out string content, "expand templates", "the requests file's content");

        testCaseExecutionContext.RequestsFileContent =
            templateExpander.Expand(content, testCaseExecutionContext.TestCase.RequestsFile.RelativePath);

        await Task.CompletedTask;
    }
}
