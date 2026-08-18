await tp.Test("All five range-loop requests should return 201.", async () =>
{
    for (var i = 1; i <= 5; i++)
    {
        Equal(201, tp.Responses[$"CreateItem{i}"].StatusCode());
    }
});

await tp.Test("Each range-loop request should carry its own numeric index.", async () =>
{
    for (var i = 1; i <= 5; i++)
    {
        dynamic body = await tp.Responses[$"CreateItem{i}"].GetBodyAsExpandoAsync();
        Equal(i, (int)body.index);
    }
});
