using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace TeaPieDraft.ScriptHandling
{
    internal class ScriptCompiler
    {
        internal static TeaPie? UserContext { get; set; }

        internal static IEnumerable<string> DefaultImports = [
            "System",
            "System.Collections.Generic",
            "System.IO",
            "System.Linq",
            "System.Net.Http",
            "System.Threading",
            "System.Threading.Tasks",
            "Microsoft.Extensions.Logging"
        ];

        public ScriptCompiler() { }
        public ScriptCompiler(TeaPie userContext) => UserContext = userContext;

        public (Script<object>, Compilation) CompileScript(string scriptContent)
        {
            var scriptOptions = ScriptOptions.Default
                .AddReferences(AppDomain.CurrentDomain.GetAssemblies().Where(x => !string.IsNullOrEmpty(x.Location)))
                .WithImports(DefaultImports);

            var script = CSharpScript.Create(scriptContent, scriptOptions, typeof(Globals));
            var compilation = script.GetCompilation();

            return (script, compilation);
        }
    }

    public class Globals
    {
#pragma warning disable IDE1006 // Naming Styles
        public TeaPie? tp { get; set; } = ScriptCompiler.UserContext;
#pragma warning restore IDE1006 // Naming Styles
    }
}
