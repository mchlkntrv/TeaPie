using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TeaPie.Http;
using TeaPie.Pipelines;
using TeaPie.Templating;
using TeaPie.Tests.Http;
using TeaPie.TestCases;
using TeaPie.Variables;

namespace TeaPie.Tests.Templating;

public class TemplatingPipelineEndToEndShould
{
    [Fact]
    public async Task ExpandLoopIntoThreeIndependentRequestsAcrossTheRealPipeline()
    {
        const string template =
            "{% for partner in Temp.FreePartners %}### New partner {{ forloop.index }}\n" +
            "POST {{ApiGatewayBaseUrl}}/companies/partners\n" +
            "Content-Type: application/json\n\n" +
            "{ \"registrationId\": \"{{ partner.RegistrationId }}\" }\n" +
            "{% endfor %}";

        var services = new ServiceCollection();
        services.AddTeaPie(false, () => { });
        var serviceProvider = services.BuildServiceProvider();

        var variables = serviceProvider.GetRequiredService<IVariables>();
        variables.SetVariable("Temp.FreePartners", new[]
        {
            new { RegistrationId = "01245" },
            new { RegistrationId = "012426" },
            new { RegistrationId = "012427" }
        });

        var testCaseExecutionContext = RequestHelper.PrepareTestCaseContext(RequestsIndex.RequestWithFullStructure, false);
        testCaseExecutionContext.RequestsFileContent = template;

        var accessor = new TestCaseExecutionContextAccessor { Context = testCaseExecutionContext };

        var appContext = new ApplicationContextBuilder()
            .WithServiceProvider(serviceProvider)
            .WithPath(RequestsIndex.RootFolderFullPath)
            .Build();

        var readStep = new ReadHttpFileStep(accessor);
        await readStep.Execute(appContext);

        var templateExpander = serviceProvider.GetRequiredService<ITemplateExpander>();
        var expandStep = new ExpandTemplatesStep(accessor, templateExpander);
        await expandStep.Execute(appContext);

        testCaseExecutionContext.RequestsFileContent.Should().NotBeNull();
        testCaseExecutionContext.RequestsFileContent!.Should()
            .Contain("\"registrationId\": \"01245\"").And
            .Contain("\"registrationId\": \"012426\"").And
            .Contain("\"registrationId\": \"012427\"");

        var pipeline = Substitute.For<IPipeline>();
        var generateStepsForRequestsStep = new GenerateStepsForRequestsStep(accessor, pipeline);
        await generateStepsForRequestsStep.Execute(appContext);

        pipeline.Received(1).InsertSteps(
            generateStepsForRequestsStep,
            Arg.Is<IPipelineStep[]>(steps => steps.Length == 9));
    }
}
