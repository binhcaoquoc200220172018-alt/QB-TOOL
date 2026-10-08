using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace InfraBIM.CulvertTool.Models
{
    public class FamilySymbolWrapper
    {
        private FamilySymbol? _symbol;

        public ElementId Id { get; }
        public string FamilyName { get; }
        public string TypeName { get; }
        public string DisplayName { get; }

        public FamilySymbol? Symbol
        {
            get
            {
                if (_symbol != null && _symbol.IsValidObject) return _symbol;
                return _symbol;
            }
            set => _symbol = value;
        }

        public FamilySymbolWrapper(FamilySymbol sym)
        {
            _symbol = sym;
            Id = sym.Id;
            FamilyName = sym.FamilyName ?? string.Empty;
            TypeName = sym.Name ?? string.Empty;
            DisplayName = $"{FamilyName} : {TypeName}";
        }

        public FamilySymbol? GetFreshSymbol(Document? doc)
        {
            if (_symbol != null && _symbol.IsValidObject) return _symbol;
            if (doc != null && doc.IsValidObject)
            {
                if (Id != null && Id != ElementId.InvalidElementId)
                {
                    if (doc.GetElement(Id) is FamilySymbol elem && elem.IsValidObject)
                    {
                        _symbol = elem;
                        return elem;
                    }
                }

                // Fallback: Tìm theo FamilyName và TypeName
                var match = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(f => string.Equals(f.FamilyName, FamilyName, StringComparison.OrdinalIgnoreCase) &&
                                         string.Equals(f.Name, TypeName, StringComparison.OrdinalIgnoreCase));
                if (match != null && match.IsValidObject)
                {
                    _symbol = match;
                    return match;
                }
            }
            return _symbol;
        }

        public override string ToString() => DisplayName;
    }
}
