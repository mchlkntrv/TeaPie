var companyNames = new[] { "Acme Corp", "Globex Inc", "Initech" };

// IMPORTANT: any code OUTSIDE a tp.Test(...) block in a -test.csx script runs
// immediately when the script is compiled - which happens BEFORE the HTTP
// requests for this test case are actually sent. Only code INSIDE tp.Test(...)
// is deferred (registered, then run later by RunScriptTestsStep, once all
// requests have completed) - so reading tp.Responses must always happen
// inside a tp.Test(...) block, never at the top level of the script.
await tp.Test(
    "All three company-creation requests should return 201, and their data " +
    "is captured for the next test case's loop.",
    async () =>
    {
        var createdCompanies = new List<object>();
        for (var i = 1; i <= companyNames.Length; i++)
        {
            Equal(201, tp.Responses[$"CreateCompany{i}"].StatusCode());

            dynamic body = await tp.Responses[$"CreateCompany{i}"].GetBodyAsExpandoAsync();
            createdCompanies.Add(new { Name = (string)body.title, CompanyId = i });
        }

        // Build a brand-new collection out of THIS loop's own responses, and hand
        // it to the next test case - the next loop will iterate over data that
        // only existed after this loop actually ran, not over anything from
        // -init.csx.
        tp.SetVariable("CreatedCompanies", createdCompanies);
    });
