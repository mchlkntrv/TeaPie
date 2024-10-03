using Serilog;
using System.Reflection;
using TeaPieDraft;

var collectionPath = args is null || args.Length == 0
    ? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Scripts"
    : args[0];

var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var app = new Application(new SerilogLoggerAdapter<Program>(logger));
await app.Run(collectionPath!);
