tp.Logger.LogInformation("Test1...Test1...Test1...Test1...Test1...Test1");

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

