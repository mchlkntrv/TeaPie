using FluentAssertions;
using static TeaPie.Tests.Templating.TemplatingTestHelpers;

namespace TeaPie.Tests.Templating;

public class TemplatingEndToEndShould
{
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

    [Fact]
    public void RenderTheLoopOverPartnersFixtureIdenticallyToTodaysOutputAheadOfTheWholeFileRenderRefactor()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "001-Loop-Over-Partners", "001-loop-over-partners-req.http");
        var original = File.ReadAllText(fixturePath);
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Partners", new[]
        {
            new { Name = "Acme Corp", RegistrationId = "01245" },
            new { Name = "Beta LLC", RegistrationId = "012426" }
        });

        var result = CreateExpander(variables).Expand(original, fixturePath);

        result.Should().Be(
            "\r\n### Create partner 1: Acme Corp\r\n" +
            "# @name CreatePartner1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Acme Corp\", \"body\": \"Registration ID 01245\", \"userId\": 1 }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create partner 2: Beta LLC\r\n" +
            "# @name CreatePartner2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Beta LLC\", \"body\": \"Registration ID 012426\", \"userId\": 2 }\r\n" +
            "\r\n" +
            "\r\n");
    }

    [Fact]
    public void RenderTheTwoLoopsOneFileFixtureIdenticallyToTodaysOutputAheadOfTheWholeFileRenderRefactor()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "002-Two-Loops-One-File", "002-two-loops-one-file-req.http");
        var original = File.ReadAllText(fixturePath);
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Products", new[]
        {
            new { Name = "Widget" },
            new { Name = "Gadget" }
        });
        variables.SetVariable("Categories", new[]
        {
            new { Name = "Electronics" },
            new { Name = "Furniture" }
        });

        var result = CreateExpander(variables).Expand(original, fixturePath);

        result.Should().Be(
            "\r\n### Create product 1: Widget\r\n" +
            "# @name CreateProduct1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Widget\", \"type\": \"product\" }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create product 2: Gadget\r\n" +
            "# @name CreateProduct2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Gadget\", \"type\": \"product\" }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Plain, non-loop request placed between the two loops\r\n" +
            "# @name MidMarker\r\n" +
            "## TEST-EXPECT-STATUS: [200]\r\n" +
            "GET {{ApiBaseUrl}}/posts/1\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create category 1: Electronics\r\n" +
            "# @name CreateCategory1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Electronics\", \"type\": \"category\" }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create category 2: Furniture\r\n" +
            "# @name CreateCategory2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Furniture\", \"type\": \"category\" }\r\n" +
            "\r\n" +
            "\r\n");
    }

    [Fact]
    public void RenderTheNumericRangeNoVariableFixtureIdenticallyToTodaysOutputAheadOfTheWholeFileRenderRefactor()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "003-Numeric-Range-No-Variable", "003-numeric-range-no-variable-req.http");
        var original = File.ReadAllText(fixturePath);
        var variables = new global::TeaPie.Variables.Variables();

        var result = CreateExpander(variables).Expand(original, fixturePath);

        result.Should().Be(
            "\r\n### Create item 1\r\n" +
            "# @name CreateItem1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Item 1\", \"index\": 1 }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create item 2\r\n" +
            "# @name CreateItem2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Item 2\", \"index\": 2 }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create item 3\r\n" +
            "# @name CreateItem3\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Item 3\", \"index\": 3 }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create item 4\r\n" +
            "# @name CreateItem4\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Item 4\", \"index\": 4 }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create item 5\r\n" +
            "# @name CreateItem5\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Item 5\", \"index\": 5 }\r\n" +
            "\r\n" +
            "\r\n");
    }

    [Fact]
    public void RenderTheLoopFeedsLoopCreateCompaniesFixtureIdenticallyToTodaysOutputAheadOfTheWholeFileRenderRefactor()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "005-Loop-Feeds-Loop", "001-Create-Companies", "001-create-companies-req.http");
        var original = File.ReadAllText(fixturePath);
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new[]
        {
            new { Name = "Acme Corp" },
            new { Name = "Beta LLC" }
        });

        var result = CreateExpander(variables).Expand(original, fixturePath);

        result.Should().Be(
            "\r\n### Create company 1: Acme Corp\r\n" +
            "# @name CreateCompany1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Acme Corp\", \"userId\": 1 }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create company 2: Beta LLC\r\n" +
            "# @name CreateCompany2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Beta LLC\", \"userId\": 2 }\r\n" +
            "\r\n" +
            "\r\n");
    }

    [Fact]
    public void RenderTheLoopFeedsLoopCreateLicensesFixtureIdenticallyToTodaysOutputAheadOfTheWholeFileRenderRefactor()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "005-Loop-Feeds-Loop", "002-Create-Licenses", "002-create-licenses-req.http");
        var original = File.ReadAllText(fixturePath);
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("CreatedCompanies", new[]
        {
            new { CompanyId = 101, Name = "Acme Corp" },
            new { CompanyId = 102, Name = "Beta LLC" }
        });

        var result = CreateExpander(variables).Expand(original, fixturePath);

        result.Should().Be(
            "\r\n### Create license for company 101: Acme Corp\r\n" +
            "# @name CreateLicense1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"License for Acme Corp\", \"companyId\": 101 }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create license for company 102: Beta LLC\r\n" +
            "# @name CreateLicense2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"License for Beta LLC\", \"companyId\": 102 }\r\n" +
            "\r\n" +
            "\r\n");
    }

    [Fact]
    public void RenderTheTripleChainCreateCompaniesFixtureIdenticallyToTodaysOutputAheadOfTheWholeFileRenderRefactor()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "006-Triple-Chain", "001-Create-Companies", "001-create-companies-req.http");
        var original = File.ReadAllText(fixturePath);
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new[]
        {
            new { Name = "Acme Corp" },
            new { Name = "Beta LLC" }
        });

        var result = CreateExpander(variables).Expand(original, fixturePath);

        result.Should().Be(
            "\r\n### Create company 1: Acme Corp\r\n" +
            "# @name CreateCompany1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Acme Corp\", \"userId\": 1 }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create company 2: Beta LLC\r\n" +
            "# @name CreateCompany2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Beta LLC\", \"userId\": 2 }\r\n" +
            "\r\n" +
            "\r\n");
    }

    [Fact]
    public void RenderTheTripleChainCreateLicensesFixtureIdenticallyToTodaysOutputAheadOfTheWholeFileRenderRefactor()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "006-Triple-Chain", "002-Create-Licenses", "002-create-licenses-req.http");
        var original = File.ReadAllText(fixturePath);
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("CreatedCompanies", new[]
        {
            new { CompanyId = 101, CompanyName = "Acme Corp" },
            new { CompanyId = 102, CompanyName = "Beta LLC" }
        });

        var result = CreateExpander(variables).Expand(original, fixturePath);

        result.Should().Be(
            "\r\n### Create license for company 101: Acme Corp\r\n" +
            "# @name CreateLicense1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"License for Acme Corp\", \"companyId\": 101 }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create license for company 102: Beta LLC\r\n" +
            "# @name CreateLicense2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"License for Beta LLC\", \"companyId\": 102 }\r\n" +
            "\r\n" +
            "\r\n");
    }

    [Fact]
    public void RenderTheTripleChainCreateInvoicesFixtureIdenticallyToTodaysOutputAheadOfTheWholeFileRenderRefactor()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "006-Triple-Chain", "003-Create-Invoices", "003-create-invoices-req.http");
        var original = File.ReadAllText(fixturePath);
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("CreatedLicenses", new[]
        {
            new { LicenseId = 201, CompanyId = 101, CompanyName = "Acme Corp" },
            new { LicenseId = 202, CompanyId = 102, CompanyName = "Beta LLC" }
        });

        var result = CreateExpander(variables).Expand(original, fixturePath);

        result.Should().Be(
            "\r\n### Create invoice for license 201 (Acme Corp)\r\n" +
            "# @name CreateInvoice1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Invoice for Acme Corp\", \"licenseId\": 201, \"companyId\": 101 }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create invoice for license 202 (Beta LLC)\r\n" +
            "# @name CreateInvoice2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Invoice for Beta LLC\", \"licenseId\": 202, \"companyId\": 102 }\r\n" +
            "\r\n" +
            "\r\n");
    }

    [Fact]
    public void RenderTheKitchenSinkMixedBatchFixtureIdenticallyToTodaysOutputAheadOfTheWholeFileRenderRefactor()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "007-Kitchen-Sink", "001-Mixed-Batch", "001-mixed-batch-req.http");
        var original = File.ReadAllText(fixturePath);
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Partners", new[]
        {
            new { Name = "Acme Corp" },
            new { Name = "Beta LLC" }
        });
        variables.SetVariable("Departments", new[]
        {
            new { Name = "Sales" },
            new { Name = "Support" }
        });

        var result = CreateExpander(variables).Expand(original, fixturePath);

        result.Should().Be(
            "### Health check (classic request, no loop involved at all)\r\n" +
            "# @name HealthCheck\r\n" +
            "## TEST-EXPECT-STATUS: [200]\r\n" +
            "GET {{ApiBaseUrl}}/posts/1\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create partner 1: Acme Corp\r\n" +
            "# @name CreatePartner1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Acme Corp\", \"type\": \"partner\" }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create partner 2: Beta LLC\r\n" +
            "# @name CreatePartner2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Beta LLC\", \"type\": \"partner\" }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Batch marker between the two loops (classic request, no loop)\r\n" +
            "# @name BatchMarker\r\n" +
            "## TEST-EXPECT-STATUS: [200]\r\n" +
            "GET {{ApiBaseUrl}}/posts/2\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create department 1: Sales\r\n" +
            "# @name CreateDepartment1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Sales\", \"type\": \"department\" }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create department 2: Support\r\n" +
            "# @name CreateDepartment2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Support\", \"type\": \"department\" }\r\n" +
            "\r\n" +
            "\r\n");
    }

    [Fact]
    public void RenderTheKitchenSinkFollowUpFixtureIdenticallyToTodaysOutputAheadOfTheWholeFileRenderRefactor()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "007-Kitchen-Sink", "002-Follow-Up", "002-follow-up-req.http");
        var original = File.ReadAllText(fixturePath);
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("CreatedPartners", new[]
        {
            new { PartnerId = 301, Name = "Acme Corp" },
            new { PartnerId = 302, Name = "Beta LLC" }
        });

        var result = CreateExpander(variables).Expand(original, fixturePath);

        result.Should().Be(
            "### Pre-check before the follow-up loop (classic request, no loop)\r\n" +
            "# @name PreCheck\r\n" +
            "## TEST-EXPECT-STATUS: [200]\r\n" +
            "GET {{ApiBaseUrl}}/posts/3\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create license for partner 301: Acme Corp\r\n" +
            "# @name CreateLicense1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"License for Acme Corp\", \"partnerId\": 301 }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create license for partner 302: Beta LLC\r\n" +
            "# @name CreateLicense2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"License for Beta LLC\", \"partnerId\": 302 }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Closing request after the follow-up loop (classic request, no loop)\r\n" +
            "# @name Closing\r\n" +
            "## TEST-EXPECT-STATUS: [200]\r\n" +
            "GET {{ApiBaseUrl}}/posts/4\r\n");
    }

    [Fact]
    public void RenderTheInlineLiteralFixtureIdenticallyToTodaysOutputAheadOfTheWholeFileRenderRefactor()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "009-Inline-Literal", "009-inline-literal-req.http");
        var original = File.ReadAllText(fixturePath);
        var variables = new global::TeaPie.Variables.Variables();

        var result = CreateExpander(variables).Expand(original, fixturePath);

        result.Should().Be(
            "\r\n### Create listing #1 with status: new\r\n" +
            "# @name CreateListing1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Listing new\" }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create listing #2 with status: used\r\n" +
            "# @name CreateListing2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Listing used\" }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create listing #3 with status: certified\r\n" +
            "# @name CreateListing3\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Listing certified\" }\r\n" +
            "\r\n" +
            "\r\n");
    }

    [Fact]
    public void RenderTheDuplicateNameWarningFixtureIdenticallyToTodaysOutputAheadOfTheWholeFileRenderRefactor()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "010-Duplicate-Name-Warning", "010-duplicate-name-warning-req.http");
        var original = File.ReadAllText(fixturePath);
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Partners", new[]
        {
            new { Name = "Acme Corp", RegistrationId = "01245" },
            new { Name = "Beta LLC", RegistrationId = "012426" }
        });

        var result = CreateExpander(variables).Expand(original, fixturePath);

        result.Should().Be(
            "\r\n### Create partner 1: Acme Corp\r\n" +
            "# @name CreatePartner\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Acme Corp\", \"body\": \"Registration ID 01245\" }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create partner 2: Beta LLC\r\n" +
            "# @name CreatePartner\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Beta LLC\", \"body\": \"Registration ID 012426\" }\r\n" +
            "\r\n" +
            "\r\n");
    }

    [Fact]
    public void RenderTheTpFileLoopFixtureHttpSectionIdenticallyToTodaysOutputAheadOfTheWholeFileRenderRefactor()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "008-Tp-File-Loop", "008-tp-file-loop.tp");
        var fullContent = File.ReadAllText(fixturePath);
        const string httpMarker = "--- HTTP";
        const string testMarker = "--- TEST";
        var httpStart = fullContent.IndexOf(httpMarker, StringComparison.Ordinal) + httpMarker.Length;
        var testStart = fullContent.IndexOf(testMarker, httpStart, StringComparison.Ordinal);
        var httpSection = fullContent[httpStart..testStart].Trim() + "\n";
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Products", new[]
        {
            new { Name = "Widget" },
            new { Name = "Gadget" },
            new { Name = "Gizmo" }
        });

        var result = CreateExpander(variables).Expand(httpSection, fixturePath);

        result.Should().Be(
            "\r\n### Create product 1: Widget\r\n" +
            "# @name CreateProduct1\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Widget\", \"type\": \"product\" }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create product 2: Gadget\r\n" +
            "# @name CreateProduct2\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Gadget\", \"type\": \"product\" }\r\n" +
            "\r\n" +
            "\r\n" +
            "### Create product 3: Gizmo\r\n" +
            "# @name CreateProduct3\r\n" +
            "## TEST-EXPECT-STATUS: [201]\r\n" +
            "POST {{ApiBaseUrl}}/posts\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{ \"title\": \"Gizmo\", \"type\": \"product\" }\r\n" +
            "\r\n" +
            "\n");
    }

    [Fact]
    public void RenderTheNestedLoopsFixtureWithCorrectOuterInnerIndexing()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "templating-demo", "Tests", "014-Nested-Loops", "014-nested-loops-req.http");
        var original = File.ReadAllText(fixturePath);
        var variables = new global::TeaPie.Variables.Variables();
        variables.SetVariable("Companies", new object[]
        {
            new { Name = "Acme", Licenses = new[] { "BASIC", "PRO" } },
            new { Name = "Globex", Licenses = new[] { "BASIC" } }
        });

        var result = CreateExpander(variables).Expand(original, fixturePath);

        result.Should().Contain("### Create license 1.1: Acme / BASIC");
        result.Should().Contain("### Create license 1.2: Acme / PRO");
        result.Should().Contain("### Create license 2.1: Globex / BASIC");
        result.Should().Contain("# @name CreateLicense1_1");
        result.Should().Contain("# @name CreateLicense2_1");
    }
}
