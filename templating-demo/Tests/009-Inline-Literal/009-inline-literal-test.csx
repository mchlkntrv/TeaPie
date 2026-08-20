var statuses = new[] { "new", "used", "certified" };

await tp.Test("All three inline-literal-driven requests should return 201 with the correct status.", async () =>
{
    for (var i = 1; i <= statuses.Length; i++)
    {
        Equal(201, tp.Responses[$"CreateListing{i}"].StatusCode());

        dynamic body = await tp.Responses[$"CreateListing{i}"].GetBodyAsExpandoAsync();
        Equal($"Listing {statuses[i - 1]}", body.title);
    }
});
