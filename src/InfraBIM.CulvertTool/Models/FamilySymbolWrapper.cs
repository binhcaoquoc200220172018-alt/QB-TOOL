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
                if (_symbol != null)
                {
                    try
                    {
                        if (_symbol.IsValidObject)
                        {
                            var dummy = _symbol.Id;
                            return _symbol;
                        }
                    }
                    catch
                    {
                        _symbol = null;
                    }
                }
                return null;
            }
            set => _symbol = value;
        }

        public FamilySymbolWrapper(FamilySymbol sym)
        {
            _symbol = sym;

            ElementId id = ElementId.InvalidElementId;
            string famName = string.Empty;
            string typeName = string.Empty;

            try
            {
                if (sym != null && sym.IsValidObject)
                {
                    id = sym.Id;
                }
            }
            catch { }

            try
            {
                if (sym != null && sym.IsValidObject)
                {
                    famName = sym.FamilyName ?? string.Empty;
                }
            }
            catch { }

            try
            {
                if (sym != null && sym.IsValidObject)
                {
                    typeName = sym.Name ?? string.Empty;
                }
            }
            catch { }

            Id = id;
            FamilyName = famName;
            TypeName = typeName;
            DisplayName = $"{FamilyName} : {TypeName}";
        }

        public FamilySymbolWrapper(ElementId id, string familyName, string typeName)
        {
            _symbol = null;
            Id = id ?? ElementId.InvalidElementId;
            FamilyName = familyName ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            DisplayName = $"{FamilyName} : {TypeName}";
        }

        public FamilySymbol? GetFreshSymbol(Document? doc)
        {
            if (_symbol != null)
            {
                try
                {
                    if (_symbol.IsValidObject)
                    {
                        var testId = _symbol.Id;
                        if (doc != null && doc.IsValidObject)
                        {
                            var elem = doc.GetElement(testId);
                            if (elem is FamilySymbol fs && fs.IsValidObject)
                            {
                                _symbol = fs;
                                return fs;
                            }
                        }
                        else
                        {
                            return _symbol;
                        }
                    }
                }
                catch
                {
                    _symbol = null;
                }
            }

            if (doc != null && doc.IsValidObject)
            {
                if (Id != null && Id != ElementId.InvalidElementId)
                {
                    try
                    {
                        if (doc.GetElement(Id) is FamilySymbol elem && elem.IsValidObject)
                        {
                            _symbol = elem;
                            return elem;
                        }
                    }
                    catch { }
                }

                // Fallback: Tìm theo FamilyName và TypeName
                if (!string.IsNullOrEmpty(FamilyName) || !string.IsNullOrEmpty(TypeName))
                {
                    try
                    {
                        var match = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilySymbol))
                            .Cast<FamilySymbol>()
                            .FirstOrDefault(f =>
                                (string.IsNullOrEmpty(FamilyName) || string.Equals(f.FamilyName, FamilyName, StringComparison.OrdinalIgnoreCase)) &&
                                (string.IsNullOrEmpty(TypeName) || string.Equals(f.Name, TypeName, StringComparison.OrdinalIgnoreCase)));
                        if (match != null && match.IsValidObject)
                        {
                            _symbol = match;
                            return match;
                        }
                    }
                    catch { }
                }
            }

            return null;
        }

        public override string ToString() => DisplayName;
    }
}
