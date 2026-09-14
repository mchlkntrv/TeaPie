var companyNames = new[] { "Acme Corp", "Globex Inc", "Initech" };

await tp.Test("All three license-creation requests should return 201.", async () =>
{
    for (var i = 1; i <= companyNames.Length; i++)
    {
        Equal(201, tp.Responses[$"CreateLicense{i}"].StatusCode());
    }
});

await tp.Test("Each license should reference the correct company from the previous loop.", async () =>
{
    for (var i = 0; i < companyNames.Length; i++)
    {
        dynamic body = await tp.Responses[$"CreateLicense{i + 1}"].GetBodyAsExpandoAsync();
        Equal($"License for {companyNames[i]}", body.title);
        Equal(i + 1, (int)body.companyId);
    }
});
