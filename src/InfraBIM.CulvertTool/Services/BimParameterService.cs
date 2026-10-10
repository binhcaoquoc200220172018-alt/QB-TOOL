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

                // Kiểm tra khớp theo Category (ưu tiên cao nhất nếu m.CategoryName đã được gán cấu kiện cụ thể)
                bool matchCat = !string.IsNullOrEmpty(m.CategoryName) && 
                                (categoryName.IndexOf(m.CategoryName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 m.CategoryName.IndexOf(categoryName, StringComparison.OrdinalIgnoreCase) >= 0);
                bool matchFam = !string.IsNullOrEmpty(m.FamilyName) && 
                                string.Equals(m.FamilyName, famName, StringComparison.OrdinalIgnoreCase);

                bool isMatch = !string.IsNullOrEmpty(m.CategoryName) ? matchCat : matchFam;
                if (!isMatch) continue;

                // Không bao giờ can thiệp tham số Yes/No kiểm soát hiển thị nội bộ của cụm Cửa xả & Sân gia cố
                if (m.IsYesNoParameter || m.IsVisibility)
                {
                    if (famName.IndexOf("CX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        famName.IndexOf("SGC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        categoryName.IndexOf("Cửa xả", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        categoryName.IndexOf("Sân gia cố", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }
                }

                string pNameUpper = (m.InternalName ?? "").ToUpperInvariant();

                // YÊU CẦU 2: Bỏ hoàn toàn A_GÓC XOAY, không gán hay can thiệp
                if (pNameUpper.Contains("A_GOC") || pNameUpper.Contains("A_GÓC")) continue;

                // YÊU CẦU 1: Bỏ qua tuyệt đối các biến công thức hình học nội bộ tránh lỗi Can't make type
                if (pNameUpper.StartsWith("A1") || pNameUpper.StartsWith("A2") || pNameUpper.StartsWith("A3") ||
                    pNameUpper.StartsWith("A4") || pNameUpper.StartsWith("A5") || pNameUpper.StartsWith("A6") ||
                    pNameUpper.StartsWith("A7") || pNameUpper.Contains("'")) continue;

                // Bỏ qua CO VAI KE vì giá trị Yes/No đã được thuật toán rải tính toán chính xác theo từng vị trí đốt cống
                if (pNameUpper == "CO VAI KE" || pNameUpper == "CO_VAI_KE" || pNameUpper.Contains("VAI KE") || pNameUpper.Contains("VAI_KE")) continue;

                // Hỗ trợ cả Instance parameter (trên inst) và Type parameter (trên inst.Symbol)
                Parameter? p = inst.LookupParameter(m.InternalName);
                bool isTypeParam = false;
                if (p == null && inst.Symbol != null)
                {
                    p = inst.Symbol.LookupParameter(m.InternalName);
                    isTypeParam = true;
                }
                if (p == null || p.IsReadOnly) continue;

                // Đối với tham số dạng Type của Cửa xả & Sân gia cố:
                // CHỈ BỎ QUA các tham số kiểm soát hiển thị (Yes/No / Visibility),
                // TUYỆT ĐỐI CHO PHÉP ghi nhận các tham số kích thước hình học (Double/Length) mà người dùng đã tùy chỉnh!
                if (isTypeParam)
                {
                    bool isVisName = pNameUpper == "CX_TUONG DAU" || pNameUpper == "CX_TUONG CANH" || 
                                     pNameUpper == "CX_SAN CONG" || pNameUpper == "CX_BE TONG LOT" || 
                                     pNameUpper == "CX_DA DAM DEM" || pNameUpper == "TD_BE TONG LOT" || 
                                     pNameUpper == "TD_DA DAM DEM" || pNameUpper.Contains("_SH") || 
                                     pNameUpper.Contains("YESNO");

                    if (m.IsVisibility || m.IsYesNoParameter || isVisName)
                    {
                        continue;
                    }
                }

                bool hasExplicitMapping = !string.IsNullOrEmpty(m.MappedField) && m.MappedField != "Tùy biến";

                // Xác định giá trị gán: ưu tiên MappedField từ dữ liệu cống, sau đó lấy giá trị tùy chỉnh riêng của cống
                string valStr = data.GetParamOverride(m.InternalName, m.CustomValue);
                if (hasExplicitMapping)
                {
                    if (m.MappedField == "KhauDo")
                    {
                        valStr = data.KhauDo;
                        if (p.StorageType == StorageType.Double)
                        {
                            string kd = (data.KhauDo ?? "").Trim();
                            if (kd.StartsWith("D", StringComparison.OrdinalIgnoreCase))
                                kd = kd.Substring(1);
                            var parts = kd.Split('x', 'X', '*', '-');
                            if (parts.Length > 0 && double.TryParse(parts[0], out double bVal))
                            {
                                valStr = (bVal >= 10.0 ? bVal : bVal * 1000.0).ToString();
                            }
                        }
                    }
                    else if (m.MappedField == "ChieuDai") valStr = data.ChieuDai.ToString("F2");
                    else if (m.MappedField == "DoDoc") valStr = data.DoDoc.ToString("F2");
                    else if (m.MappedField == "GocXoay") valStr = data.GocXoay.ToString("F2");
                    else if (m.MappedField == "L_Ngam_San") valStr = data.L_Ngam_San.ToString("F2");
                    else if (m.MappedField == "B_san") valStr = data.KhauDo;
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
                            double internalVal;
                            bool isAngle = (m.DataType != null && m.DataType.IndexOf("Angle", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                           pNameUpper.Contains("GOC") || pNameUpper.Contains("GX") || pNameUpper.Contains("ANGLE");

                            if (isAngle)
                            {
                                // Góc nhập vào là độ (Degrees) -> chuyển sang Radians chuẩn Revit DB
                                internalVal = dVal * Math.PI / 180.0;
                            }
                            else
                            {
                                // Chiều dài: nếu >= 10 coi là mm, < 10 coi là mét
                                internalVal = (Math.Abs(dVal) >= 10.0)
                                    ? UnitUtils.ConvertToInternalUnits(dVal, UnitTypeId.Millimeters)
                                    : UnitUtils.ConvertToInternalUnits(dVal, UnitTypeId.Meters);
                            }

                            try { p.Set(internalVal); } catch { }
                        }
                    }
                    else if (p.StorageType == StorageType.String)
                    {
                        try { p.Set(valStr); } catch { }
                    }
                    else if (p.StorageType == StorageType.Integer)
                    {
                        string trimVal = valStr.Trim();
                        if (int.TryParse(trimVal, out int iVal))
                        {
                            p.Set(iVal);
                        }
                        else if (bool.TryParse(trimVal, out bool bVal))
                        {
                            p.Set(bVal ? 1 : 0);
                        }
                        else if (string.Equals(trimVal, "có", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(trimVal, "co", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(trimVal, "yes", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(trimVal, "bật", StringComparison.OrdinalIgnoreCase))
                        {
                            p.Set(1);
                        }
                        else if (string.Equals(trimVal, "không", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(trimVal, "khong", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(trimVal, "no", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(trimVal, "tắt", StringComparison.OrdinalIgnoreCase))
                        {
                            p.Set(0);
                        }
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
