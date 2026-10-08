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
        /// Quét toàn bộ tham số của một FamilySymbol bao gồm:
        /// 1. Giữ nguyên 100% thứ tự khai báo trong Family Types của Revit (không đảo thứ tự).
        /// 2. Lọc bỏ tuyệt đối các tham số ĐÃ CÓ GÁN FORMULA (như Angle X, A1-1, A10, A2, CX_L2, Angle TC...).
        /// 3. Phân nhóm rõ ràng: Dimensions (Kích thước) và Other (Khác).
        /// 4. Trong nhóm Other: chỉ hiển thị kiểu dữ liệu Length và Yes/No.
        /// 5. Bỏ qua hoàn toàn A_GÓC XOAY và không tự ý thêm biến lạ.
        /// </summary>
        public static List<ParameterMappingItem> ScanParametersForFamily(Document doc, FamilySymbol sym, string categoryName)
        {
            var result = new List<ParameterMappingItem>();
            if (sym == null) return result;

            var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // =========================================================================
            // 1. ƯU TIÊN 1: ĐỌC TRỰC TIẾP TỪ FAMILY DEFINITION
            // Đảm bảo thứ tự 100% như Family Types và kiểm tra triệt để fp.Formula
            // =========================================================================
            bool scannedFromFamily = false;
            Document? famDoc = null;
            bool wasAlreadyOpen = false;

            try
            {
                if (doc != null && sym.Family != null)
                {
                    // Kiểm tra xem Family Document đã mở trong Revit chưa (tránh mở trùng / đóng nhầm)
                    foreach (Document d in doc.Application.Documents)
                    {
                        if (d.IsFamilyDocument && string.Equals(d.Title, sym.Family.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            famDoc = d;
                            wasAlreadyOpen = true;
                            break;
                        }
                    }

                    if (famDoc == null && sym.Family.IsEditable)
                    {
                        famDoc = doc.EditFamily(sym.Family);
                    }

                    if (famDoc != null)
                    {
                        ExtractParametersFromFamilyDefinition(famDoc, sym, categoryName, result, processedNames);
                        scannedFromFamily = result.Count > 0;
                    }
                }
            }
            catch { }
            finally
            {
                if (famDoc != null && !wasAlreadyOpen)
                {
                    try { famDoc.Close(false); } catch { }
                }
            }

            // =========================================================================
            // 2. FALLBACK NẾU KHÔNG MỞ ĐƯỢC FAMILY DEFINITION
            // Đọc từ Symbol & Instance mẫu trong dự án (chỉ lấy !p.IsReadOnly)
            // =========================================================================
            if (!scannedFromFamily)
            {
                try
                {
                    // 2.1 Đọc Type Parameters từ Symbol
                    ExtractParametersFromSymbol(sym, categoryName, result, processedNames);

                    // 2.2 Đọc Instance Parameters từ Instance mẫu có sẵn trong dự án
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
                    else if (doc != null && doc.IsModifiable)
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
                                doc.Delete(tempInst.Id);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // Gán số thứ tự STT (1, 2, 3...) theo đúng thứ tự ban đầu của Family, KHÔNG sắp xếp đảo lộn!
            for (int i = 0; i < result.Count; i++)
            {
                result[i].STT = i + 1;
            }

            return result;
        }

        private static void ExtractParametersFromFamilyDefinition(
            Document famDoc,
            FamilySymbol sym,
            string categoryName,
            List<ParameterMappingItem> list,
            HashSet<string> processed)
        {
            var famMgr = famDoc.FamilyManager;
            if (famMgr == null) return;

            // Lấy danh sách FamilyParameter theo ĐÚNG THỨ TỰ hiển thị trong Family Types dialog
            IEnumerable<FamilyParameter> parameters;
            try
            {
                parameters = famMgr.GetParameters();
            }
            catch
            {
                parameters = famMgr.Parameters.Cast<FamilyParameter>();
            }

            string famNameUpper = (sym.FamilyName ?? "").ToUpperInvariant();

            foreach (FamilyParameter fp in parameters)
            {
                if (fp?.Definition == null || string.IsNullOrWhiteSpace(fp.Definition.Name)) continue;
                string name = fp.Definition.Name;
                if (processed.Contains(name)) continue;

                // YÊU CẦU QUAN TRỌNG: Lọc bỏ TOÀN BỘ parameter ĐÃ CÓ GÁN FORMULA!
                if (!string.IsNullOrWhiteSpace(fp.Formula)) continue;
                try
                {
                    if (fp.IsDeterminedByFormula) continue;
                }
                catch { }

                string nLower = name.ToLowerInvariant();

                // YÊU CẦU: Bỏ hoàn toàn A_GÓC XOAY
                if (nLower.Contains("a_goc") || nLower.Contains("a_góc")) continue;

                // YÊU CẦU: Nếu là Family Đá dăm đệm, tuyệt đối không có B_BTL, H_BTL
                if (famNameUpper.Contains("DA DAM") || famNameUpper.Contains("DEM CONG"))
                {
                    if (name.StartsWith("B_BTL", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("H_BTL", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // Nhận diện kiểu dữ liệu chuẩn Revit
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

                // Nhận diện nhóm tham số trong Family
                string groupName = "Chung";
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
                bool isLength = dataTypeStr.Contains("Length") || fp.StorageType == StorageType.Double;
                bool isYesNo = dataTypeStr.Contains("Yes/No") || isBoolYesNo;

                bool isDim = false;
                bool isOther = false;
                bool isVis = false;

                if (gLower.Contains("dimen") || gLower.Contains("kích thước") || gLower.Contains("geom"))
                {
                    groupName = "Dimensions (Kích thước)";
                    isDim = true;
                }
                else if (gLower.Contains("other") || gLower.Contains("khác") || gLower.Contains("general"))
                {
                    // YÊU CẦU: Trong mục Other CHỈ HIỂN THỊ KIỂU DỮ LIỆU LENGTH VÀ YES/NO THÔI!
                    if (isLength || isYesNo)
                    {
                        groupName = "Other (Khác)";
                        isOther = true;
                        if (isYesNo) isVis = true;
                    }
                    else
                    {
                        continue; // Loại bỏ các kiểu dữ liệu khác trong Other (String, Double, Integer...)
                    }
                }
                else if (isYesNo)
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
                    // Bỏ qua các nhóm hệ thống khác (IFC, Identity Data, Phasing...)
                    continue;
                }

                // Đọc giá trị mặc định từ Family Type hiện hành
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
                                if (dataTypeStr.Contains("Angle"))
                                {
                                    double deg = UnitUtils.ConvertFromInternalUnits(dVal.Value, UnitTypeId.Degrees);
                                    defValue = deg.ToString("F1");
                                }
                                else
                                {
                                    double mm = UnitUtils.ConvertFromInternalUnits(dVal.Value, UnitTypeId.Millimeters);
                                    defValue = mm.ToString("F1");
                                }
                            }
                        }
                        else if (fp.StorageType == StorageType.Integer)
                        {
                            int? iVal = famMgr.CurrentType.AsInteger(fp);
                            if (iVal.HasValue)
                            {
                                if (isBoolYesNo)
                                    defValue = (iVal.Value == 1) ? "Có" : "Không";
                                else
                                    defValue = iVal.Value.ToString();
                            }
                        }
                        else if (fp.StorageType == StorageType.String)
                        {
                            defValue = famMgr.CurrentType.AsString(fp) ?? string.Empty;
                        }
                    }
                }
                catch { }

                if (isBoolYesNo && string.IsNullOrEmpty(defValue))
                {
                    defValue = "Không";
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
                    IsVisibility = isVis || isBoolYesNo,
                    DataType = dataTypeStr,
                    DefaultValue = defValue,
                    CustomValue = defValue,
                    MappedField = DeduceDefaultMappedField(name),
                    IsInstance = fp.IsInstance
                });
            }
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

            // Nếu tham số là ReadOnly trong Revit dự án -> Đã có gán Formula hoặc là tham số hệ thống không thể sửa
            if (p.IsReadOnly) return null;

            string nLower = name.ToLowerInvariant();

            // YÊU CẦU: Bỏ hoàn toàn A_GÓC XOAY
            if (nLower.Contains("a_goc") || nLower.Contains("a_góc")) return null;

            // YÊU CẦU: Nếu là Family Đá dăm đệm, tuyệt đối không thêm B_BTL, H_BTL
            string famNameUpper = (sym.FamilyName ?? "").ToUpperInvariant();
            if (famNameUpper.Contains("DA DAM") || famNameUpper.Contains("DEM CONG"))
            {
                if (name.StartsWith("B_BTL", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("H_BTL", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

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
            bool isLength = dataTypeStr.Contains("Length") || p.StorageType == StorageType.Double;
            bool isYesNo = dataTypeStr.Contains("Yes/No") || isBoolYesNo;

            if (gLower.Contains("dimen") || gLower.Contains("kích thước") || gLower.Contains("geom"))
            {
                groupName = "Dimensions (Kích thước)";
                isDim = true;
            }
            else if (gLower.Contains("other") || gLower.Contains("khác") || gLower.Contains("general"))
            {
                // YÊU CẦU: Trong mục Other CHỈ HIỂN THỊ KIỂU DỮ LIỆU LENGTH VÀ YES/NO THÔI!
                if (isLength || isYesNo)
                {
                    groupName = "Other (Khác)";
                    isOther = true;
                    if (isYesNo) isVis = true;
                }
                else
                {
                    return null;
                }
            }
            else if (isBoolYesNo)
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

            if (isBoolYesNo && string.IsNullOrEmpty(defValue))
            {
                defValue = "Không";
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
                IsVisibility = isVis || isBoolYesNo,
                DataType = dataTypeStr,
                DefaultValue = defValue,
                CustomValue = defValue,
                MappedField = DeduceDefaultMappedField(name),
                IsInstance = isInstance
            };
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
            return "Tùy biến";
        }
    }
}
