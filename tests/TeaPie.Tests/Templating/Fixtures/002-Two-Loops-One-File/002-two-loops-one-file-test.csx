var productNames = new[] { "Widget", "Gadget", "Gizmo" };
var categoryNames = new[] { "Electronics", "Tools" };

await tp.Test("All three product-loop requests should return 201.", async () =>
{
    for (var i = 1; i <= productNames.Length; i++)
    {
        Equal(201, tp.Responses[$"CreateProduct{i}"].StatusCode());
    }
});

await tp.Test("The plain marker request between the two loops should return 200.", async () =>
{
    Equal(200, tp.Responses["MidMarker"].StatusCode());
});

await tp.Test("All two category-loop requests should return 201.", async () =>
{
    for (var i = 1; i <= categoryNames.Length; i++)
    {
        Equal(201, tp.Responses[$"CreateCategory{i}"].StatusCode());
    }
});

await tp.Test("Each product request should carry its own product's data.", async () =>
{
    for (var i = 0; i < productNames.Length; i++)
    {
        dynamic body = await tp.Responses[$"CreateProduct{i + 1}"].GetBodyAsExpandoAsync();
        Equal(productNames[i], body.title);
        Equal("product", body.type);
    }
});

await tp.Test("Each category request should carry its own category's data.", async () =>
{
    for (var i = 0; i < categoryNames.Length; i++)
    {
        dynamic body = await tp.Responses[$"CreateCategory{i + 1}"].GetBodyAsExpandoAsync();
        Equal(categoryNames[i], body.title);
        Equal("category", body.type);
    }
});
