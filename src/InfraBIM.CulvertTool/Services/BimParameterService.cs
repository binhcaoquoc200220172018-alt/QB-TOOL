using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Diễn giải mẫu chuỗi Smart Tag cho Xem trước (Preview) hoặc gán thực tế
        /// </summary>
        public static string ResolveValueTemplate(
            string? template,
            CulvertRowData? row,
            int? dotIndex = 1,
            string? viTri = "TL",
            string? materialName = "Bê tông C25")
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            string val = template;

            if (row != null)
            {
                val = val.Replace("{STT}", row.STT.ToString())
                         .Replace("{LyTrinh}", row.LyTrinh ?? "")
                         .Replace("{KhauDo}", row.KhauDo ?? "")
                         .Replace("{LoaiCong}", row.LoaiCong ?? "")
                         .Replace("{SoCua}", row.SoCua.ToString())
                         .Replace("{ChieuDai}", row.ChieuDai.ToString("F2"))
                         .Replace("{DoDoc}", row.DoDoc.ToString("F2") + "%")
                         .Replace("{GocXoay}", row.GocXoay.ToString("F2") + "°")
                         .Replace("{CaoDoDau}", row.Z1.ToString("F3"))
                         .Replace("{CaoDoCuoi}", row.Z2.ToString("F3"))
                         .Replace("{Z1}", row.Z1.ToString("F3"))
                         .Replace("{Z2}", row.Z2.ToString("F3"))
                         .Replace("{X1}", row.X1.ToString("F3"))
                         .Replace("{Y1}", row.Y1.ToString("F3"))
                         .Replace("{X2}", row.X2.ToString("F3"))
                         .Replace("{Y2}", row.Y2.ToString("F3"));
            }

            // Xử lý {STT_DOT}
            if (dotIndex.HasValue && dotIndex.Value > 0)
            {
                val = val.Replace("{STT_DOT:00}", dotIndex.Value.ToString("D2"))
                         .Replace("{STT_DOT_2D}", dotIndex.Value.ToString("D2"))
                         .Replace("{STT_DOT}", dotIndex.Value.ToString());
            }
            else
            {
                val = val.Replace("{STT_DOT:00}", "")
                         .Replace("{STT_DOT_2D}", "")
                         .Replace("{STT_DOT}", "");
            }

            // Xử lý {VI_TRI}
            if (!string.IsNullOrEmpty(viTri))
            {
                val = val.Replace("{VI_TRI}", viTri)
                         .Replace("{Huong}", viTri);
            }
            else
            {
                val = val.Replace("{VI_TRI}", "")
                         .Replace("{Huong}", "");
            }

            // Xử lý {VatLieu}
            if (!string.IsNullOrEmpty(materialName))
            {
                val = val.Replace("{VatLieu}", materialName)
                         .Replace("{Material}", materialName);
            }
            else
            {
                val = val.Replace("{VatLieu}", "")
                         .Replace("{Material}", "");
            }

            // Làm sạch ký tự thừa nếu thẻ rỗng
            val = val.Replace("..", ".");
            if (val.EndsWith(".")) val = val.Substring(0, val.Length - 1);
            return val.Trim();
        }

        /// <summary>
        /// Gán danh sách tham số BIM tùy biến động từ Tab 04 (backward-compatible)
        /// </summary>
        public static void ApplyCustomBimParameters(
            Element elem,
            IEnumerable<CustomBimParameterItem>? customParams,
            CulvertRowData row,
            string elementScope)
        {
            ApplyCustomBimParameters(elem, customParams, row, elementScope, null, null, null, null, true);
        }

        /// <summary>
        /// Gán danh sách tham số BIM tùy biến động từ Tab 04 với hỗ trợ STT_DOT, vị trí TL/HL, vật liệu và tự tạo Shared Parameter
        /// </summary>
        public static void ApplyCustomBimParameters(
            Element elem,
            IEnumerable<CustomBimParameterItem>? customParams,
            CulvertRowData row,
            string elementScope,
            int? dotIndex,
            string? viTri,
            string? materialName,
            Document? doc,
            bool autoCreateSharedParams = true)
        {
            if (elem == null || customParams == null) return;

            // Sắp xếp: scope chung ("Tất cả...") áp dụng trước, scope cấu kiện chi tiết áp dụng sau để ghi đè chuẩn xác
            var sortedParams = customParams
                .Where(cp => cp.IsActive && !string.IsNullOrWhiteSpace(cp.ParamName))
                .OrderBy(cp => (string.IsNullOrWhiteSpace(cp.TargetScope) || cp.TargetScope.Equals("Tất cả cấu kiện", StringComparison.OrdinalIgnoreCase)) ? 0 
                             : cp.TargetScope.StartsWith("Tất cả", StringComparison.OrdinalIgnoreCase) ? 1 : 2);

            foreach (var cp in sortedParams)
            {
                // Kiểm tra phạm vi áp dụng (hỗ trợ "Tất cả...", hoặc khớp tên cụm/cấu kiện)
                bool isAllScope = string.IsNullOrWhiteSpace(cp.TargetScope) ||
                                  cp.TargetScope.Equals("Tất cả cấu kiện", StringComparison.OrdinalIgnoreCase);

                bool matchesCluster = false;
                if (!isAllScope && cp.TargetScope.StartsWith("Tất cả cấu kiện ", StringComparison.OrdinalIgnoreCase))
                {
                    string cluster = cp.TargetScope.Substring("Tất cả cấu kiện ".Length).Trim();
                    if (cluster.IndexOf("cửa xả", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        (elementScope.IndexOf("cửa xả", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         elementScope.IndexOf("Tường", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         elementScope.IndexOf("Sân cống", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         elementScope.IndexOf("Bê tông lót", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         elementScope.IndexOf("Đá dăm", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        matchesCluster = true;
                    }
                    else if (cluster.IndexOf("sân gia cố", StringComparison.OrdinalIgnoreCase) >= 0 &&
                             (elementScope.IndexOf("sân gia cố", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              elementScope.IndexOf("Bê tông lót", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        matchesCluster = true;
                    }
                    else if (cluster.IndexOf("hố ga", StringComparison.OrdinalIgnoreCase) >= 0 &&
                             (elementScope.IndexOf("hố ga", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              elementScope.IndexOf("Cổ giếng", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              elementScope.IndexOf("Khuôn hầm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              elementScope.IndexOf("Nắp đan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              elementScope.IndexOf("Bê tông lót", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        matchesCluster = true;
                    }
                    else if (cluster.IndexOf("hố kỹ thuật", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             cluster.IndexOf("hộp nối", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (elementScope.IndexOf("Hố kỹ thuật", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            elementScope.IndexOf("Hộp nối", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            elementScope.IndexOf("Bê tông lót", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            elementScope.IndexOf("Đá dăm", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchesCluster = true;
                        }
                    }
                    else if (cluster.IndexOf("cống kỹ thuật", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (elementScope.IndexOf("Thân cống", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            elementScope.IndexOf("kỹ thuật", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            elementScope.IndexOf("Bê tông lót", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            elementScope.IndexOf("Đá dăm", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchesCluster = true;
                        }
                    }
                    else if (cluster.IndexOf("đúc sẵn", StringComparison.OrdinalIgnoreCase) >= 0 &&
                             (elementScope.IndexOf("đúc sẵn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              elementScope.IndexOf("Thân cống", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              elementScope.IndexOf("Bê tông lót", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              elementScope.IndexOf("Đá dăm", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        matchesCluster = true;
                    }
                    else if (cluster.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) >= 0 &&
                             (elementScope.IndexOf("liên", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              elementScope.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              elementScope.IndexOf("Thân cống", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              elementScope.IndexOf("Bê tông lót", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              elementScope.IndexOf("Đá dăm", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        matchesCluster = true;
                    }
                }

                if (!isAllScope && !matchesCluster &&
                    !cp.TargetScope.Contains(elementScope, StringComparison.OrdinalIgnoreCase) &&
                    !elementScope.Contains(cp.TargetScope, StringComparison.OrdinalIgnoreCase))
                    continue;

                string val = ResolveValueTemplate(cp.ValueTemplate, row, dotIndex, viTri, materialName);
                if (string.IsNullOrEmpty(val)) continue;

                SetParamStringWithGroup(elem, cp.ParamName, cp.RevitGroup, val, doc, autoCreateSharedParams);
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
                // 0. Phân luồng Profile / Loại cống tuyệt đối tránh đá biến
                string mFullDesc = $"{m.ProfileName} {m.CategoryName} {m.GroupName}".ToUpperInvariant();
                bool mIsCastInPlace = mFullDesc.Contains("ĐỔ TẠI CHỖ") || mFullDesc.Contains("DO TAI CHO") || mFullDesc.Contains("CAST_IN_PLACE") || mFullDesc.Contains("CIP");
                bool mIsTechnical = mFullDesc.Contains("KỸ THUẬT") || mFullDesc.Contains("KY THUAT") || mFullDesc.Contains("TECHNICAL") || mFullDesc.Contains("CKT");
                bool mIsPrecast = mFullDesc.Contains("ĐÚC SẴN") || mFullDesc.Contains("DUC SAN") || mFullDesc.Contains("PRECAST");

                if (data.IsTechnical)
                {
                    if (mIsPrecast || mIsCastInPlace) continue;
                    if (mFullDesc.Contains("CỬA XẢ") || mFullDesc.Contains("SÂN GIA CỐ")) continue;
                }
                else if (data.IsCastInPlace)
                {
                    if (mIsPrecast || mIsTechnical) continue;
                    if (!string.IsNullOrEmpty(m.ProfileName))
                    {
                        bool isCommonComponent = mFullDesc.Contains("SÂN GIA CỐ") || mFullDesc.Contains("SAN GIA CO") ||
                                                 mFullDesc.Contains("CỬA XẢ") || mFullDesc.Contains("CUA XA") ||
                                                 mFullDesc.Contains("HỘP NỐI") || mFullDesc.Contains("HOP NOI") ||
                                                 mFullDesc.Contains("HỐ GA") || mFullDesc.Contains("HO GA");
                        if (!isCommonComponent && !string.IsNullOrEmpty(data.AssignedProfileName) && !string.Equals(m.ProfileName, data.AssignedProfileName, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                }
                else // Precast
                {
                    if (mIsCastInPlace || mIsTechnical) continue;
                    if (mFullDesc.Contains("TRÒN") && (data.LoaiCong ?? "").ToUpperInvariant().Contains("HOP")) continue;
                    if (mFullDesc.Contains("HỘP") && (data.LoaiCong ?? "").ToUpperInvariant().Contains("TRON")) continue;
                }

                // Kiểm tra khớp cấu kiện:
                // Nếu m.CategoryName đã được gán cụ thể (ví dụ "Hố kỹ thuật 1.5x1.5m", "Hộp nối cống dọc", "Đá dăm đệm thân cống"...),
                bool matchCat;
                if (!string.IsNullOrEmpty(m.CategoryName))
                {
                    bool isBeddingM = m.CategoryName.IndexOf("đá dăm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      m.CategoryName.IndexOf("da dam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      m.CategoryName.IndexOf("bê tông lót", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      m.CategoryName.IndexOf("be tong lot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      m.CategoryName.IndexOf("btl", StringComparison.OrdinalIgnoreCase) >= 0;

                    bool isBeddingTarget = categoryName.IndexOf("đá dăm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           categoryName.IndexOf("da dam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           categoryName.IndexOf("bê tông lót", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           categoryName.IndexOf("be tong lot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           categoryName.IndexOf("btl", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isBeddingM && isBeddingTarget)
                    {
                        // Phải cùng cụm cấu kiện: Cửa xả vs Sân gia cố vs Thân cống
                        bool mIsOutlet = m.CategoryName.Contains("Cửa xả") || m.GroupName.Contains("Cửa xả");
                        bool tIsOutlet = categoryName.Contains("Cửa xả");
                        bool mIsApron = m.CategoryName.Contains("Sân gia cố") || m.GroupName.Contains("Sân gia cố");
                        bool tIsApron = categoryName.Contains("Sân gia cố");
                        bool mIsBarrel = !mIsOutlet && !mIsApron;
                        bool tIsBarrel = !tIsOutlet && !tIsApron;

                        if (mIsOutlet == tIsOutlet && mIsApron == tIsApron && mIsBarrel == tIsBarrel)
                        {
                            bool isDaDam = (m.CategoryName.IndexOf("đá dăm", StringComparison.OrdinalIgnoreCase) >= 0 || m.CategoryName.IndexOf("da dam", StringComparison.OrdinalIgnoreCase) >= 0) &&
                                           (categoryName.IndexOf("đá dăm", StringComparison.OrdinalIgnoreCase) >= 0 || categoryName.IndexOf("da dam", StringComparison.OrdinalIgnoreCase) >= 0);
                            bool isBtl = (m.CategoryName.IndexOf("lót", StringComparison.OrdinalIgnoreCase) >= 0 || m.CategoryName.IndexOf("btl", StringComparison.OrdinalIgnoreCase) >= 0) &&
                                         (categoryName.IndexOf("lót", StringComparison.OrdinalIgnoreCase) >= 0 || categoryName.IndexOf("btl", StringComparison.OrdinalIgnoreCase) >= 0);
                            matchCat = isDaDam || isBtl;
                        }
                        else
                        {
                            matchCat = false;
                        }
                    }
                    else if ((m.CategoryName.IndexOf("cống hộp", StringComparison.OrdinalIgnoreCase) >= 0 || m.CategoryName.IndexOf("thân cống", StringComparison.OrdinalIgnoreCase) >= 0) &&
                             (m.CategoryName.IndexOf("liên", StringComparison.OrdinalIgnoreCase) >= 0 || categoryName.IndexOf("liên", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        // Khớp chính xác hoàn toàn đối với các loại liên thân cống (liên ngoài trái, liên trong, liên ngoài phải)
                        matchCat = string.Equals(m.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        matchCat = (categoryName.IndexOf(m.CategoryName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    m.CategoryName.IndexOf(categoryName, StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                }
                else
                {
                    matchCat = false;
                }

                bool matchFam = !string.IsNullOrEmpty(m.FamilyName) && 
                                string.Equals(m.FamilyName, famName, StringComparison.OrdinalIgnoreCase);

                bool isMatch = matchCat || (matchFam && string.IsNullOrEmpty(m.CategoryName));
                if (!isMatch) continue;

                // Nếu là tham số Yes/No: nếu người dùng bỏ tích (IsSelected == false) thì coi như chọn "Không" (0)
                // Nếu là tham số kích thước khác: nếu bỏ tích thì bỏ qua không can thiệp
                if (!m.IsSelected && !m.IsYesNoParameter)
                {
                    continue;
                }

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
                    bool isOutletVis = pNameUpper == "CX_TUONG DAU" || pNameUpper == "CX_TUONG CANH" || 
                                       pNameUpper == "CX_SAN CONG" || pNameUpper == "CX_BE TONG LOT" || 
                                       pNameUpper == "CX_DA DAM DEM" || pNameUpper == "TD_BE TONG LOT" || 
                                       pNameUpper == "TD_DA DAM DEM" || pNameUpper.Contains("_SH");

                    if (isOutletVis)
                    {
                        continue;
                    }

                    // TỰ ĐỘNG DUPLICATE SYMBOL ĐỂ THAM SỐ TYPE HOÀN TOÀN ĐỘC LẬP GIỮA CÁC CỐNG/CỤM CẤU KIỆN
                    try
                    {
                        string cleanCat = string.IsNullOrEmpty(categoryName) ? "" : "_" + System.Text.RegularExpressions.Regex.Replace(categoryName, @"[^\w\d]", "");
                        string suffix = $"_{data.STT}{cleanCat}";
                        string curName = inst.Symbol.Name;
                        if (!curName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        {
                            string desiredTypeName = $"{curName}{suffix}";
                            var existingSym = inst.Symbol.Family.GetFamilySymbolIds()
                                .Select(id => inst.Document.GetElement(id) as FamilySymbol)
                                .FirstOrDefault(s => s != null && string.Equals(s.Name, desiredTypeName, StringComparison.OrdinalIgnoreCase));

                            if (existingSym != null)
                            {
                                if (!existingSym.IsActive) existingSym.Activate();
                                inst.Symbol = existingSym;
                                p = existingSym.LookupParameter(m.InternalName);
                            }
                            else
                            {
                                var newSym = inst.Symbol.Duplicate(desiredTypeName) as FamilySymbol;
                                if (newSym != null)
                                {
                                    if (!newSym.IsActive) newSym.Activate();
                                    inst.Symbol = newSym;
                                    p = newSym.LookupParameter(m.InternalName);
                                }
                            }
                        }
                    }
                    catch { }
                }

                bool hasExplicitMapping = !string.IsNullOrEmpty(m.MappedField) && m.MappedField != "Tùy biến";

                // Xác định giá trị gán: ưu tiên MappedField từ dữ liệu cống, sau đó lấy giá trị tùy chỉnh riêng của cống
                string valStr;
                if (data.HasParamOverride(m.ProfileName, m.CategoryName, m.InternalName))
                {
                    valStr = data.GetParamOverride(m.ProfileName, m.CategoryName, m.InternalName, "");
                }
                else
                {
                    valStr = m.DefaultValue ?? "";
                }
                if (m.IsYesNoParameter)
                {
                    if (!m.IsSelected)
                    {
                        valStr = "0";
                    }
                    else if (string.IsNullOrWhiteSpace(valStr))
                    {
                        valStr = m.DefaultValue ?? "1";
                    }
                }
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
            SetParamStringWithGroup(elem, pName, "Constraints", val, null, false);
        }

        private static readonly object _spLock = new object();

        /// <summary>
        /// Gán tham số string cho Element, hỗ trợ tự động khởi tạo Shared Parameter vào nhóm Revit (mặc định Constraints)
        /// </summary>
        public static void SetParamStringWithGroup(
            Element elem,
            string pName,
            string revitGroup,
            string val,
            Document? doc = null,
            bool autoCreateSharedParams = true)
        {
            if (elem == null || string.IsNullOrEmpty(val) || string.IsNullOrWhiteSpace(pName)) return;

            Parameter p = elem.LookupParameter(pName);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String)
            {
                try { p.Set(val); return; } catch { }
            }

            // Nếu chưa có parameter và có doc + tùy chọn tự tạo Shared Parameter
            if (p == null && doc != null && autoCreateSharedParams && elem.Category != null)
            {
                try
                {
                    if (EnsureSharedParameterCreated(doc, pName, revitGroup, elem.Category))
                    {
                        p = elem.LookupParameter(pName);
                        if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String)
                        {
                            try { p.Set(val); return; } catch { }
                        }
                    }
                }
                catch { }
            }

            // Fallback sang các built-in parameter nếu parameter chưa tồn tại
            if (pName == "Tên cấu kiện" || pName.IndexOf("Mark", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Parameter pMark = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                if (pMark != null && !pMark.IsReadOnly)
                {
                    try { pMark.Set(val); } catch { }
                }
            }
            else if (pName == "Mô tả" || pName.IndexOf("Comment", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Parameter pComments = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (pComments != null && !pComments.IsReadOnly)
                {
                    try { pComments.Set(val); } catch { }
                }
            }
        }

        /// <summary>
        /// Đảm bảo Shared Parameter được khởi tạo và bind vào category với GroupTypeId (mặc định Constraints)
        /// </summary>
        public static bool EnsureSharedParameterCreated(Document doc, string paramName, string revitGroup, Category cat)
        {
            if (doc == null || string.IsNullOrWhiteSpace(paramName) || cat == null) return false;
            lock (_spLock)
            {
                try
                {
                    BindingMap map = doc.ParameterBindings;
                    DefinitionBindingMapIterator it = map.ForwardIterator();
                    while (it.MoveNext())
                    {
                        if (it.Key != null && string.Equals(it.Key.Name, paramName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (it.Current is InstanceBinding instB && instB.Categories.Contains(cat))
                            {
                                return true;
                            }
                        }
                    }

                    var app = doc.Application;
                    string originalSpFile = app.SharedParametersFilename;
                    string tempSpFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DV_TOOL_HTKT_SharedParams.txt");

                    if (!System.IO.File.Exists(tempSpFile))
                    {
                        string header = "# Temporary Shared Parameter File for DV_TOOL_HTKT\r\n" +
                                        "*META\tVERSION\tMINVERSION\r\n" +
                                        "META\t2\t1\r\n" +
                                        "*GROUP\tID\tNAME\r\n" +
                                        "GROUP\t1\tBIM_CONSTRAINTS\r\n" +
                                        "*PARAM\tGUID\tNAME\tDATATYPE\tDATACAT\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\r\n";
                        System.IO.File.WriteAllText(tempSpFile, header, System.Text.Encoding.UTF8);
                    }

                    app.SharedParametersFilename = tempSpFile;
                    DefinitionFile defFile = app.OpenSharedParameterFile();
                    if (defFile == null) return false;

                    DefinitionGroup group = defFile.Groups.get_Item("BIM_CONSTRAINTS") ?? defFile.Groups.Create("BIM_CONSTRAINTS");
                    Definition def = group.Definitions.get_Item(paramName);
                    if (def == null)
                    {
                        ExternalDefinitionCreationOptions opt = new ExternalDefinitionCreationOptions(paramName, SpecTypeId.String.Text);
                        def = group.Definitions.Create(opt);
                    }

                    CategorySet catSet = app.Create.NewCategorySet();
                    catSet.Insert(cat);
                    try { catSet.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_GenericModel)); } catch { }
                    try { catSet.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_StructuralFraming)); } catch { }
                    try { catSet.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_StructuralFoundation)); } catch { }

                    InstanceBinding binding = app.Create.NewInstanceBinding(catSet);

                    ForgeTypeId groupTypeId = GroupTypeId.Constraints;
                    if (string.Equals(revitGroup, "Identity Data", StringComparison.OrdinalIgnoreCase))
                        groupTypeId = GroupTypeId.IdentityData;
                    else if (string.Equals(revitGroup, "Data", StringComparison.OrdinalIgnoreCase))
                        groupTypeId = GroupTypeId.Data;
                    else if (string.Equals(revitGroup, "Text", StringComparison.OrdinalIgnoreCase))
                        groupTypeId = GroupTypeId.Text;
                    else if (string.Equals(revitGroup, "Phasing", StringComparison.OrdinalIgnoreCase))
                        groupTypeId = GroupTypeId.Phasing;

                    doc.ParameterBindings.Insert(def, binding, groupTypeId);

                    if (!string.IsNullOrEmpty(originalSpFile) && System.IO.File.Exists(originalSpFile))
                    {
                        try { app.SharedParametersFilename = originalSpFile; } catch { }
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Cập nhật riêng thông tin BIM vào các cấu kiện đã có trong mô hình Revit mà không cần dựng lại 3D
        /// </summary>
        public static (int UpdatedCount, List<string> Logs) UpdateBimParametersForExistingModel(
            Document doc,
            IList<CulvertRowData> culvertList,
            IEnumerable<CustomBimParameterItem> customParams,
            BimInfoConfig bimConfig,
            IEnumerable<CulvertMaterialItem>? materialSettings)
        {
            var logs = new List<string>();
            int count = 0;
            if (doc == null || culvertList == null || !culvertList.Any())
            {
                logs.Add("❌ Không có dữ liệu cống hoặc file Revit không khả dụng.");
                return (0, logs);
            }

            using (Transaction tx = new Transaction(doc, "DV_TOOL: Cập nhật thông tin BIM"))
            {
                tx.Start();
                try
                {
                    var collector = new FilteredElementCollector(doc)
                        .WherePasses(new ElementMulticategoryFilter(new List<BuiltInCategory>
                        {
                            BuiltInCategory.OST_GenericModel,
                            BuiltInCategory.OST_StructuralFraming,
                            BuiltInCategory.OST_StructuralFoundation
                        }))
                        .WhereElementIsNotElementType()
                        .ToElements();

                    foreach (var elem in collector)
                    {
                        if (elem is not FamilyInstance fi) continue;
                        string famName = fi.Symbol?.FamilyName ?? "";

                        CulvertRowData? matchedRow = null;
                        Parameter pLt = elem.LookupParameter("Lý trình") ?? elem.LookupParameter("BIM_LyTrinh");
                        if (pLt != null && !string.IsNullOrEmpty(pLt.AsString()))
                        {
                            string ltVal = pLt.AsString().Trim();
                            matchedRow = culvertList.FirstOrDefault(r => string.Equals((r.LyTrinh ?? "").Trim(), ltVal, StringComparison.OrdinalIgnoreCase));
                        }

                        if (matchedRow == null)
                        {
                            LocationPoint? lp = fi.Location as LocationPoint;
                            if (lp != null)
                            {
                                XYZ pt = lp.Point;
                                matchedRow = culvertList.FirstOrDefault(r =>
                                {
                                    double d1 = Math.Sqrt(Math.Pow(pt.X - UnitUtils.ConvertToInternalUnits(r.X1, UnitTypeId.Meters), 2) +
                                                          Math.Pow(pt.Y - UnitUtils.ConvertToInternalUnits(r.Y1, UnitTypeId.Meters), 2));
                                    double d2 = Math.Sqrt(Math.Pow(pt.X - UnitUtils.ConvertToInternalUnits(r.X2, UnitTypeId.Meters), 2) +
                                                          Math.Pow(pt.Y - UnitUtils.ConvertToInternalUnits(r.Y2, UnitTypeId.Meters), 2));
                                    return (d1 <= 30.0 || d2 <= 30.0);
                                });
                            }
                        }

                        if (matchedRow == null && culvertList.Count == 1)
                        {
                            matchedRow = culvertList[0];
                        }

                        if (matchedRow == null) continue;

                        string scope = "Tất cả cấu kiện";
                        int? dotIdx = null;
                        string? viTri = null;

                        if (famName.Contains("Đốt") || famName.Contains("Dot") || famName.Contains("Thân") || famName.Contains("Than"))
                        {
                            scope = matchedRow.IsCastInPlace ? "Cống hộp liên trong" : "Đốt cống đúc sẵn";
                            string currentMark = fi.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "";
                            var matchDigits = System.Text.RegularExpressions.Regex.Match(currentMark, @"\d+");
                            if (matchDigits.Success && int.TryParse(matchDigits.Value, out int idxVal))
                                dotIdx = idxVal;
                        }
                        else if (famName.Contains("Cửa xả") || famName.Contains("Tường đầu") || famName.Contains("Tường cánh"))
                        {
                            scope = "Cửa xả";
                            LocationPoint? lp = fi.Location as LocationPoint;
                            if (lp != null)
                            {
                                double dTL = Math.Sqrt(Math.Pow(lp.Point.X - UnitUtils.ConvertToInternalUnits(matchedRow.X1, UnitTypeId.Meters), 2) +
                                                       Math.Pow(lp.Point.Y - UnitUtils.ConvertToInternalUnits(matchedRow.Y1, UnitTypeId.Meters), 2));
                                double dHL = Math.Sqrt(Math.Pow(lp.Point.X - UnitUtils.ConvertToInternalUnits(matchedRow.X2, UnitTypeId.Meters), 2) +
                                                       Math.Pow(lp.Point.Y - UnitUtils.ConvertToInternalUnits(matchedRow.Y2, UnitTypeId.Meters), 2));
                                viTri = (dTL <= dHL) ? "TL" : "HL";
                            }
                        }
                        else if (famName.Contains("Sân") || famName.Contains("San"))
                        {
                            scope = "Sân gia cố";
                            viTri = "TL";
                        }
                        else if (famName.Contains("Hố") || famName.Contains("Hộp") || famName.Contains("Ho ga"))
                        {
                            scope = "Hố ga (Hộp nối)";
                        }

                        string? matName = null;
                        if (materialSettings != null)
                        {
                            var matItem = materialSettings.FirstOrDefault(m => m.CategoryType.Contains(scope) || scope.Contains(m.CategoryType));
                            matName = matItem?.MaterialName;
                        }

                        ApplyCustomBimParameters(elem, customParams, matchedRow, scope, dotIdx, viTri, matName, doc, bimConfig.AutoCreateSharedParameters);
                        count++;
                    }

                    tx.Commit();
                    logs.Add($"✅ Đã cập nhật thành công thông tin BIM cho {count} cấu kiện trong Revit.");
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    logs.Add($"❌ Lỗi cập nhật BIM: {ex.Message}");
                }
            }

            return (count, logs);
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
