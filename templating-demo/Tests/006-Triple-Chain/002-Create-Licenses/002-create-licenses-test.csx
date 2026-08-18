var companyNames = new[] { "Acme Corp", "Globex Inc", "Initech" };

await tp.Test(
    "All three license-creation requests should return 201, and their data (plus the original " +
    "company name, carried forward one more hop) is captured for the final test case.",
    async () =>
    {
        var createdLicenses = new List<object>();
        for (var i = 1; i <= companyNames.Length; i++)
        {
            Equal(201, tp.Responses[$"CreateLicense{i}"].StatusCode());

            dynamic body = await tp.Responses[$"CreateLicense{i}"].GetBodyAsExpandoAsync();
            createdLicenses.Add(new
            {
                LicenseId = i,
                CompanyId = (int)body.companyId,
                CompanyName = companyNames[i - 1]
            });
        }

        // Level 2 of the chain: licenses created here (still carrying the ORIGINAL
        // company name from level 1) feed the third and final test case's loop.
        tp.SetVariable("CreatedLicenses", createdLicenses);
    });
