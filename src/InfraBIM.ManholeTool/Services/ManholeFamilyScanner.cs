using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using InfraBIM.ManholeTool.Models;

namespace InfraBIM.ManholeTool.Services
{
    /// <summary>
    /// Quét và phân loại tham số của Family Hố Ga thành 3 nhóm:
    /// - Nhóm Hình học / Kích thước (Tab 2)
    /// - Nhóm Vật liệu (Tab 4)
    /// - Nhóm Phi hình học / Metadata (Tab 5)
    /// Kế thừa kiến trúc từ FamilyParameterScannerService của Tool Cống Ngang:
    /// 1. Ưu tiên mở doc.EditFamily để đọc FamilyManager giữ nguyên 100% thứ tự khai báo.
    /// 2. Lọc bỏ các tham số đã có Formula công thức tính toán.
    /// 3. Phân nhóm chuẩn xác: Dimensions và Yes/No (ẩn hiện cấu kiện).
    /// </summary>
    public static class ManholeFamilyScanner
    {
        public static (List<ManholeParameterItem> GeoParams, 
                       List<ManholeMaterialItem> MaterialParams, 
                       List<CustomNonGeoParamItem> NonGeoParams) 
            ScanFamilyParameters(Document doc, FamilySymbol symbol)
        {
            var geoList = new List<ManholeParameterItem>();
            var matList = new List<ManholeMaterialItem>();
            var nonGeoList = new List<CustomNonGeoParamItem>();

            if (symbol == null || doc == null) 
                return (geoList, matList, nonGeoList);

            var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // =========================================================================
            // 1. ƯU TIÊN 1: ĐỌC TỪ FAMILY DEFINITION (FamilyManager)
            // =========================================================================
            bool scannedFromFamily = false;
            Document? famDoc = null;
            bool famDocWasAlreadyOpen = false;

            try
            {
                if (symbol.Family != null)
                {
                    // 1.1 Kiểm tra nếu Family Document đã mở sẵn trong Revit
                    foreach (Document d in doc.Application.Documents)
                    {
                        if (d.IsFamilyDocument && string.Equals(d.Title, symbol.Family.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            famDoc = d;
                            famDocWasAlreadyOpen = true;
                            break;
                        }
                    }

                    // 1.2 Nếu chưa mở, mở an toàn bằng doc.EditFamily
                    if (famDoc == null && symbol.Family.IsEditable)
                    {
                        try
                        {
                            famDoc = doc.EditFamily(symbol.Family);
                        }
                        catch
                        {
                            famDoc = null;
                        }
                    }

                    if (famDoc != null && famDoc.FamilyManager != null)
                    {
                        ExtractParametersFromFamilyManager(famDoc, symbol, geoList, matList, nonGeoList, processedNames);
                        scannedFromFamily = geoList.Count > 0 || matList.Count > 0 || nonGeoList.Count > 0;
                    }
                }
            }
            catch { }
            finally
            {
                if (famDoc != null && !famDocWasAlreadyOpen)
                {
                    try { famDoc.Close(false); } catch { }
                }
            }

            // =========================================================================
            // 2. FALLBACK TỪ DỰ ÁN (SYMBOL & INSTANCE) NẾU CHƯA ĐỌC ĐƯỢC TỪ FAMILY
            // =========================================================================
            if (!scannedFromFamily)
            {
                // 2.1 Quét Type Parameters từ FamilySymbol
                foreach (Parameter p in symbol.Parameters)
                {
                    ProcessParameter(p, isType: true, geoList, matList, nonGeoList, processedNames);
                }

                // 2.2 Quét Instance Parameters từ Sample Instance có sẵn hoặc tạm thời
                FamilyInstance? sampleInst = null;
                try
                {
                    sampleInst = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .FirstOrDefault(fi => fi.Symbol != null && (fi.Symbol.Id == symbol.Id || 
                            (symbol.Family != null && fi.Symbol.Family != null && fi.Symbol.Family.Id == symbol.Family.Id)));
                }
                catch { }

                if (sampleInst != null)
                {
                    foreach (Parameter p in sampleInst.Parameters)
                    {
                        ProcessParameter(p, isType: false, geoList, matList, nonGeoList, processedNames);
                    }
                }
                else
                {
                    try
                    {
                        using var t = new Transaction(doc, "Temp Scan Instance");
                        t.Start();
                        if (!symbol.IsActive) symbol.Activate();

                        FamilyInstance? tempInst = doc.Create.NewFamilyInstance(XYZ.Zero, symbol, StructuralType.NonStructural);
                        if (tempInst != null)
                        {
                            foreach (Parameter p in tempInst.Parameters)
                            {
                                ProcessParameter(p, isType: false, geoList, matList, nonGeoList, processedNames);
                            }
                        }
                        t.RollBack();
                    }
                    catch { }
                }
            }

            // Gán số thứ tự hiển thị STT cho danh sách hình học
            for (int i = 0; i < geoList.Count; i++)
            {
                geoList[i].STT = i + 1;
            }

            return (geoList, matList, nonGeoList);
        }

        private static void ExtractParametersFromFamilyManager(
            Document famDoc,
            FamilySymbol symbol,
            List<ManholeParameterItem> geoList,
            List<ManholeMaterialItem> matList,
            List<CustomNonGeoParamItem> nonGeoList,
            HashSet<string> processed)
        {
            var famMgr = famDoc.FamilyManager;
            if (famMgr == null) return;

            foreach (FamilyParameter fp in famMgr.GetParameters())
            {
                if (fp.Definition == null || string.IsNullOrWhiteSpace(fp.Definition.Name)) continue;
                string name = fp.Definition.Name;

                // Lọc bỏ tham số có công thức tính toán (Formula)
                if (!string.IsNullOrWhiteSpace(fp.Formula)) continue;

                if (IsInternalSystemIgnoredParam(name)) continue;
                if (processed.Contains(name)) continue;

                // Lấy kiểu dữ liệu
                var dt = fp.Definition.GetDataType();
                bool isYesNo = (dt == SpecTypeId.Boolean.YesNo);
                bool isMaterial = (dt == SpecTypeId.Reference.Material) || 
                                  name.ToUpperInvariant().Contains("MATERIAL") || 
                                  name.ToUpperInvariant().Contains("VẬT LIỆU") || 
                                  name.ToUpperInvariant().StartsWith("MAT_");

                // 1. Phân loại Vật liệu
                if (isMaterial)
                {
                    processed.Add(name);
                    matList.Add(new ManholeMaterialItem
                    {
                        ParamName = name,
                        IsType = !fp.IsInstance,
                        TargetSide = "ALL",
                        SelectedMaterialName = string.Empty
                    });
                    continue;
                }

                // 2. Phân loại Tham số Văn bản (String)
                if (fp.StorageType == StorageType.String)
                {
                    string defStr = string.Empty;
                    try
                    {
                        if (famMgr.CurrentType != null)
                            defStr = famMgr.CurrentType.AsString(fp) ?? string.Empty;
                    }
                    catch { }

                    processed.Add(name);
                    nonGeoList.Add(new CustomNonGeoParamItem
                    {
                        ParamName = name,
                        ParamValue = defStr,
                        IsReadOnly = false
                    });
                    continue;
                }

                // 3. Phân loại Tham số Hình học / Kích thước / YesNo
                if (fp.StorageType == StorageType.Double || fp.StorageType == StorageType.Integer)
                {
                    string groupLabel = "Dimensions (Kích Thước)";
                    try
                    {
                        var groupTypeId = fp.Definition.GetGroupTypeId();
                        groupLabel = LabelUtils.GetLabelForGroup(groupTypeId);
                    }
                    catch { }

                    string gUpper = groupLabel.ToUpperInvariant();
                    bool isDim = gUpper.Contains("DIMEN") || gUpper.Contains("KÍCH THƯỚC") || gUpper.Contains("GEOM");

                    // Đối với mục Other thì chỉ lấy biến Yes/No
                    if (!isDim && !isYesNo)
                    {
                        continue;
                    }

                    string dtLabel = isYesNo ? "Yes/No" : "Chiều dài (m)";
                    string defVal = "0.000";
                    bool boolVal = false;

                    try
                    {
                        if (famMgr.CurrentType != null)
                        {
                            if (fp.StorageType == StorageType.Double)
                            {
                                double? dVal = famMgr.CurrentType.AsDouble(fp);
                                if (dVal.HasValue)
                                {
                                    if (dt == SpecTypeId.Angle)
                                    {
                                        dtLabel = "Góc (độ)";
                                        double deg = UnitUtils.ConvertFromInternalUnits(dVal.Value, UnitTypeId.Degrees);
                                        defVal = deg.ToString("0.0#");
                                    }
                                    else
                                    {
                                        double m = UnitUtils.ConvertFromInternalUnits(dVal.Value, UnitTypeId.Meters);
                                        defVal = m.ToString("F3");
                                    }
                                }
                            }
                            else if (fp.StorageType == StorageType.Integer)
                            {
                                int? iVal = famMgr.CurrentType.AsInteger(fp);
                                if (iVal.HasValue)
                                {
                                    if (isYesNo)
                                    {
                                        boolVal = (iVal.Value == 1);
                                        defVal = boolVal ? "1" : "0";
                                    }
                                    else
                                    {
                                        dtLabel = "Số nguyên";
                                        defVal = iVal.Value.ToString();
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    processed.Add(name);
                    geoList.Add(new ManholeParameterItem
                    {
                        Name = name,
                        Value = defVal,
                        DataType = dtLabel,
                        StorageType = fp.StorageType,
                        IsType = !fp.IsInstance,
                        IsReadOnly = false,
                        Group = isYesNo ? "Bật/Tắt Cấu Kiện (Yes/No)" : "Kích Thước & Hình Học",
                        IsYesNo = isYesNo,
                        BoolValue = boolVal
                    });
                }
            }
        }

        private static void ProcessParameter(
            Parameter p, 
            bool isType, 
            List<ManholeParameterItem> geoList, 
            List<ManholeMaterialItem> matList, 
            List<CustomNonGeoParamItem> nonGeoList, 
            HashSet<string> processed)
        {
            if (p == null || p.Definition == null) return;
            string name = p.Definition.Name;
            if (string.IsNullOrWhiteSpace(name)) return;
            if (processed.Contains(name)) return;
            if (IsInternalSystemIgnoredParam(name)) return;

            processed.Add(name);

            // 1. Kiểm tra tham số Vật liệu
            if (IsMaterialParameter(p))
            {
                matList.Add(new ManholeMaterialItem
                {
                    ParamName = name,
                    IsType = isType,
                    TargetSide = "ALL",
                    SelectedMaterialName = string.Empty
                });
                return;
            }

            // 2. Tham số kiểu số / kích thước / YesNo (Tab 2)
            if (p.StorageType == StorageType.Double || p.StorageType == StorageType.Integer)
            {
                string dtLabel = "Number";
                bool isYesNo = false;
                try
                {
                    var dt = p.Definition.GetDataType();
                    if (dt == SpecTypeId.Length) dtLabel = "Chiều dài (m)";
                    else if (dt == SpecTypeId.Angle) dtLabel = "Góc (độ)";
                    else if (dt == SpecTypeId.Slope) dtLabel = "Độ dốc (%)";
                    else if (dt == SpecTypeId.Boolean.YesNo) { dtLabel = "Yes/No"; isYesNo = true; }
                }
                catch { }

                string valStr = "";
                bool boolVal = false;
                if (p.StorageType == StorageType.Double)
                {
                    double m = UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Meters);
                    valStr = m.ToString("F3");
                }
                else if (p.StorageType == StorageType.Integer)
                {
                    int i = p.AsInteger();
                    valStr = i.ToString();
                    boolVal = (i == 1);
                }

                geoList.Add(new ManholeParameterItem
                {
                    Name = name,
                    Value = valStr,
                    DataType = dtLabel,
                    StorageType = p.StorageType,
                    IsType = isType,
                    IsReadOnly = p.IsReadOnly,
                    Group = isYesNo ? "Bật/Tắt Cấu Kiện (Yes/No)" : "Kích Thước & Hình Học",
                    IsYesNo = isYesNo,
                    BoolValue = boolVal
                });
                return;
            }

            // 3. Tham số văn bản / phi hình học (Tab 5)
            if (p.StorageType == StorageType.String)
            {
                nonGeoList.Add(new CustomNonGeoParamItem
                {
                    ParamName = name,
                    ParamValue = p.AsString() ?? string.Empty,
                    IsReadOnly = p.IsReadOnly
                });
            }
        }

        private static bool IsMaterialParameter(Parameter p)
        {
            try
            {
                var dt = p.Definition.GetDataType();
                if (dt == SpecTypeId.Reference.Material) return true;
            }
            catch { }

            string nUpper = p.Definition.Name.ToUpperInvariant();
            if (nUpper.Contains("MATERIAL") || nUpper.Contains("VẬT LIỆU") || nUpper.Contains("VAT LIEU") || nUpper.StartsWith("MAT_"))
            {
                return true;
            }

            return false;
        }

        private static bool IsInternalSystemIgnoredParam(string name)
        {
            string u = name.ToUpperInvariant();
            if (u == "ALL_MODEL_MARK" || u == "ELEM_FAMILY_AND_TYPE_PARAM" || u == "ELEM_TYPE_PARAM") return true;
            if (u.StartsWith("ELEM_") || u.StartsWith("INSTANCE_")) return true;
            if (u == "WORKSET" || u == "DESIGN_OPTION" || u == "PHASE CREATED" || u == "PHASE DEMOLISHED") return true;
            return false;
        }

        /// <summary>
        /// Lấy danh sách tên tất cả các Material có trong dự án Revit
        /// </summary>
        public static List<string> GetAllProjectMaterials(Document doc)
        {
            var result = new List<string>();
            if (doc == null) return result;

            try
            {
                var materials = new FilteredElementCollector(doc)
                    .OfClass(typeof(Material))
                    .Cast<Material>()
                    .Where(m => !string.IsNullOrWhiteSpace(m.Name))
                    .OrderBy(m => m.Name);

                foreach (var m in materials)
                {
                    result.Add(m.Name);
                }
            }
            catch { }

            return result;
        }

        /// <summary>
        /// Lấy danh sách tất cả các Level trong dự án Revit
        /// </summary>
        public static List<Level> GetAllLevels(Document doc)
        {
            var result = new List<Level>();
            if (doc == null) return result;

            try
            {
                result = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();
            }
            catch { }

            return result;
        }

        /// <summary>
        /// Lấy danh sách tất cả FamilySymbol có thể sử dụng (Generic Model, Site, Structural Foundations...)
        /// </summary>
        public static List<FamilySymbol> GetAvailableManholeSymbols(Document doc)
        {
            var result = new List<FamilySymbol>();
            if (doc == null) return result;

            try
            {
                result = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(s => s.Category != null && 
                               (s.Category.Id.Value == (long)BuiltInCategory.OST_GenericModel ||
                                s.Category.Id.Value == (long)BuiltInCategory.OST_Site ||
                                s.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFoundation ||
                                s.Category.Id.Value == (long)BuiltInCategory.OST_SpecialityEquipment))
                    .OrderBy(s => s.FamilyName)
                    .ThenBy(s => s.Name)
                    .ToList();
            }
            catch { }

            return result;
        }
    }
}
