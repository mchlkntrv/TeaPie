tp.Test("Number of cars should be greater than zero", () => {
    var previousNumber = 0;
    if (tp.HasVariable("Cars-NumberOfCars")){
        previousNumber = tp.GetVariable("Cars-NumberOfCars");
    }

    tp.Response.Body.ToJson().Count.Should().Be().GreaterThan(previousNumber);
});

tp.SetVariable("Cars-NumberOfCars", tp.Response.Body.ToJson().Count);

