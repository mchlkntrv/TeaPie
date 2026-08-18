var companyNames = new[] { "Acme Corp", "Globex Inc", "Initech" };

await tp.Test(
    "All three invoices should return 201, and each one's title should still contain the ORIGINAL " +
    "company name that entered the chain three test cases ago.",
    async () =>
    {
        for (var i = 1; i <= companyNames.Length; i++)
        {
            Equal(201, tp.Responses[$"CreateInvoice{i}"].StatusCode());

            dynamic body = await tp.Responses[$"CreateInvoice{i}"].GetBodyAsExpandoAsync();
            Equal($"Invoice for {companyNames[i - 1]}", body.title);
            Equal(i, (int)body.licenseId);
            Equal(i, (int)body.companyId);
        }
    });
