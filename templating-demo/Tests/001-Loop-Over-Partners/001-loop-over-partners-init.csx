// Simulates data captured earlier in a real scenario (e.g. parsed from a previous
// response). Any named collection variable can be looped over in the request file
// using '{% for x in CollectionName %}'.
tp.SetVariable("Partners", new[]
{
    new { Name = "Acme Corp", RegistrationId = "01245" },
    new { Name = "Globex Inc", RegistrationId = "012426" },
    new { Name = "Initech", RegistrationId = "012427" }
});
