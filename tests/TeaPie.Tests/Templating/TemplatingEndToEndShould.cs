using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class TemplatingEndToEndShould
{
    private static TemplateExpander CreateExpander(global::TeaPie.Variables.IVariables? variables = null)
    {
        var vars = variables ?? new global::TeaPie.Variables.Variables();
        return new TemplateExpander(
            new LoopBlockScanner(),
            new LoopBodyMasker(),
            new CollectionSourceResolver(vars),
            new VariablesFluidModelBuilder(),
            vars);
    }

    [Fact]
    public void RoundTripEveryDemoRequestFileWithoutLoopTagsByteIdentically()
    {
        var demoRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "demo");
        var files = Directory.GetFiles(demoRoot, "*.http", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(demoRoot, "*.tp", SearchOption.AllDirectories));
        var expander = CreateExpander();

        foreach (var file in files)
        {
            var original = File.ReadAllText(file);

            if (original.Contains("{%", StringComparison.Ordinal))
            {
                continue;
            }

            var expanded = expander.Expand(original, file);
            expanded.Should().Be(original, $"file '{file}' does not contain '{{%' and must be returned unchanged");
        }
    }

    [Fact]
    public void ExpandPartnersStyleSeedScenarioForThreeItems()
    {
        const string template =
            "{% for partner in FreePartners %}### New partner {{ forloop.index }}\n" +
            "## TEST-EXPECT-STATUS: [201]\n" +
            "## TEST-JSON-HAS-ID-PROPERTY: Temp.FreePartners.PartnerId{{ forloop.index }}\n" +
            "POST {{ApiGatewayBaseUrl}}/companies/{{Temp.Partners.CompanyId}}/partners\n" +
            "Content-Type: application/json\n\n" +
            "{ \"registrationId\": \"{{ partner.RegistrationId }}\", \"isFree\": true }\n" +
            "{% endfor %}";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("FreePartners", new[]
        {
            new { RegistrationId = "01245" },
            new { RegistrationId = "012426" },
            new { RegistrationId = "012427" }
        });

        var result = CreateExpander(variables).Expand(template, "partners-seed.http");

        result.Should().Contain("### New partner 1");
        result.Should().Contain("Temp.FreePartners.PartnerId1");
        result.Should().Contain("\"registrationId\": \"01245\"");
        result.Should().Contain("### New partner 2");
        result.Should().Contain("Temp.FreePartners.PartnerId2");
        result.Should().Contain("\"registrationId\": \"012426\"");
        result.Should().Contain("### New partner 3");
        result.Should().Contain("Temp.FreePartners.PartnerId3");
        result.Should().Contain("\"registrationId\": \"012427\"");
        result.Should().Contain("POST {{ApiGatewayBaseUrl}}/companies/{{Temp.Partners.CompanyId}}/partners");
    }
}
