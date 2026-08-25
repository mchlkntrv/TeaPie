// Demonstrates the templating loop feature: a request block can be repeated once per
// item in a collection, producing N independent requests instead of one hand-written request.
#load "$teapie/Definitions/GenerateNewCar.csx"

// Generate a batch of fake cars - each one becomes its own POST request in the loop below.
var newCars = GenerateCars(5);

tp.SetVariable("NewCarsBatch", newCars, "cars");
tp.SetVariable("NewCarsBatchCount", newCars.Count, "cars");

// Kept separately (instead of relying on the 'Car' type in the test script) so the
// post-response script doesn't need to load the 'Car' class definition itself.
tp.SetVariable("NewCarsBatchBrands", newCars.Select(car => car.Brand).ToList(), "cars");
