using FluentAssertions;
using NSubstitute;
using TeaPie.Templating;
using TeaPie.Tests.Http;
using TeaPie.TestCases;

namespace TeaPie.Tests.Templating;

public class ExpandTemplatesStepShould
{
    [Fact]
    public async Task RewriteRequestsFileContentUsingTheTemplateExpander()
    {
        var context = RequestHelper.PrepareTestCaseContext(RequestsIndex.RequestWithFullStructure, false);
        context.RequestsFileContent = "{% for x in Xs %}BODY{% endfor %}";

        var expander = Substitute.For<ITemplateExpander>();
        expander.Expand("{% for x in Xs %}BODY{% endfor %}", context.TestCase.RequestsFile.RelativePath)
            .Returns("EXPANDED");

        var accessor = new TestCaseExecutionContextAccessor { Context = context };
        var step = new ExpandTemplatesStep(accessor, expander);

        var appContext = new ApplicationContextBuilder().WithPath(RequestsIndex.RootFolderFullPath).Build();
        await step.Execute(appContext);

        context.RequestsFileContent.Should().Be("EXPANDED");
    }
}
