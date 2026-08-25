using Microsoft.Extensions.Logging;
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

        var expandedContent = templateExpander.Expand(content, testCaseExecutionContext.TestCase.RequestsFile.RelativePath);
        testCaseExecutionContext.RequestsFileContent = expandedContent;

        LogExpansionResult(context, testCaseExecutionContext, content.Length, expandedContent.Length);
        LogExpandedContent(context, testCaseExecutionContext, expandedContent);

        await Task.CompletedTask;
    }

    private static void LogExpansionResult(
        ApplicationContext context, TestCaseExecutionContext testCaseExecutionContext, int originalLength, int expandedLength)
        => context.Logger.LogDebug(
            "Templates within the requests file at '{Path}' were expanded (content length {OriginalLength} -> " +
            "{ExpandedLength}, {Change}).",
            testCaseExecutionContext.TestCase.RequestsFile.RelativePath,
            originalLength,
            expandedLength,
            originalLength == expandedLength ? "unchanged" : "loop(s) expanded");

    private static void LogExpandedContent(
        ApplicationContext context, TestCaseExecutionContext testCaseExecutionContext, string expandedContent)
        => context.Logger.LogTrace(
            "Requests file at '{Path}' after template expansion:{NewLine}{Content}",
            testCaseExecutionContext.TestCase.RequestsFile.RelativePath,
            Environment.NewLine,
            expandedContent);
}
