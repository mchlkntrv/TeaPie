tp.Test("Status code should be 201 (Created)", context =>
    context.Response.StatusCode.Should().Be(201);
);

tp.ScopeVariables.Set("Cars-NewCar-Id", tp.Context.Respose.Body["Id"]);
tp.NextTestCase("Get All Cars");

