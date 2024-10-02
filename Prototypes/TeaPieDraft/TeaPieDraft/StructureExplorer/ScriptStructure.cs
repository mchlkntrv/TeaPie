using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;

namespace TeaPieDraft.StructureExplorer
{
    internal class ScriptStructure : FileStructure
    {
        public Script<object>? Script { get; set; }
        public Compilation? Compiled { get; set; }
    }

}