using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TeaPie.Templating;
using TeaPie.Tests.Http;
using TeaPie.TestCases;

namespace TeaPie.Tests.Templating;

public class ExpandTemplatesStepShould
{
    [Theory]
    [InlineData("{% for x in Xs %}BODY{% endfor %}", true, true)]
    [InlineData("{% for x in Xs %}BODY{% endfor %}", false, true)]
    [InlineData("GET https://example.test/plain", true, false)]
    [InlineData("POST https://example.test\n\n{\"fmt\":\"{%Y-%m-%d}\"}", true, false)]
    public async Task UseTheExpandedContentAndDumpItOnlyForATemplatedFile(
        string content, bool dumpRequested, bool templated)
    {
        var context = RequestHelper.PrepareTestCaseContext(RequestsIndex.RequestWithFullStructure, false);
        context.RequestsFileContent = content;
        var dataValues = new Dictionary<string, string> { ["__teapie_value_1"] = "a\nb" };

        var expander = Substitute.For<ITemplateExpander>();
        expander.Expand(content, context.TestCase.RequestsFile.RelativePath)
            .Returns(new TemplateExpansionResult(templated ? "EXPANDED" : content, TemplatedRequestNames.None)
            {
                DataValues = dataValues
            });

        var dumper = Substitute.For<IExpandedContentDumper>();
        var step = new ExpandTemplatesStep(new TestCaseExecutionContextAccessor { Context = context }, expander, dumper);

        var logger = Substitute.For<ILogger<ApplicationContext>>();
        var appContextBuilder = new ApplicationContextBuilder()
            .WithPath(RequestsIndex.RootFolderFullPath)
            .WithLogger(logger);
        if (dumpRequested)
        {
            appContextBuilder.WithExpandedContentDump();
        }

        var appContext = appContextBuilder.Build();

        await step.Execute(appContext);

        if (templated)
        {
            context.RequestsFileContent.Should().Be("EXPANDED");
            context.TemplateDataValues.Should().Equal(dataValues);
            dumper.ReceivedCalls().Should().HaveCount(dumpRequested ? 1 : 0);
            if (dumpRequested)
            {
                dumper.Received(1).Dump(appContext, context.TestCase, "EXPANDED");
            }
        }
        else
        {
            context.RequestsFileContent.Should().BeSameAs(content);
            dumper.ReceivedCalls().Should().BeEmpty();
            logger.DidNotReceive().Log(
                Arg.Is<LogLevel>(level => level == LogLevel.Debug || level == LogLevel.Trace),
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
