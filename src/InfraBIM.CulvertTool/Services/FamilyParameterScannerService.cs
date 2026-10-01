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
        /// Quét toàn bộ tham số của một FamilySymbol (bao gồm cả Type Parameter và Instance Parameter dạng (default) trong group Dimensions và Other)
        /// </summary>
        public static List<ParameterMappingItem> ScanParametersForFamily(Document doc, FamilySymbol sym, string categoryName)
        {
            var result = new List<ParameterMappingItem>();
            if (sym == null) return result;

            var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // 1. Tìm instance đã có sẵn trong dự án trước
                FamilyInstance? sampleInst = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .FirstOrDefault(fi => fi.Symbol.Id == sym.Id);

                if (sampleInst != null)
                {
                    ExtractParametersFromInstance(sampleInst, sym, categoryName, result, processedNames);
                }
                else
                {
                    // 2. Chưa có instance trong dự án -> Dùng Transaction tạm thời để tạo 1 instance mẫu rồi RollBack
                    using (var t = new Transaction(doc, "Tạm quét tham số Family"))
                    {
                        t.Start();
                        try
                        {
                            if (!sym.IsActive) sym.Activate();
                            var tempInst = doc.Create.NewFamilyInstance(XYZ.Zero, sym, StructuralType.NonStructural);
                            ExtractParametersFromInstance(tempInst, sym, categoryName, result, processedNames);
                        }
                        catch
                        {
                            // Nếu NewFamilyInstance dạng NonStructural bị hạn chế (do Family yêu cầu Face/Line host), quét từ Type Parameters
                            ExtractParametersFromSymbol(sym, categoryName, result, processedNames);
                        }
                        finally
                        {
                            t.RollBack(); // Luôn hủy bỏ để không để lại rác trong mô hình
                        }
                    }
                }
            }
            catch
            {
                // Fallback an toàn: quét trực tiếp từ FamilySymbol
                ExtractParametersFromSymbol(sym, categoryName, result, processedNames);
            }

            // Sắp xếp: Ưu tiên các tham số thuộc nhóm Dimensions và Other lên đầu bảng
            return result
                .OrderByDescending(p => p.IsDimension)
                .ThenByDescending(p => p.IsOther)
                .ThenBy(p => p.InternalName)
                .ToList();
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
                processed.Add(name);

                // Bỏ qua các tham số hệ thống không liên quan (như ElementId, Schedule Level...)
                if (p.IsReadOnly && p.StorageType != StorageType.Double && p.StorageType != StorageType.Integer) continue;

                var item = CreateMappingItemFromParameter(p, sym, categoryName);
                list.Add(item);
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
                processed.Add(name);

                var item = CreateMappingItemFromParameter(p, sym, categoryName);
                list.Add(item);
            }
        }

        private static ParameterMappingItem CreateMappingItemFromParameter(Parameter p, FamilySymbol sym, string categoryName)
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
                    // 2. Nhóm Khác (Other) - CHỈ DUY NHẤT MỤC "Other" TRONG REVIT PROPERTIES (VD: ẨN PHẢI, ẨN TRÁI, kg/m)
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
            }

            // Kiểm tra tham số là Instance hay Type
            bool isInstance = (sym.LookupParameter(name) == null);

            string defValue = p.AsValueString() ?? string.Empty;
            if (string.IsNullOrEmpty(defValue))
            {
                if (p.StorageType == StorageType.Double)
                {
                    defValue = p.AsDouble().ToString("F2");
                }
                else if (p.StorageType == StorageType.Integer)
                {
                    defValue = p.AsInteger().ToString();
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
