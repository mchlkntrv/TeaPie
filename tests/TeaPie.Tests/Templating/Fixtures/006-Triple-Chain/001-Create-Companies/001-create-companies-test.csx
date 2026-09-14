var companyNames = new[] { "Acme Corp", "Globex Inc", "Initech" };

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

        tp.SetVariable("CreatedCompanies", createdCompanies);
    });
