using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace CSharpScriptingPrototype
{
    public class ScriptRunnerWithNugetPackagesSupport
    {
        public static async Task RunScript(string scriptPath)
        {
            var packagePath = $"{Environment.CurrentDirectory}/packages";
            List<(string PackageName, string Version)> nugetPackages = await ProcessNugetPackages(scriptPath);
            foreach (var package in nugetPackages)
            {
                await DownloadNuget(packagePath, package.PackageName, package.Version);
            }

            ScriptOptions scriptOptions = await PrepareScriptOptions(packagePath, scriptPath, nugetPackages);

            Context context = new Context() { Id = 1, Name = "Newtonsoft.Json Package" };
            var result = await CSharpScript.EvaluateAsync<string>(
                File.ReadAllText(scriptPath),
                scriptOptions,
                globals: new Globals { Context = context },
                globalsType: typeof(Globals)
            );

        }

        private static async Task<ScriptOptions> PrepareScriptOptions(string packagePath, string scriptPath, List<(string PackageName, string Version)> nugetPackages)
        {
            var scriptOptions = ScriptOptions.Default;
            foreach (var nugetPackage in nugetPackages)
            {
                // Tu by ste volali kód na stiahnutie a načítanie balíčkov
                // Pridanie referencií na DLLs
                var packageDlls = Directory.GetFiles(packagePath + "/" + nugetPackage.PackageName, "*.dll", SearchOption.AllDirectories);
                foreach (var dll in packageDlls)
                {
                    scriptOptions = scriptOptions.AddReferences(MetadataReference.CreateFromFile(dll));
                }
            }

            await Task.CompletedTask;
            return scriptOptions;
        }

        private static async Task<List<(string PackageName, string Version)>> ProcessNugetPackages(string scriptPath)
        {
            var scriptContent = File.ReadAllLines(scriptPath);
            var nugetPackages = new List<(string PackageName, string Version)>();

            foreach (var line in scriptContent)
            {
                if (line.TrimStart().StartsWith("// nuget:"))
                {
                    var packageInfo = line.Substring("// nuget:".Length).Trim();
                    var parts = packageInfo.Split(',');
                    if (parts.Length == 2)
                    {
                        nugetPackages.Add((parts[0].Trim(), parts[1].Trim()));
                    }
                }
            }

            await Task.CompletedTask;
            return nugetPackages;
        }

        public static async Task DownloadNuget(string packagePath, string packageID, string version)
        {
            var logger = NullLogger.Instance;
            var cache = new SourceCacheContext();
            var repositories = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");

            var resource = await repositories.GetResourceAsync<FindPackageByIdResource>();
            var packageVersion = new NuGetVersion(version);
            var dependencyInfoResource = await repositories.GetResourceAsync<DependencyInfoResource>();
            var dependencyInfo = await dependencyInfoResource.ResolvePackage(
                new PackageIdentity(packageID, packageVersion),
                FrameworkConstants.CommonFrameworks.NetStandard20,
                cache,
                logger,
                CancellationToken.None);

            foreach (var dependency in dependencyInfo.Dependencies)
            {
                // Handle dependencies here
            }

            var packageDownloadContext = new PackageDownloadContext(cache);
            var downloadResource = await repositories.GetResourceAsync<DownloadResource>();
            var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                new PackageIdentity(packageID, packageVersion),
                packageDownloadContext,
                packagePath,
                logger,
                CancellationToken.None);

        }
    }

    public class Globals
    {
        public Context Context { get; set; }
    }

    public class Context
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
