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
        /// 1. Type Parameters (Tham số loại trên Symbol - VD: CH_B, CH_H, CH_T)
        /// 2. Instance Parameters (Tham số biến thể trên Instance - VD: CO VAI KE, CONG HOP_B, TAM DAN_B, DA DAM DEM_H)
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
                FamilyInstance? sampleInst = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .FirstOrDefault(fi => fi.Symbol != null && (fi.Symbol.Id == sym.Id || (sym.Family != null && fi.Symbol.Family != null && fi.Symbol.Family.Id == sym.Family.Id)));

                if (sampleInst != null)
                {
                    ExtractParametersFromInstance(sampleInst, sym, categoryName, result, processedNames);
                }
                else
                {
                    // Bước 2.2: Chưa có instance trong dự án -> Dùng Transaction tạm thời tạo instance mẫu rồi RollBack
                    bool createdTemp = false;
                    using (var t = new Transaction(doc, "Tạm quét tham số Family"))
                    {
                        t.Start();
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
                            }
                        }
                        catch
                        {
                            // Bỏ qua lỗi tạo temp instance
                        }
                        finally
                        {
                            t.RollBack(); // Luôn hủy bỏ để không lưu rác trong mô hình
                        }
                    }

                    // Bước 2.3: Fallback nếu không tạo được temp instance (VD: Face/Host-based phức tạp)
                    // Mở Family Document trong bộ nhớ để đọc FamilyManager
                    if (!createdTemp && sym.Family != null && sym.Family.IsEditable)
                    {
                        ExtractParametersFromFamilyDefinition(doc, sym, categoryName, result, processedNames);
                    }
                }
            }
            catch
            {
                // Fallback an toàn tối thiểu: Quét trực tiếp từ Symbol nếu có sự cố
                if (result.Count == 0)
                {
                    ExtractParametersFromSymbol(sym, categoryName, result, processedNames);
                }
            }

            // Sắp xếp: Ưu tiên Dimensions -> Other -> Các nhóm khác -> Tên tham số A-Z
            return result
                .OrderByDescending(p => p.IsDimension)
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

                // Kiểm tra loại trừ các tham số hệ thống vô nghĩa
                bool isUserParam = false;
                try
                {
                    isUserParam = (p.Id != null && p.Id.Value > 0);
                }
                catch { }

                var item = CreateMappingItemFromParameter(p, sym, categoryName, isInstance: false);
                if (item == null) continue;

                // Giữ lại tham số Family định nghĩa hoặc tham số thuộc Dimensions/Other
                if (isUserParam || item.IsDimension || item.IsOther)
                {
                    processed.Add(name);
                    list.Add(item);
                }
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

                // Kiểm tra xem có phải là tham số do Family định nghĩa hay built-in hữu ích
                bool isUserParam = false;
                try
                {
                    isUserParam = (p.Id != null && p.Id.Value > 0);
                }
                catch { }

                var item = CreateMappingItemFromParameter(p, sym, categoryName, isInstance: true);
                if (item == null) continue;

                if (isUserParam || item.IsDimension || item.IsOther)
                {
                    processed.Add(name);
                    list.Add(item);
                }
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

                        // Chỉ bổ sung tham số Instance nếu chưa có
                        bool isInstance = fp.IsInstance;

                        string groupName = "Chung";
                        bool isDim = false;
                        bool isOther = false;

                        try
                        {
                            var groupTypeId = fp.Definition.GetGroupTypeId();
                            if (groupTypeId != null && !string.IsNullOrEmpty(groupTypeId.TypeId))
                            {
                                string label = LabelUtils.GetLabelForGroup(groupTypeId);
                                groupName = label;

                                if (groupTypeId == GroupTypeId.Geometry ||
                                    label.IndexOf("dimen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    label.IndexOf("kích thước", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    groupTypeId.TypeId.IndexOf("dimen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    groupTypeId.TypeId.IndexOf("geom", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    groupName = "Dimensions (Kích thước)";
                                    isDim = true;
                                }
                                else if (string.Equals(label.Trim(), "Other", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(label.Trim(), "Khác", StringComparison.OrdinalIgnoreCase) ||
                                         groupTypeId.TypeId.IndexOf("other", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    groupName = "Other (Khác)";
                                    isOther = true;
                                }
                            }
                        }
                        catch
                        {
                            if (name.StartsWith("CX_") || name.StartsWith("CH_") || name.Contains("H_") || name.Contains("B_") || name.Contains("L_"))
                            {
                                groupName = "Dimensions (Kích thước)";
                                isDim = true;
                            }
                            else
                            {
                                groupName = "Other (Khác)";
                                isOther = true;
                            }
                        }

                        // Lấy giá trị mặc định từ CurrentType
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
                        try
                        {
                            var dt = fp.Definition.GetDataType();
                            if (dt == SpecTypeId.Length) dataTypeStr = "Length (Chiều dài)";
                            else if (dt == SpecTypeId.Angle) dataTypeStr = "Angle (Góc)";
                            else if (dt == SpecTypeId.Number) dataTypeStr = "Number (Số)";
                            else if (dt == SpecTypeId.Boolean.YesNo) dataTypeStr = "Yes/No (Có/Không)";
                        }
                        catch { }

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

        private static ParameterMappingItem CreateMappingItemFromParameter(
            Parameter p,
            FamilySymbol sym,
            string categoryName,
            bool isInstance)
        {
            string name = p.Definition.Name;
            string groupName = "Chung";
            bool isDim = false;
            bool isOther = false;

            try
            {
                var groupTypeId = p.Definition.GetGroupTypeId();
                if (groupTypeId != null && !string.IsNullOrEmpty(groupTypeId.TypeId))
                {
                    string label = LabelUtils.GetLabelForGroup(groupTypeId);
                    groupName = label;

                    // 1. Nhóm Kích thước (Dimensions / Geometry)
                    if (groupTypeId == GroupTypeId.Geometry ||
                        label.IndexOf("dimen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        label.IndexOf("kích thước", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        groupTypeId.TypeId.IndexOf("dimen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        groupTypeId.TypeId.IndexOf("geom", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        groupName = "Dimensions (Kích thước)";
                        isDim = true;
                        isOther = false;
                    }
                    // 2. Nhóm Khác (Other) - CHỈ DUY NHẤT MỤC "Other" TRONG REVIT PROPERTIES (VD: CO VAI KE, CONG HOP_B, CONG HOP_H)
                    else if (string.Equals(label.Trim(), "Other", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(label.Trim(), "Khác", StringComparison.OrdinalIgnoreCase) ||
                             groupTypeId.TypeId.IndexOf("other", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        groupName = "Other (Khác)";
                        isOther = true;
                        isDim = false;
                    }
                    else
                    {
                        isDim = false;
                        isOther = false;
                    }
                }
            }
            catch
            {
                if (name.StartsWith("CX_") || name.StartsWith("CH_") || name.Contains("H_") || name.Contains("B_") || name.Contains("L_") || name.Contains("Angle"))
                {
                    groupName = "Dimensions (Kích thước)";
                    isDim = true;
                    isOther = false;
                }
                else
                {
                    groupName = "Other (Khác)";
                    isOther = true;
                    isDim = false;
                }
            }

            // Giá trị mặc định hiển thị
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
                    try
                    {
                        var dt = p.Definition.GetDataType();
                        if (dt == SpecTypeId.Boolean.YesNo)
                        {
                            defValue = (p.AsInteger() == 1) ? "Có" : "Không";
                        }
                        else
                        {
                            defValue = p.AsInteger().ToString();
                        }
                    }
                    catch
                    {
                        defValue = p.AsInteger().ToString();
                    }
                }
                else if (p.StorageType == StorageType.String)
                {
                    defValue = p.AsString() ?? string.Empty;
                }
            }

            string dataTypeStr = p.StorageType.ToString();
            try
            {
                var dt = p.Definition.GetDataType();
                if (dt == SpecTypeId.Length) dataTypeStr = "Length (Chiều dài)";
                else if (dt == SpecTypeId.Angle) dataTypeStr = "Angle (Góc)";
                else if (dt == SpecTypeId.Number) dataTypeStr = "Number (Số)";
                else if (dt == SpecTypeId.Boolean.YesNo) dataTypeStr = "Yes/No (Có/Không)";
            }
            catch { }

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
            if (lower.Contains("goc") || lower.Contains("angle"))
                return "GocXoay";
            return "Tùy biến";
        }
    }
}
