using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using InfraBIM.CulvertTool.Models;

namespace InfraBIM.CulvertTool.Services
{
    public static class BimParameterService
    {
        public static void SetElementBimProperties(
            Element elem,
            BimInfoConfig bimConfig,
            string tenCauKien,
            string? moTaOverride = null,
            double? zDauM = null,
            double? zCuoiM = null,
            double? xM = null,
            double? yM = null,
            double? zDayM = null)
        {
            if (elem == null) return;

            // 1. Thông tin văn bản chung
            SetParamString(elem, "Tên công trình", bimConfig.TenCongTrinh);
            SetParamString(elem, "Tên nhóm cấu kiện", bimConfig.TenNhom);
            SetParamString(elem, "Tên cấu kiện", tenCauKien);
            SetParamString(elem, "Mô tả", moTaOverride ?? bimConfig.MoTa);

            // 2. Gán cao độ đầu / cuối nếu được cấu hình
            if (bimConfig.AutoSetElevations)
            {
                if (zDauM.HasValue) SetParamDouble(elem, "Cao độ đầu (m)", zDauM.Value);
                if (zCuoiM.HasValue) SetParamDouble(elem, "Cao độ cuối (m)", zCuoiM.Value);
                if (zDayM.HasValue) SetParamDouble(elem, "Cao độ đáy (m)", zDayM.Value);
            }

            // 3. Gán tọa độ X / Y nếu được cấu hình
            if (bimConfig.AutoSetCoordinates)
            {
                if (xM.HasValue) SetParamDouble(elem, "Tọa độ X(m)", xM.Value);
                if (yM.HasValue) SetParamDouble(elem, "Tọa độ Y(m)", yM.Value);
            }
        }

        /// <summary>
        /// Gán danh sách tham số BIM tùy biến động từ Tab 03
        /// </summary>
        public static void ApplyCustomBimParameters(
            Element elem,
            IEnumerable<CustomBimParameterItem>? customParams,
            CulvertRowData row,
            string elementScope)
        {
            if (elem == null || customParams == null) return;

            foreach (var cp in customParams)
            {
                if (!cp.IsActive || string.IsNullOrWhiteSpace(cp.ParamName)) continue;

                // Kiểm tra phạm vi áp dụng
                if (cp.TargetScope != "Tất cả cấu kiện" && !cp.TargetScope.Contains(elementScope))
                    continue;

                // Thay thế template tags
                string val = cp.ValueTemplate ?? "";
                val = val.Replace("{STT}", row.STT.ToString())
                         .Replace("{LyTrinh}", row.LyTrinh ?? "")
                         .Replace("{KhauDo}", row.KhauDo ?? "")
                         .Replace("{LoaiCong}", row.LoaiCong ?? "")
                         .Replace("{SoCua}", row.SoCua.ToString())
                         .Replace("{ChieuDai}", row.ChieuDai.ToString("F2"))
                         .Replace("{DoDoc}", row.DoDoc.ToString("F2") + "%")
                         .Replace("{GocXoay}", row.GocXoay.ToString("F2") + "°")
                         .Replace("{X1}", row.X1.ToString("F3"))
                         .Replace("{Y1}", row.Y1.ToString("F3"))
                         .Replace("{Z1}", row.Z1.ToString("F3"))
                         .Replace("{X2}", row.X2.ToString("F3"))
                         .Replace("{Y2}", row.Y2.ToString("F3"))
                         .Replace("{Z2}", row.Z2.ToString("F3"));

                SetParamString(elem, cp.ParamName, val);
            }
        }

        /// <summary>
        /// Gán các tham số hình học/Dimensions đã được tùy chỉnh/ánh xạ trong Tab 02 cho FamilyInstance
        /// </summary>
        public static void ApplyFamilyMappedParameters(
            FamilyInstance inst,
            IEnumerable<ParameterMappingItem>? mappings,
            CulvertRowData data,
            string categoryName)
        {
            if (inst == null || mappings == null) return;
            string famName = inst.Symbol?.FamilyName ?? string.Empty;

            foreach (var m in mappings)
            {
                if (!m.IsSelected) continue;

                // Kiểm tra khớp theo Category hoặc FamilyName
                bool matchCat = !string.IsNullOrEmpty(m.CategoryName) && 
                                (categoryName.IndexOf(m.CategoryName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 m.CategoryName.IndexOf(categoryName, StringComparison.OrdinalIgnoreCase) >= 0);
                bool matchFam = !string.IsNullOrEmpty(m.FamilyName) && 
                                string.Equals(m.FamilyName, famName, StringComparison.OrdinalIgnoreCase);

                if (!matchCat && !matchFam) continue;

                Parameter p = inst.LookupParameter(m.InternalName);
                if (p == null || p.IsReadOnly) continue;

                // Xác định giá trị gán: ưu tiên MappedField từ dữ liệu cống, nếu không thì lấy CustomValue
                string valStr = m.CustomValue;
                if (!string.IsNullOrEmpty(m.MappedField) && m.MappedField != "Tùy biến")
                {
                    if (m.MappedField == "KhauDo") valStr = data.KhauDo;
                    else if (m.MappedField == "ChieuDai") valStr = data.ChieuDai.ToString("F2");
                    else if (m.MappedField == "DoDoc") valStr = data.DoDoc.ToString("F2");
                    else if (m.MappedField == "GocXoay") valStr = data.GocXoay.ToString("F2");
                    else if (m.MappedField == "L_Ngam_San") valStr = data.L_Ngam_San.ToString("F2");
                    else if (m.MappedField == "KC_HN1") valStr = data.KC_HN1.ToString("F2");
                    else if (m.MappedField == "KC_HN2") valStr = data.KC_HN2.ToString("F2");
                    else if (m.MappedField == "KhoangCachTim") valStr = data.KhoangCachTim.ToString("F2");
                    else if (m.MappedField == "X1") valStr = data.X1.ToString("F3");
                    else if (m.MappedField == "Y1") valStr = data.Y1.ToString("F3");
                    else if (m.MappedField == "Z1") valStr = data.Z1.ToString("F3");
                    else if (m.MappedField == "X2") valStr = data.X2.ToString("F3");
                    else if (m.MappedField == "Y2") valStr = data.Y2.ToString("F3");
                    else if (m.MappedField == "Z2") valStr = data.Z2.ToString("F3");
                }

                if (string.IsNullOrWhiteSpace(valStr)) continue;

                try
                {
                    if (p.StorageType == StorageType.Double)
                    {
                        string cleaned = valStr.Replace("mm", "").Replace("m", "").Replace("°", "").Trim();
                        if (double.TryParse(cleaned, out double dVal))
                        {
                            double internalVal = dVal;
                            try
                            {
                                var dt = p.Definition.GetDataType();
                                if (dt == SpecTypeId.Length)
                                {
                                    internalVal = (dVal >= 10.0)
                                        ? UnitUtils.ConvertToInternalUnits(dVal, UnitTypeId.Millimeters)
                                        : UnitUtils.ConvertToInternalUnits(dVal, UnitTypeId.Meters);
                                }
                                else if (dt == SpecTypeId.Angle)
                                {
                                    internalVal = UnitUtils.ConvertToInternalUnits(dVal, UnitTypeId.Degrees);
                                }
                            }
                            catch
                            {
                                internalVal = (dVal >= 10.0)
                                    ? UnitUtils.ConvertToInternalUnits(dVal, UnitTypeId.Millimeters)
                                    : UnitUtils.ConvertToInternalUnits(dVal, UnitTypeId.Meters);
                            }

                            p.Set(internalVal);
                        }
                    }
                    else if (p.StorageType == StorageType.String)
                    {
                        p.Set(valStr);
                    }
                    else if (p.StorageType == StorageType.Integer)
                    {
                        if (int.TryParse(valStr.Trim(), out int iVal)) p.Set(iVal);
                    }
                }
                catch { }
            }
        }

        public static void SetParamString(Element elem, string pName, string val)
        {
            if (string.IsNullOrEmpty(val)) return;
            Parameter p = elem.LookupParameter(pName);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String)
            {
                p.Set(val);
            }
            else
            {
                if (pName == "Tên cấu kiện")
                {
                    Parameter pMark = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    if (pMark != null && !pMark.IsReadOnly) pMark.Set(val);
                }
                else if (pName == "Mô tả")
                {
                    Parameter pComments = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (pComments != null && !pComments.IsReadOnly) pComments.Set(val);
                }
            }
        }

        public static void SetParamDouble(Element elem, string pName, double valM)
        {
            Parameter p = elem.LookupParameter(pName);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
            {
                double internalVal = UnitUtils.ConvertToInternalUnits(valM, UnitTypeId.Meters);
                p.Set(internalVal);
            }
        }
    }
}
