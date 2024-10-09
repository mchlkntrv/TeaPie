tp.Test("Status code should be 201 (Created)", () =>
    tp.Response.StatusCode.Should().Be(201);
);

tp.DeleteVariables("Cars");

