namespace TeaPie.DotnetTool;

public static class Program
{
    public static async Task<int> RunAsync(string[] args)
    {
        var app = ApplicationProvider.GetApplication();

        if (!args.Contains("--no-logo"))
        {
            Displayer.DisplayApplicationHeader();
        }

        return await app.RunAsync(args);
    }

    public static async Task Main(string[] args)
    {
        await RunAsync(args);
    }
}
