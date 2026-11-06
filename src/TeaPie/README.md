# TeaPie - Library

[![NuGet](https://img.shields.io/nuget/v/TeaPie)](https://www.nuget.org/packages/TeaPie/)
[![License](https://img.shields.io/github/license/Kros-sk/TeaPie)](../../LICENSE)
[![Build](https://github.com/Kros-sk/TeaPie/actions/workflows/pipeline.yml/badge.svg)](https://github.com/Kros-sk/TeaPie/actions)

The TeaPie library enables you to programmatically run and extend the functionality of the [TeaPie CLI tool](https://www.nuget.org/packages/TeaPie.Tool/). It is ideal for integrating TeaPie into your own tools, CI/CD pipelines, or building custom extensions.

## 📖 Documentation

📚 **Complete documentation of the public API is available in** **[Wiki - API](https://www.teapie.fun/api/TeaPie.html)**.

## 🚀 Features

✅ Programmatic **test execution** of `.http` files

✅ **Environment** & **variable** injection

✅ **Scriptable initialization** logic

✅ Customizable **logging** and **reporting**

## 📦 Installation

### Install via NuGet

To integrate the package into your C# .NET project:

```sh
dotnet add package TeaPie
```

## ⚡ Quick Start

The access point to functionality is represented by `ApplicationBuilder` class. Here is an example of usage:

```csharp
var appBuilder = ApplicationBuilder.Create(isCollectionRun: true);

var app = appBuilder
    .WithPath("path/to/collection")
    .WithTemporaryPath("custom/temp/folder")
    .WithLogging(LogLevel.Information, "path/to/log-file.log", LogLevel.Debug)
    .WithEnvironment("testlab")
    .WithEnvironmentFile("path/to/my/environment-file.json")
    .WithReportFile("path/to/report-file.xml")
    .WithInitializationScript("path/to/my/init-script.csx")
    .WithVariablesCaching(false)
    .WithExpandedContentDump("path/to/dump-folder")
    .WithDefaultPipeline(); // Default pipeline runs tests
    // .WithStructureExplorationPipeline() - to show structure
    .Build();

await app.Run(CancellationToken.None);
```

`WithExpandedContentDump()` shows each templated requests file after its [templates are expanded](https://www.teapie.fun/docs/templating-loops.html). With no argument (or an empty string) it prints the files to the console. With a directory it writes them there instead; a relative path is resolved against the current working directory, and the directory must be outside the tested collection.

## 🤝 Contributing

We welcome contributions! Please check out the [Contribution Guide](../../CONTRIBUTING.md) for details on how to get involved.

## 📝 License

TeaPie is licensed under the **MIT License**. See the [LICENSE](../../LICENSE) file for details.
