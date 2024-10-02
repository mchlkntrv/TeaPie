#load ".././init.csx"
#nuget "Newtonsoft.Json, 13.0.3"

using Newtonsoft.Json;

tp.Logger.LogInformation("I am Test11 and I am about to run (marathon)...");

Initialization();

public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }

    public string ToString() => $"{Name}, {Age}";
}

var personJson = "{ \"name\": \"Abraham Lincoln\", \"age\": 24 }";
Console.WriteLine("Person in JSON format:" + personJson);

var person = JsonConvert.DeserializeObject<Person>(personJson);
Console.WriteLine("JSON deserialized to Person class: " + person.ToString());


int[] array = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
Console.WriteLine("All numbers: ");
foreach (var x in array)
{
    Console.Write(x + ", ");
}

Console.WriteLine("\nEven numbers: ");
List<int> list = [.. array.Where(x => x % 2 == 0)];
list.ForEach(x => Console.Write(x + ", "));
Console.WriteLine();
