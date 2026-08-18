var partnerNames = new[] { "Acme Corp", "Globex Inc", "Initech" };

await tp.Test("The pre-check and closing request (both classic, non-loop) should return 200.", async () =>
{
    Equal(200, tp.Responses["PreCheck"].StatusCode());
    Equal(200, tp.Responses["Closing"].StatusCode());
});

await tp.Test(
    "Every license request should return 201 and reference the correct partner - data that only " +
    "existed after the PREVIOUS test case's loop actually ran.",
    async () =>
    {
        for (var i = 1; i <= partnerNames.Length; i++)
        {
            Equal(201, tp.Responses[$"CreateLicense{i}"].StatusCode());

            dynamic body = await tp.Responses[$"CreateLicense{i}"].GetBodyAsExpandoAsync();
            Equal($"License for {partnerNames[i - 1]}", body.title);
            Equal(i, (int)body.partnerId);
        }
    });
