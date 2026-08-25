using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TeaPie.Pipelines;
using TeaPie.Templating;
using TeaPie.TestCases;
using TeaPie.Variables;

namespace TeaPie.Tests.Http;

public class GenerateStepsForRequestsStepShould
{
    [Fact]
    public void NotReportAnyDuplicateWhenEveryRequestNameIsUnique()
    {
        string[] requests =
        [
            "### First\n# @name CreatePartner1\nPOST {{ApiGatewayBaseUrl}}/partners\n",
            "### Second\n# @name CreatePartner2\nPOST {{ApiGatewayBaseUrl}}/partners\n"
        ];

        var duplicates = GenerateStepsForRequestsStep.FindDuplicateRequestNames(requests);

        duplicates.Should().BeEmpty();
    }

    [Fact]
    public void ReportADuplicateWhenALoopProducesRequestsSharingTheSameLiteralName()
    {
        string[] requests =
        [
            "### First\n# @name CreatePartner\nPOST {{ApiGatewayBaseUrl}}/partners\n",
            "### Second\n# @name CreatePartner\nPOST {{ApiGatewayBaseUrl}}/partners\n",
            "### Third\n# @name CreatePartner\nPOST {{ApiGatewayBaseUrl}}/partners\n"
        ];

        var duplicates = GenerateStepsForRequestsStep.FindDuplicateRequestNames(requests);

        duplicates.Should().BeEquivalentTo([("CreatePartner", 3)]);
    }

    [Fact]
    public void ReportEachDistinctDuplicateNameSeparately()
    {
        string[] requests =
        [
            "### First\n# @name CreatePartner\nPOST {{ApiGatewayBaseUrl}}/partners\n",
            "### Second\n# @name CreatePartner\nPOST {{ApiGatewayBaseUrl}}/partners\n",
            "### Third\n# @name CreateCompany\nPOST {{ApiGatewayBaseUrl}}/companies\n",
            "### Fourth\n# @name CreateCompany\nPOST {{ApiGatewayBaseUrl}}/companies\n"
        ];

        var duplicates = GenerateStepsForRequestsStep.FindDuplicateRequestNames(requests);

        duplicates.Should().BeEquivalentTo([("CreatePartner", 2), ("CreateCompany", 2)]);
    }

    [Fact]
    public void ReportADuplicateBetweenAPlainRequestAndARequestProducedByALoop()
    {
        string[] requests =
        [
            "### Plain request\n# @name Setup\nPOST {{ApiGatewayBaseUrl}}/setup\n",
            "### From loop\n# @name Setup\nPOST {{ApiGatewayBaseUrl}}/partners\n"
        ];

        var duplicates = GenerateStepsForRequestsStep.FindDuplicateRequestNames(requests);

        duplicates.Should().BeEquivalentTo([("Setup", 2)]);
    }

    [Fact]
    public void ReportDuplicatesArisingAcrossTwoDifferentLoops()
    {
        string[] requests =
        [
            "### Loop 1 iteration 1\n# @name Item\nPOST {{ApiGatewayBaseUrl}}/products\n",
            "### Loop 1 iteration 2\n# @name Item\nPOST {{ApiGatewayBaseUrl}}/products\n",
            "### Loop 2 iteration 1\n# @name Item\nPOST {{ApiGatewayBaseUrl}}/categories\n"
        ];

        var duplicates = GenerateStepsForRequestsStep.FindDuplicateRequestNames(requests);

        duplicates.Should().BeEquivalentTo([("Item", 3)]);
    }

    [Fact]
    public void NotReportAnythingForRequestsWithoutAName()
    {
        string[] requests =
        [
            "### First\nPOST {{ApiGatewayBaseUrl}}/partners\n",
            "### Second\nPOST {{ApiGatewayBaseUrl}}/partners\n"
        ];

        var duplicates = GenerateStepsForRequestsStep.FindDuplicateRequestNames(requests);

        duplicates.Should().BeEmpty();
    }

    [Fact]
    public async Task WarnOnDuplicateNamesWithoutChangingTheScheduledStepCountOrContent()
    {
        const string template =
            "{% for partner in Temp.Partners %}### Create partner\n" +
            "# @name CreatePartner\n" +
            "POST {{ApiGatewayBaseUrl}}/partners\n" +
            "Content-Type: application/json\n\n" +
            "{ \"registrationId\": \"{{ partner.RegistrationId }}\" }\n" +
            "{% endfor %}";

        var services = new ServiceCollection();
        services.AddTeaPie(false, () => { });
        var serviceProvider = services.BuildServiceProvider();

        var variables = serviceProvider.GetRequiredService<IVariables>();
        variables.SetVariable("Temp.Partners", new[]
        {
            new { RegistrationId = "01245" },
            new { RegistrationId = "012426" }
        });

        var testCaseExecutionContext = RequestHelper.PrepareTestCaseContext(RequestsIndex.RequestWithFullStructure, false);
        testCaseExecutionContext.RequestsFileContent = template;

        var accessor = new TestCaseExecutionContextAccessor { Context = testCaseExecutionContext };

        var appContext = new ApplicationContextBuilder()
            .WithServiceProvider(serviceProvider)
            .WithPath(RequestsIndex.RootFolderFullPath)
            .Build();

        var templateExpander = serviceProvider.GetRequiredService<ITemplateExpander>();
        var expandStep = new ExpandTemplatesStep(accessor, templateExpander);
        await expandStep.Execute(appContext);

        var pipeline = Substitute.For<IPipeline>();
        var generateStepsForRequestsStep = new GenerateStepsForRequestsStep(accessor, pipeline);

        var act = async () => await generateStepsForRequestsStep.Execute(appContext);

        await act.Should().NotThrowAsync();
        pipeline.Received(1).InsertSteps(
            generateStepsForRequestsStep,
            Arg.Is<IPipelineStep[]>(steps => steps.Length == 6));
    }
}
