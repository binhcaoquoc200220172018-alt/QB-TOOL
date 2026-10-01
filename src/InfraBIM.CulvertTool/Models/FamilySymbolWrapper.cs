using Autodesk.Revit.DB;

namespace InfraBIM.CulvertTool.Models
{
    public class FamilySymbolWrapper
    {
        public FamilySymbol Symbol { get; }
        public string DisplayName { get; }
        public string FamilyName => Symbol?.FamilyName ?? string.Empty;
        public string TypeName => Symbol?.Name ?? string.Empty;

        public FamilySymbolWrapper(FamilySymbol sym)
        {
            Symbol = sym;
            DisplayName = $"{sym.FamilyName} : {sym.Name}";
        }

        public override string ToString() => DisplayName;
    }
}
