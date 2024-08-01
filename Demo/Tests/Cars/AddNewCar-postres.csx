tp.Test("Status code should be 201 (Created)", context =>
    context.Response.StatusCode.Should().Be(201);
);

tp.RuntimeVariables.Add<int>(
    "Cars.NewCar.Id",
    tp.Context.Respose.Body["Id"]
);

