var expectedPartners = new[] { "Acme Corp", "Globex Inc", "Initech" };

await tp.Test("All three loop-generated requests should return 201 (Created).", async () =>
{
    for (var i = 1; i <= expectedPartners.Length; i++)
    {
        Equal(201, tp.Responses[$"CreatePartner{i}"].StatusCode());
    }
});

await tp.Test("Each loop iteration should have sent its own partner's data (not a copy of one).", async () =>
{
    for (var i = 0; i < expectedPartners.Length; i++)
    {
        dynamic responseJson = await tp.Responses[$"CreatePartner{i + 1}"].GetBodyAsExpandoAsync();
        Equal(expectedPartners[i], responseJson.title);
    }
});
