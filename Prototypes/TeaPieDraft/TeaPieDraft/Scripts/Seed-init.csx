tp.Logger.LogInformation("I am Seed(less) script... (or water-melon?)");

int[] array = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
Console.WriteLine("All numbers: ");
foreach (var x in array)
{
    Console.Write(x + ", ");
}

Console.WriteLine("\nOdd numbers: ");
List<int> list = [.. array.Where(x => x % 2 != 0)];
list.ForEach(x => Console.Write(x + ", "));
Console.WriteLine();
