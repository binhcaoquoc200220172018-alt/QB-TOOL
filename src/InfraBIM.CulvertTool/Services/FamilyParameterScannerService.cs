using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using InfraBIM.CulvertTool.Models;

namespace InfraBIM.CulvertTool.Services
{
    public static class FamilyParameterScannerService
    {
        /// <summary>
        /// Quét toàn bộ tham số của một FamilySymbol bao gồm CẢ:
        /// 1. Type Parameters (Tham số loại trên Symbol - VD: CH_B, CH_H, CH_T, CX_TUONG DAU...)
        /// 2. Instance Parameters (Tham số biến thể trên Instance - VD: CO VAI KE, CONG HOP_B, TAM DAN_B, DA DAM DEM_H...)
        /// Phân nhóm rõ ràng: Dimensions (Kích thước), Visibility (Ẩn hiện), Other (Khác)
        /// </summary>
        public static List<ParameterMappingItem> ScanParametersForFamily(Document doc, FamilySymbol sym, string categoryName)
        {
            var result = new List<ParameterMappingItem>();
            if (sym == null) return result;

            var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // =========================================================================
                // 1. TRÍCH XUẤT TOÀN BỘ TYPE PARAMETERS (TỪ SYMBOL TRỰC TIẾP)
                // =========================================================================
                ExtractParametersFromSymbol(sym, categoryName, result, processedNames);

                // =========================================================================
                // 2. TRÍCH XUẤT TOÀN BỘ INSTANCE PARAMETERS
                // =========================================================================
                // Bước 2.1: Tìm instance mẫu đã có sẵn trong dự án
                FamilyInstance? sampleInst = null;
                try
                {
                    sampleInst = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .FirstOrDefault(fi => fi.Symbol != null && (fi.Symbol.Id == sym.Id || (sym.Family != null && fi.Symbol.Family != null && fi.Symbol.Family.Id == sym.Family.Id)));
                }
                catch { }

                if (sampleInst != null)
                {
                    ExtractParametersFromInstance(sampleInst, sym, categoryName, result, processedNames);
                }
                else
                {
                    // Bước 2.2: Nếu dự án đang trong Transaction, có thể tạo temp instance để đọc
                    bool createdTemp = false;
                    if (doc.IsModifiable)
                    {
                        try
                        {
                            if (!sym.IsActive) sym.Activate();

                            FamilyInstance? tempInst = null;
                            if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(sym))
                            {
                                tempInst = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, sym);
                            }
                            else
                            {
                                tempInst = doc.Create.NewFamilyInstance(XYZ.Zero, sym, StructuralType.NonStructural);
                            }

                            if (tempInst != null)
                            {
                                ExtractParametersFromInstance(tempInst, sym, categoryName, result, processedNames);
                                createdTemp = true;
                                doc.Delete(tempInst.Id);
                            }
                        }
                        catch { }
                    }

                    // Bước 2.3: Đọc từ Family Document nếu có thể
                    if (!createdTemp && sym.Family != null && sym.Family.IsEditable)
                    {
                        try
                        {
                            ExtractParametersFromFamilyDefinition(doc, sym, categoryName, result, processedNames);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            // Bước 3: Đảm bảo các tham số cốt lõi của Family dự án luôn hiện diện
            EnsureStandardParametersExist(sym, categoryName, result, processedNames);

            // Sắp xếp: Ưu tiên Dimensions -> Visibility -> Other -> Tên tham số A-Z
            return result
                .OrderByDescending(p => p.IsDimension)
                .ThenByDescending(p => p.IsVisibility)
                .ThenByDescending(p => p.IsOther)
                .ThenBy(p => p.InternalName)
                .ToList();
        }

        private static void ExtractParametersFromSymbol(
            FamilySymbol sym,
            string categoryName,
            List<ParameterMappingItem> list,
            HashSet<string> processed)
        {
            foreach (Parameter p in sym.Parameters)
            {
                if (p.Definition == null || string.IsNullOrWhiteSpace(p.Definition.Name)) continue;
                string name = p.Definition.Name;
                if (processed.Contains(name)) continue;

                var item = CreateMappingItemFromParameter(p, sym, categoryName, isInstance: false);
                if (item == null) continue;

                processed.Add(name);
                list.Add(item);
            }
        }

        private static void ExtractParametersFromInstance(
            FamilyInstance inst,
            FamilySymbol sym,
            string categoryName,
            List<ParameterMappingItem> list,
            HashSet<string> processed)
        {
            foreach (Parameter p in inst.Parameters)
            {
                if (p.Definition == null || string.IsNullOrWhiteSpace(p.Definition.Name)) continue;
                string name = p.Definition.Name;
                if (processed.Contains(name)) continue;

                var item = CreateMappingItemFromParameter(p, sym, categoryName, isInstance: true);
                if (item == null) continue;

                processed.Add(name);
                list.Add(item);
            }
        }

        private static void ExtractParametersFromFamilyDefinition(
            Document doc,
            FamilySymbol sym,
            string categoryName,
            List<ParameterMappingItem> list,
            HashSet<string> processed)
        {
            Document? famDoc = null;
            try
            {
                famDoc = doc.EditFamily(sym.Family);
                if (famDoc != null)
                {
                    var famMgr = famDoc.FamilyManager;
                    foreach (FamilyParameter fp in famMgr.Parameters)
                    {
                        if (fp.Definition == null || string.IsNullOrWhiteSpace(fp.Definition.Name)) continue;
                        string name = fp.Definition.Name;
                        if (processed.Contains(name)) continue;

                        bool isInstance = fp.IsInstance;
                        string groupName = "Chung";
                        bool isDim = false;
                        bool isOther = false;
                        bool isVis = false;

                        string defValue = string.Empty;
                        try
                        {
                            if (famMgr.CurrentType != null)
                            {
                                if (fp.StorageType == StorageType.Double)
                                {
                                    double? dVal = famMgr.CurrentType.AsDouble(fp);
                                    if (dVal.HasValue)
                                    {
                                        double mm = UnitUtils.ConvertFromInternalUnits(dVal.Value, UnitTypeId.Millimeters);
                                        defValue = mm.ToString("F1");
                                    }
                                }
                                else if (fp.StorageType == StorageType.Integer)
                                {
                                    int? iVal = famMgr.CurrentType.AsInteger(fp);
                                    if (iVal.HasValue)
                                    {
                                        try
                                        {
                                            var dt = fp.Definition.GetDataType();
                                            if (dt == SpecTypeId.Boolean.YesNo)
                                                defValue = (iVal.Value == 1) ? "Có" : "Không";
                                            else
                                                defValue = iVal.Value.ToString();
                                        }
                                        catch
                                        {
                                            defValue = iVal.Value.ToString();
                                        }
                                    }
                                }
                                else if (fp.StorageType == StorageType.String)
                                {
                                    defValue = famMgr.CurrentType.AsString(fp) ?? string.Empty;
                                }
                            }
                        }
                        catch { }

                        string dataTypeStr = fp.StorageType.ToString();
                        bool isBoolYesNo = false;
                        try
                        {
                            var dt = fp.Definition.GetDataType();
                            if (dt == SpecTypeId.Length) dataTypeStr = "Length (Chiều dài)";
                            else if (dt == SpecTypeId.Angle) dataTypeStr = "Angle (Góc)";
                            else if (dt == SpecTypeId.Number) dataTypeStr = "Number (Số)";
                            else if (dt == SpecTypeId.Boolean.YesNo) { dataTypeStr = "Yes/No (Có/Không)"; isBoolYesNo = true; }
                        }
                        catch { }

                        try
                        {
                            var groupTypeId = fp.Definition.GetGroupTypeId();
                            if (groupTypeId != null && !string.IsNullOrEmpty(groupTypeId.TypeId))
                            {
                                groupName = LabelUtils.GetLabelForGroup(groupTypeId);
                            }
                        }
                        catch { }

                        string gLower = groupName.ToLowerInvariant();
                        string nLower = name.ToLowerInvariant();

                        // YÊU CẦU 2: Bỏ hoàn toàn A_GÓC XOAY
                        if (nLower.Contains("a_goc") || nLower.Contains("a_góc")) continue;

                        // YÊU CẦU 2 (HÌNH 3): Nếu là Family Đá dăm đệm, tuyệt đối không thêm B_BTL, H_BTL
                        string famNameUpper = (sym.FamilyName ?? "").ToUpperInvariant();
                        if (famNameUpper.Contains("DA DAM") || famNameUpper.Contains("DEM CONG"))
                        {
                            if (name.StartsWith("B_BTL", StringComparison.OrdinalIgnoreCase) ||
                                name.StartsWith("H_BTL", StringComparison.OrdinalIgnoreCase))
                                continue;
                        }

                        // YÊU CẦU 1: Bỏ qua tuyệt đối các biến công thức hình học nội bộ
                        if (nLower.StartsWith("a1") || nLower.StartsWith("a2") || nLower.StartsWith("a3") ||
                            nLower.StartsWith("a4") || nLower.StartsWith("a5") || nLower.StartsWith("a6") ||
                            nLower.StartsWith("a7") || nLower.StartsWith("angle'") || nLower.Contains("'"))
                            continue;

                        bool isLength = dataTypeStr.Contains("Length") || fp.StorageType == StorageType.Double;
                        bool isYesNo = dataTypeStr.Contains("Yes/No") || isBoolYesNo;

                        if (gLower.Contains("dimen") || gLower.Contains("kích thước") || gLower.Contains("geom"))
                        {
                            groupName = "Dimensions (Kích thước)";
                            isDim = true;
                        }
                        else if (gLower.Contains("other") || gLower.Contains("khác") || gLower.Contains("general"))
                        {
                            // YÊU CẦU 3 (HÌNH 4): Trong mục Other CHỈ HIỂN THỊ KIỂU DỮ LIỆU LENGTH VÀ YES/NO THÔI!
                            if (isLength || isYesNo)
                            {
                                groupName = "Other (Khác)";
                                isOther = true;
                                if (isYesNo) isVis = true;
                            }
                            else
                            {
                                continue; // Loại bỏ hoàn toàn String, Double, ElementId, Integer...
                            }
                        }
                        else if (isYesNo && (nLower.Contains("_sh") || nLower.Contains("sh_") || nLower.Contains("co vai ke") || nLower.Contains("co_vai_ke") || nLower.Contains("an hien") || nLower.Contains("ẩn hiện")))
                        {
                            groupName = "Other (Khác)";
                            isOther = true;
                            isVis = true;
                        }
                        else if (name.StartsWith("CH_") && (nLower.Contains("_h") || nLower.Contains("_b") || nLower.Contains("_w") || nLower.Contains("_l") || nLower.Contains("_t")))
                        {
                            groupName = "Dimensions (Kích thước)";
                            isDim = true;
                        }
                        else
                        {
                            // TUYỆT ĐỐI KHÔNG ép các trường hợp còn lại vào Other!
                            continue;
                        }

                        processed.Add(name);
                        list.Add(new ParameterMappingItem
                        {
                            IsSelected = true,
                            CategoryName = categoryName,
                            FamilyName = sym.FamilyName,
                            InternalName = name,
                            DisplayName = name,
                            GroupName = groupName,
                            IsDimension = isDim,
                            IsOther = isOther,
                            IsVisibility = isVis,
                            DataType = dataTypeStr,
                            DefaultValue = defValue,
                            CustomValue = defValue,
                            MappedField = DeduceDefaultMappedField(name),
                            IsInstance = isInstance
                        });
                    }
                }
            }
            catch { }
            finally
            {
                if (famDoc != null)
                {
                    try { famDoc.Close(false); } catch { }
                }
            }
        }

        private static bool IsSystemJunkParameter(Parameter p)
        {
            if (p == null || p.Definition == null) return true;
            string name = p.Definition.Name;

            if (p.Definition is InternalDefinition idDef && idDef.BuiltInParameter != BuiltInParameter.INVALID)
            {
                var bip = idDef.BuiltInParameter;
                switch (bip)
                {
                    case BuiltInParameter.ALL_MODEL_IMAGE:
                    case BuiltInParameter.IFC_EXPORT_ELEMENT_AS:
                    case BuiltInParameter.IFC_EXPORT_ELEMENT:
                    case BuiltInParameter.IFC_GUID:
                    case BuiltInParameter.DESIGN_OPTION_PARAM:
                    case BuiltInParameter.PHASE_DEMOLISHED:
                    case BuiltInParameter.PHASE_CREATED:
                    case BuiltInParameter.HOST_ID_PARAM:
                    case BuiltInParameter.FAMILY_LEVEL_PARAM:
                    case BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM:
                    case BuiltInParameter.ELEM_FAMILY_PARAM:
                    case BuiltInParameter.ELEM_TYPE_PARAM:
                    case BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM:
                    case BuiltInParameter.SYMBOL_NAME_PARAM:
                    case BuiltInParameter.SYMBOL_ID_PARAM:
                    case BuiltInParameter.UNIFORMAT_CODE:
                    case BuiltInParameter.UNIFORMAT_DESCRIPTION:
                    case BuiltInParameter.ALL_MODEL_MANUFACTURER:
                    case BuiltInParameter.ALL_MODEL_MODEL:
                    case BuiltInParameter.ALL_MODEL_URL:
                    case BuiltInParameter.ALL_MODEL_DESCRIPTION:
                    case BuiltInParameter.EDITED_BY:
                        return true;
                }
            }

            if (name.StartsWith("IFC", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Flip", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Image", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Work Plane", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Design Option", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Phase Created", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Phase Demolished", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Host Id", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Level", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Family Name", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Type Name", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Family and Type", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Type Id", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static ParameterMappingItem? CreateMappingItemFromParameter(
            Parameter p,
            FamilySymbol sym,
            string categoryName,
            bool isInstance)
        {
            if (p?.Definition == null || string.IsNullOrWhiteSpace(p.Definition.Name)) return null;
            string name = p.Definition.Name;
            if (IsSystemJunkParameter(p)) return null;

            string groupName = "Chung";
            bool isDim = false;
            bool isOther = false;
            bool isVis = false;

            string dataTypeStr = p.StorageType.ToString();
            bool isBoolYesNo = false;
            try
            {
                var dt = p.Definition.GetDataType();
                if (dt == SpecTypeId.Length) dataTypeStr = "Length (Chiều dài)";
                else if (dt == SpecTypeId.Angle) dataTypeStr = "Angle (Góc)";
                else if (dt == SpecTypeId.Number) dataTypeStr = "Number (Số)";
                else if (dt == SpecTypeId.Boolean.YesNo) { dataTypeStr = "Yes/No (Có/Không)"; isBoolYesNo = true; }
            }
            catch { }

            try
            {
                var groupTypeId = p.Definition.GetGroupTypeId();
                if (groupTypeId != null && !string.IsNullOrEmpty(groupTypeId.TypeId))
                {
                    groupName = LabelUtils.GetLabelForGroup(groupTypeId);
                }
            }
            catch { }

            string gLower = groupName.ToLowerInvariant();
            string nLower = name.ToLowerInvariant();

            // YÊU CẦU 2: Bỏ hoàn toàn A_GÓC XOAY
            if (nLower.Contains("a_goc") || nLower.Contains("a_góc"))
            {
                return null;
            }

            // YÊU CẦU 1 & 4: Lọc bỏ các biến công thức tính toán nội bộ tránh phá hỏng Family và gây rối Tab 02
            if (nLower.StartsWith("a1") || nLower.StartsWith("a2") || nLower.StartsWith("a3") ||
                nLower.StartsWith("a4") || nLower.StartsWith("a5") || nLower.StartsWith("a6") ||
                nLower.StartsWith("a7") || nLower.StartsWith("angle'") || nLower.Contains("'"))
            {
                return null;
            }

            // YÊU CẦU 2 (HÌNH 3): Nếu là Family Đá dăm đệm, tuyệt đối không thêm B_BTL, H_BTL
            string famNameUpper = (sym.FamilyName ?? "").ToUpperInvariant();
            if (famNameUpper.Contains("DA DAM") || famNameUpper.Contains("DEM CONG"))
            {
                if (name.StartsWith("B_BTL", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("H_BTL", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            bool isLength = dataTypeStr.Contains("Length") || p.StorageType == StorageType.Double;
            bool isYesNo = dataTypeStr.Contains("Yes/No") || isBoolYesNo;

            if (gLower.Contains("dimen") || gLower.Contains("kích thước") || gLower.Contains("geom"))
            {
                groupName = "Dimensions (Kích thước)";
                isDim = true;
            }
            else if (gLower.Contains("other") || gLower.Contains("khác") || gLower.Contains("general"))
            {
                // YÊU CẦU 3 (HÌNH 4): Trong mục Other CHỈ HIỂN THỊ KIỂU DỮ LIỆU LENGTH VÀ YES/NO THÔI!
                if (isLength || isYesNo)
                {
                    groupName = "Other (Khác)";
                    isOther = true;
                    if (isYesNo) isVis = true;
                }
                else
                {
                    return null; // Bỏ qua tất cả String, Double, ElementId, Integer...
                }
            }
            else if (isBoolYesNo && (nLower.Contains("_sh") || nLower.Contains("sh_") || nLower.Contains("co vai ke") || nLower.Contains("co_vai_ke") || nLower.Contains("an hien") || nLower.Contains("ẩn hiện")))
            {
                groupName = "Other (Khác)";
                isOther = true;
                isVis = true;
            }
            else if (name.StartsWith("CH_") && (nLower.Contains("_h") || nLower.Contains("_b") || nLower.Contains("_w") || nLower.Contains("_l") || nLower.Contains("_t")))
            {
                groupName = "Dimensions (Kích thước)";
                isDim = true;
            }
            else
            {
                // YÊU CẦU 4: TUYỆT ĐỐI KHÔNG ép các nhóm còn lại (Identity Data, IFC, Phasing...) vào Other!
                return null;
            }

            string defValue = p.AsValueString() ?? string.Empty;
            if (string.IsNullOrEmpty(defValue))
            {
                if (p.StorageType == StorageType.Double)
                {
                    try
                    {
                        var dt = p.Definition.GetDataType();
                        if (dt == SpecTypeId.Length)
                        {
                            double mm = UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Millimeters);
                            defValue = mm.ToString("F1");
                        }
                        else if (dt == SpecTypeId.Angle)
                        {
                            double deg = UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Degrees);
                            defValue = deg.ToString("F1");
                        }
                        else
                        {
                            defValue = p.AsDouble().ToString("F2");
                        }
                    }
                    catch
                    {
                        defValue = p.AsDouble().ToString("F2");
                    }
                }
                else if (p.StorageType == StorageType.Integer)
                {
                    if (isBoolYesNo)
                    {
                        defValue = (p.AsInteger() == 1) ? "Có" : "Không";
                    }
                    else
                    {
                        defValue = p.AsInteger().ToString();
                    }
                }
                else if (p.StorageType == StorageType.String)
                {
                    defValue = p.AsString() ?? string.Empty;
                }
            }

            return new ParameterMappingItem
            {
                IsSelected = true,
                CategoryName = categoryName,
                FamilyName = sym.FamilyName,
                InternalName = name,
                DisplayName = name,
                GroupName = groupName,
                IsDimension = isDim,
                IsOther = isOther,
                IsVisibility = isVis,
                DataType = dataTypeStr,
                DefaultValue = defValue,
                CustomValue = defValue,
                MappedField = DeduceDefaultMappedField(name),
                IsInstance = isInstance
            };
        }

        private static void EnsureStandardParametersExist(
            FamilySymbol sym,
            string categoryName,
            List<ParameterMappingItem> list,
            HashSet<string> processed)
        {
            string famUpper = (sym.FamilyName ?? "").ToUpperInvariant();

            if (famUpper.Contains("HOP NOI"))
            {
                AddFallbackParam(list, processed, sym, categoryName, "HOP NOI CONG_B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2100.0");
                AddFallbackParam(list, processed, sym, categoryName, "HOP NOI CONG_W", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2700.0");
                AddFallbackParam(list, processed, sym, categoryName, "HOP NOI CONG_T", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "250.0");
                AddFallbackParam(list, processed, sym, categoryName, "CONG HOP_B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1860.0");
                AddFallbackParam(list, processed, sym, categoryName, "CONG HOP_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1860.0");
                AddFallbackParam(list, processed, sym, categoryName, "HOP NOI CONG_H THAN", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2100.0");
                AddFallbackParam(list, processed, sym, categoryName, "HOP NOI CONG_H DAY", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "250.0");
                AddFallbackParam(list, processed, sym, categoryName, "HOP NOI CONG_H NAP", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "150.0");
                AddFallbackParam(list, processed, sym, categoryName, "TAM DAN_B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "540.0");
                AddFallbackParam(list, processed, sym, categoryName, "DA DAM DEM_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "150.0");
            }
            else if (famUpper.Contains("CX_SAN CONG") || famUpper.Contains("CUA XA"))
            {
                // Group Dimensions (Kích thước hình học thực tế của Cửa xả)
                AddFallbackParam(list, processed, sym, categoryName, "CH_GX", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Angle (Góc)", defVal: "90.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_GX1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Angle (Góc)", defVal: "20.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_GX2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Angle (Góc)", defVal: "20.0");
                AddFallbackParam(list, processed, sym, categoryName, "Angle X", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Number (Số)", defVal: "2.5");
                AddFallbackParam(list, processed, sym, categoryName, "CX_B Cong", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1000.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_L san cong", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1350.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_SC_B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "400.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_SC_BTL_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "100.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_SC_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "200.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_SC_H2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1000.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_B day 11", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "631.9");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_B day 12", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "631.9");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_B day 2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "432.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_B dinh", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "282.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_H 1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1090.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TC_H 2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "250.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TD_H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1090.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TD_T", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1400.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TD_T day", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "200.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TD_T dinh", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "400.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_TD_W day", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "830.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_DO_H1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "1000.0");

                // Group Other (Chỉ gồm các biến thực sự thuộc Other / Ẩn hiện cấu kiện Cửa xả)
                AddFallbackParam(list, processed, sym, categoryName, "CX_GLC", "Other (Khác)", isDim: false, isInst: true, dataType: "Yes/No (Có/Không)", defVal: "Không", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_TUONG DAU", "Other (Khác)", isDim: false, isInst: false, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_TUONG CANH", "Other (Khác)", isDim: false, isInst: false, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_SAN CONG", "Other (Khác)", isDim: false, isInst: false, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_BE TONG LOT", "Other (Khác)", isDim: false, isInst: false, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_DA DAM DEM", "Other (Khác)", isDim: false, isInst: false, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
            }
            else if (famUpper.Contains("SAN GIA CO") || famUpper.Contains("SGC"))
            {
                AddFallbackParam(list, processed, sym, categoryName, "CH_SGC_L1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "8465.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_SGC_B", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "7220.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_SGC_T", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "300.0");
                AddFallbackParam(list, processed, sym, categoryName, "CX_SGC_SH", "Other (Khác)", isDim: false, isInst: true, dataType: "Yes/No (Có/Không)", defVal: "Có", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_SGC_BTL_SH", "Other (Khác)", isDim: false, isInst: true, dataType: "Yes/No (Có/Không)", defVal: "Không", isVis: true);
                AddFallbackParam(list, processed, sym, categoryName, "CX_SGC_DD_SH", "Other (Khác)", isDim: false, isInst: true, dataType: "Yes/No (Có/Không)", defVal: "Không", isVis: true);
            }
            else if (famUpper.Contains("DA DAM") || famUpper.Contains("DEM CONG"))
            {
                // YÊU CẦU 2 (HÌNH 3): Family Đá dăm đệm thân cống (TNN_CH_DA DAM DEM)
                // TUYỆT ĐỐI KHÔNG CÓ B_BTL và H_BTL! CHỈ CÓ B_DDD, H_DDD, H, L1, L2!
                AddFallbackParam(list, processed, sym, categoryName, "B_DDD", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2260.0");
                AddFallbackParam(list, processed, sym, categoryName, "H_DDD", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "150.0");
                AddFallbackParam(list, processed, sym, categoryName, "H", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
                AddFallbackParam(list, processed, sym, categoryName, "L1", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
                AddFallbackParam(list, processed, sym, categoryName, "L2", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "0.0");
            }
            else if (famUpper.Contains("BE TONG LOT") || famUpper.Contains("BTL") || famUpper.Contains("BT LOT"))
            {
                // Family Bê tông lót thân cống (TNN_CH_BT LOT) - CHỈ CÓ B_BTL, H_BTL
                AddFallbackParam(list, processed, sym, categoryName, "B_BTL", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "2100.0");
                AddFallbackParam(list, processed, sym, categoryName, "H_BTL", "Dimensions (Kích thước)", isDim: true, isInst: true, dataType: "Length (Chiều dài)", defVal: "100.0");
            }
            else if (famUpper.Contains("THAN CONG"))
            {
                // Thân cống (Hình 1 & 2 của người dùng)
                AddFallbackParam(list, processed, sym, categoryName, "CH_B", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "1500.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_H", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "1500.0");
                AddFallbackParam(list, processed, sym, categoryName, "CH_T", "Dimensions (Kích thước)", isDim: true, isInst: false, dataType: "Length (Chiều dài)", defVal: "180.0");
                AddFallbackParam(list, processed, sym, categoryName, "CO VAI KE", "Other (Khác)", isDim: false, isInst: true, dataType: "Yes/No (Có/Không)", defVal: "Không", isVis: true);
            }
        }

        private static void AddFallbackParam(
            List<ParameterMappingItem> list,
            HashSet<string> processed,
            FamilySymbol sym,
            string categoryName,
            string paramName,
            string groupName,
            bool isDim,
            bool isInst,
            string dataType,
            string defVal,
            bool isVis = false)
        {
            if (processed.Contains(paramName)) return;
            processed.Add(paramName);

            list.Add(new ParameterMappingItem
            {
                IsSelected = true,
                CategoryName = categoryName,
                FamilyName = sym.FamilyName,
                InternalName = paramName,
                DisplayName = paramName,
                GroupName = groupName,
                IsDimension = isDim,
                IsOther = !isDim,
                IsVisibility = isVis,
                DataType = dataType,
                DefaultValue = defVal,
                CustomValue = defVal,
                MappedField = DeduceDefaultMappedField(paramName),
                IsInstance = isInst
            });
        }

        private static string DeduceDefaultMappedField(string paramName)
        {
            string lower = paramName.ToLowerInvariant();
            if (lower.Contains("khau_do") || lower.Contains("khaudo") || lower.Contains("b_cong") || lower.Contains("d_cong"))
                return "KhauDo";
            if (lower.Contains("chieu_dai") || lower.Contains("chieudai") || lower == "l" || lower.Contains("l_std"))
                return "ChieuDai";
            if (lower.Contains("ngam") || lower.Contains("l_ngam"))
                return "L_Ngam_San";
            if (lower.Contains("b_san") || lower.Contains("san_cong"))
                return "B_san";
            if (lower.Contains("do_doc") || lower.Contains("dop"))
                return "DoDoc";
            // LƯU Ý QUAN TRỌNG: KHÔNG tự động map bất kỳ biến góc nào (A_GOC XOAY, CH_GX...) sang GocXoay từ Excel
            // Chỗ này là biến người dùng có thể tự chỉnh bên tab parameter!
            return "Tùy biến";
        }
    }
}
