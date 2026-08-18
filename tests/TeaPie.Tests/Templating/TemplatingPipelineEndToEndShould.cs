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
        // Partners-style seed scenario (mirrors TemplatingEndToEndShould.
        // ExpandPartnersStyleSeedScenarioForThreeItems), but deliberately uses a dotted
        // collection-variable name ('Temp.FreePartners') so this end-to-end test would have
        // caught the dotted-name silent-zero-expansion bug even if the string-level
        // TemplateExpanderShould tests had missed it.
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

        // ReadHttpFileStep: content is pre-populated (as it would be after real file I/O), so this
        // is a no-op here, included for pipeline-order fidelity with production (ReadHttpFileStep ->
        // ExpandTemplatesStep -> GenerateStepsForRequestsStep, see TestCaseStepsFactory).
        var readStep = new ReadHttpFileStep(accessor);
        await readStep.Execute(appContext);

        // Real ExpandTemplatesStep with its real ITemplateExpander (and that expander's real
        // LoopBlockScanner/LoopBodyMasker/CollectionSourceResolver collaborators) resolved from DI.
        var templateExpander = serviceProvider.GetRequiredService<ITemplateExpander>();
        var expandStep = new ExpandTemplatesStep(accessor, templateExpander);
        await expandStep.Execute(appContext);

        testCaseExecutionContext.RequestsFileContent.Should().NotBeNull();
        testCaseExecutionContext.RequestsFileContent!.Should()
            .Contain("\"registrationId\": \"01245\"").And
            .Contain("\"registrationId\": \"012426\"").And
            .Contain("\"registrationId\": \"012427\"");

        // Real GenerateStepsForRequestsStep. Only IPipeline is substituted (to capture how many
        // steps got scheduled instead of actually inserting them into a running pipeline and
        // executing real HTTP calls); RequestStepsFactory still resolves the real
        // ParseHttpRequestStep/ExecuteRequestStep/DisposeRequestStep instances from the real DI
        // container for each split-out request.
        var pipeline = Substitute.For<IPipeline>();
        var generateStepsForRequestsStep = new GenerateStepsForRequestsStep(accessor, pipeline);
        await generateStepsForRequestsStep.Execute(appContext);

        // Each independent request yields exactly 3 steps (Parse/Execute/Dispose), so 9 steps
        // scheduled means exactly 3 requests were split out of the expanded content.
        pipeline.Received(1).InsertSteps(
            generateStepsForRequestsStep,
            Arg.Is<IPipelineStep[]>(steps => steps.Length == 9));
    }
}
