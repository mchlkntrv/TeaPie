var companyNames = new[] { "Acme Corp", "Globex Inc", "Initech" };

// Rule from the 005 demo: anything touching tp.Responses must live INSIDE
// tp.Test(...), never at the top level of the script.
await tp.Test(
    "All three company-creation requests should return 201, and their data is captured for the next test case.",
    async () =>
    {
        var createdCompanies = new List<object>();
        for (var i = 1; i <= companyNames.Length; i++)
        {
            Equal(201, tp.Responses[$"CreateCompany{i}"].StatusCode());

            dynamic body = await tp.Responses[$"CreateCompany{i}"].GetBodyAsExpandoAsync();
            createdCompanies.Add(new { CompanyName = (string)body.title, CompanyId = i });
        }

        // Level 1 of the chain: companies created here feed the next test case's loop.
        tp.SetVariable("CreatedCompanies", createdCompanies);
    });
