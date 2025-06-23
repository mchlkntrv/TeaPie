using TeaPie.DotnetTool;

var app = ApplicationProvider.GetApplication();

if (!args.Contains("--no-logo"))
{
    Displayer.DisplayApplicationHeader();
}

return await app.RunAsync(args);
