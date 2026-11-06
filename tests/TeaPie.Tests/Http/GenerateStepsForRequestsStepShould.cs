using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TeaPie.Http;
using TeaPie.Pipelines;
using TeaPie.Templating;
using TeaPie.TestCases;
using TeaPie.Variables;

namespace TeaPie.Tests.Http;

public class GenerateStepsForRequestsStepShould
{
    [Fact]
    public async Task RegisterEachLoopGeneratedRequestUnderItsRenderedName()
    {
        var (context, appContext, insertedSteps, logger) = await ExpandAndGenerateStepsForContent(
            "{% for partner in Temp.Partners %}### Create partner\n" +
            "# @name CreatePartner{{ forloop.index }}\n" +
            "POST https://example.com/partners\n" +
            "Content-Type: application/json\n\n" +
            "{ \"registrationId\": \"{{ partner.RegistrationId }}\" }\n" +
            "{% endfor %}");

        insertedSteps.Should().HaveCount(6);
        await insertedSteps[0].Execute(appContext);
        await insertedSteps[3].Execute(appContext);
        context.Requests.Keys.Should().BeEquivalentTo(["CreatePartner1", "CreatePartner2"]);
        CountDuplicateNameWarnings(logger, "CreatePartner").Should().Be(0);
    }

    public enum DuplicateNameOutcome
    {
        None,
        Warning,
        Error,
        ErrorWithLoopHint
    }

    public static TheoryData<string, string, DuplicateNameOutcome> DuplicateNamesInTemplatedFiles => new()
    {
        {
            "{% for i in (1..3) %}" + Request("login", "{{ i }}") + "{% endfor %}",
            "login", DuplicateNameOutcome.Warning
        },
        {
            "{% for i in (1..2) %}" + Request("login", "a/{{ i }}") + "{% endfor %}" +
            "{% for i in (1..2) %}" + Request("login", "b/{{ i }}") + "{% endfor %}",
            "login", DuplicateNameOutcome.Warning
        },
        {
            "{% comment %}\n" + Request("Car", "old") + "{% endcomment %}\n" +
            "{% for c in (1, 2) %}" + Request("Car", "cars/{{ c }}") + "{% endfor %}",
            "Car", DuplicateNameOutcome.Warning
        },
        {
            "{% for i in (1..2) %}" + Request("item{{ forloop.index }}", "{{ i }}") + "{% endfor %}" +
            Request("login", "a") + Request("login", "b"),
            "login", DuplicateNameOutcome.Error
        },
        {
            Request("login", "a") + "{% for i in (1..3) %}" + Request("login", "{{ i }}") + "{% endfor %}",
            "login", DuplicateNameOutcome.ErrorWithLoopHint
        },
        {
            "{% for i in (1..3) %}" + Request("login", "{{ i }}") + Request("login", "profile/{{ i }}") +
            "{% endfor %}",
            "login", DuplicateNameOutcome.ErrorWithLoopHint
        },
        {
            "{% for i in (1..2) %}" + Request("login{{ forloop.index }}", "{{ i }}") +
            Request("login{{ forloop.index }}", "profile/{{ i }}") + "{% endfor %}",
            "login1", DuplicateNameOutcome.ErrorWithLoopHint
        },
        {
            "{% if false %}" + Request("login", "v2") + "{% else %}" + Request("login", "v1") + "{% endif %}",
            "login", DuplicateNameOutcome.None
        }
    };

    [Theory]
    [MemberData(nameof(DuplicateNamesInTemplatedFiles))]
    public async Task RejectADuplicateRequestNameUnlessOnlyLoopsRepeatIt(
        string content, string name, DuplicateNameOutcome expectedOutcome)
    {
        var (_, appContext, insertedSteps, logger) = await ExpandAndGenerateStepsForContent(content);

        var parseAllRequests = async () =>
        {
            foreach (var step in insertedSteps.OfType<ParseHttpRequestStep>())
            {
                await step.Execute(appContext);
            }
        };

        if (expectedOutcome is DuplicateNameOutcome.Error or DuplicateNameOutcome.ErrorWithLoopHint)
        {
            var exception = (await parseAllRequests.Should().ThrowAsync<InvalidOperationException>()).Which;
            exception.Message.Should().Contain($"more than one request named '{name}' after template expansion");
            exception.Message.Contains("{{ forloop.index }}", StringComparison.Ordinal).Should()
                .Be(expectedOutcome is DuplicateNameOutcome.ErrorWithLoopHint);
        }
        else
        {
            await parseAllRequests.Should().NotThrowAsync();
        }

        CountDuplicateNameWarnings(logger, name).Should().Be(expectedOutcome is DuplicateNameOutcome.Warning ? 1 : 0);
    }

    [Fact]
    public async Task KeepRejectingDuplicateNamesInAFileWithoutTemplates()
    {
        const string content =
            "### First\n# @name Same\nPOST https://example.com/partners\n\n" +
            "### Second\n# @name Same\nPOST https://example.com/partners\n";

        var (context, appContext, insertedSteps, logger) = await ExpandAndGenerateStepsForContent(content);

        CountDuplicateNameWarnings(logger, "Same").Should().Be(0);
        context.TemplatedRequestNames.Should().BeNull();
        await insertedSteps[0].Execute(appContext);
        var registerSecond = () => insertedSteps[3].Execute(appContext);
        await registerSecond.Should().ThrowAsync<ArgumentException>();
    }

    private static string Request(string name, string path)
        => $"### Request\n# @name {name}\nPOST https://example.com/{path}\n\n";

    private static int CountDuplicateNameWarnings(ILogger<ApplicationContext> logger, string name)
        => logger.ReceivedCalls().Count(call =>
            call.GetArguments()[0] is LogLevel.Warning
            && call.GetArguments()[2]?.ToString()?.Contains($"requests named '{name}'") == true);

    private static async Task<(TestCaseExecutionContext Context, ApplicationContext AppContext,
        IPipelineStep[] InsertedSteps, ILogger<ApplicationContext> Logger)> ExpandAndGenerateStepsForContent(
        string content)
    {
        var services = new ServiceCollection();
        var logger = Substitute.For<ILogger<ApplicationContext>>();
        services.AddSingleton(logger);
        services.AddTeaPie(false, () => { });
        var serviceProvider = services.BuildServiceProvider();

        var variables = serviceProvider.GetRequiredService<IVariables>();
        variables.SetVariable("Temp.Partners", new[]
        {
            new { RegistrationId = "01245" },
            new { RegistrationId = "012426" }
        });

        var context = RequestHelper.PrepareTestCaseContext(RequestsIndex.RequestWithFullStructure, false);
        context.RequestsFileContent = content;
        var accessor = new TestCaseExecutionContextAccessor { Context = context };
        var appContext = new ApplicationContextBuilder()
            .WithServiceProvider(serviceProvider)
            .WithPath(RequestsIndex.RootFolderFullPath)
            .Build();

        await new ExpandTemplatesStep(
                accessor,
                serviceProvider.GetRequiredService<ITemplateExpander>(),
                serviceProvider.GetRequiredService<IExpandedContentDumper>())
            .Execute(appContext);

        IPipelineStep[] insertedSteps = [];
        var pipeline = Substitute.For<IPipeline>();
        pipeline.When(p => p.InsertSteps(Arg.Any<IPipelineStep>(), Arg.Any<IPipelineStep[]>()))
            .Do(call => insertedSteps = call.ArgAt<IPipelineStep[]>(1));
        await new GenerateStepsForRequestsStep(accessor, pipeline).Execute(appContext);

        return (context, appContext, insertedSteps, logger);
    }
}
