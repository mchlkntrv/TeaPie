var partnerNames = new[] { "Acme Corp", "Globex Inc", "Initech" };
var departmentNames = new[] { "Sales", "Engineering" };

await tp.Test("The health check and batch marker (both classic, non-loop requests) should return 200.", async () =>
{
    Equal(200, tp.Responses["HealthCheck"].StatusCode());
    Equal(200, tp.Responses["BatchMarker"].StatusCode());
});

await tp.Test(
    "All partner-loop and department-loop requests should return 201, and the partners' data " +
    "is captured for the follow-up test case's loop.",
    async () =>
    {
        var createdPartners = new List<object>();
        for (var i = 1; i <= partnerNames.Length; i++)
        {
            Equal(201, tp.Responses[$"CreatePartner{i}"].StatusCode());
            createdPartners.Add(new { PartnerId = i, Name = partnerNames[i - 1] });
        }

        for (var i = 1; i <= departmentNames.Length; i++)
        {
            Equal(201, tp.Responses[$"CreateDepartment{i}"].StatusCode());
        }

        // Feeds the loop in 002-Follow-Up/002-follow-up-req.http.
        tp.SetVariable("CreatedPartners", createdPartners);
    });
