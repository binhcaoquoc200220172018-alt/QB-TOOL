using System;
using System.Collections.Generic;

namespace InfraBIM.CulvertTool.Models
{
    /// <summary>
    /// Chứa thông tin 19 trường dữ liệu chuẩn từ bảng Excel đầu vào
    /// </summary>
    public class CulvertRowData
    {
        // Cột A -> E: Thông tin chung
        public int STT { get; set; }
        public string LyTrinh { get; set; } = string.Empty;
        public string LoaiCong { get; set; } = "Cống hộp"; // Cống hộp / Cống tròn / Cống kỹ thuật
        public string CauKien { get; set; } = "Đúc sẵn";   // Đúc sẵn / Đổ tại chỗ
        public int SoCua { get; set; } = 1;
        public string KhauDo { get; set; } = "1.5x1.5";

        // Cột F -> H: Tọa độ sân cống 1 (Thượng lưu - VN2000)
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double Z1 { get; set; }

        // Cột I -> K: Tọa độ sân cống 2 (Hạ lưu - VN2000)
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public double Z2 { get; set; }

        // Cột L -> N: Thông số hình học thiết kế
        public double ChieuDai { get; set; }
        public double DoDoc { get; set; }
        public double GocXoay { get; set; }

        // Cột O -> Q: Hộp nối cống dọc (Khoảng cách hộp nối KC_HN1, KC_HN2)
        public int SoHopNoi { get; set; } = 0; // 0, 1, 2
        public double Dist_HN1 { get; set; }
        public double Dist_HN2 { get; set; }

        // Alias thân thiện theo tiếng Việt
        public double KC_HN1
        {
            get => Dist_HN1;
            set => Dist_HN1 = value;
        }

        public double KC_HN2
        {
            get => Dist_HN2;
            set => Dist_HN2 = value;
        }

        // Bề rộng hố thu 1 và hố thu 2 (m)
        public double B_HT1 { get; set; } = 1.50;
        public double B_HT2 { get; set; } = 1.50;

        public double B_HN1
        {
            get => B_HT1;
            set => B_HT1 = value;
        }

        public double B_HN2
        {
            get => B_HT2;
            set => B_HT2 = value;
        }

        // Cột R -> S: Khe hở và ngàm
        public double L_Ngam_San { get; set; } = 0.30;
        public double Khe_Ho_HN { get; set; } = 0.05;

        // Bổ sung: Khoảng cách giữa 2 tim cống tròn đôi (m)
        public double KhoangCachTim { get; set; } = 2.0;

        // Chiều dài Sân gia cố Thượng lưu và Hạ lưu (m) - Family Adaptive 2 điểm
        public double L_SGC_TL { get; set; } = 3.0;
        public double L_SGC_HL { get; set; } = 3.0;

        // Cột cuối: Ghi chú loại cống rải tự động
        public string GhiChu { get; set; } = string.Empty;

        // Hồ sơ loại cống chỉ định (vd: "Cống đổ tại chỗ 1", "Cống đổ tại chỗ 2"...)
        public string AssignedProfileName { get; set; } = string.Empty;

        /// <summary>
        /// Tự động xác định loại cống rải chính xác theo cấu trúc thiết lập
        /// </summary>
        public bool? IsTechnicalOverride { get; set; }

        public string DetermineCulvertType()
        {
            if (IsTechnicalOverride == true) return "CỐNG KỸ THUẬT";
            if (!string.IsNullOrEmpty(AssignedProfileName))
            {
                return AssignedProfileName;
            }

            string gc = (GhiChu ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(gc))
            {
                if (gc.Contains("KỸ THUẬT") || gc.Contains("KY THUAT")) return "CỐNG KỸ THUẬT";
                if (gc.Contains("TRÒN ĐÔI") || gc.Contains("TRON DOI") || gc.Contains("ĐÔI")) return "CỐNG TRÒN ĐÔI";
                if (gc.Contains("TRÒN") || gc.Contains("TRON")) return "CỐNG TRÒN ĐƠN";
                if (gc.Contains("ĐỔ TẠI CHỖ") || gc.Contains("DO TAI CHO")) return "CỐNG HỘP ĐỔ TẠI CHỖ";
                if (gc.Contains("HỘP ĐÚC SẴN") || gc.Contains("HOP DUC SAN")) return "CỐNG HỘP ĐÚC SẴN";
                if (gc.Contains("HỘP") || gc.Contains("HOP")) return (SoCua > 1) ? "CỐNG HỘP ĐỔ TẠI CHỖ" : "CỐNG HỘP ĐÚC SẴN";
            }

            string lc = (LoaiCong ?? string.Empty).Trim().ToUpperInvariant();
            string ck = (CauKien ?? string.Empty).Trim().ToUpperInvariant();

            if (lc.Contains("KỸ THUẬT") || lc.Contains("KY THUAT")) return "CỐNG KỸ THUẬT";

            if (lc.Contains("TRON") || lc.Contains("TRÒN") || lc.Contains("CT"))
            {
                if (SoCua > 1) return "CỐNG TRÒN ĐÔI";
                return "CỐNG TRÒN ĐƠN";
            }

            // Mặc định: cống hộp
            if (SoCua > 1) return "CỐNG HỘP ĐỔ TẠI CHỖ";
            if (ck.Contains("ĐỔ") || ck.Contains("DO") || ck.Contains("TẠI CHỖ")) return "CỐNG HỘP ĐỔ TẠI CHỖ";
            return "CỐNG HỘP ĐÚC SẴN";
        }

        public string ResolvedCulvertType => DetermineCulvertType();

        public bool IsCastInPlace => DetermineCulvertType().Contains("ĐỔ TẠI CHỖ") || (SoCua > 1) || (!string.IsNullOrEmpty(AssignedProfileName) && AssignedProfileName.Contains("đổ tại chỗ", StringComparison.OrdinalIgnoreCase));
        public bool IsTechnical => IsTechnicalOverride ?? (DetermineCulvertType().Contains("KỸ THUẬT") || (LoaiCong != null && (LoaiCong.Contains("kỹ thuật", StringComparison.OrdinalIgnoreCase) || LoaiCong.Contains("CKT", StringComparison.OrdinalIgnoreCase))) || (GhiChu != null && (GhiChu.Contains("kỹ thuật", StringComparison.OrdinalIgnoreCase) || GhiChu.Contains("CKT", StringComparison.OrdinalIgnoreCase))));
        public bool IsPrecast => !IsTechnical && !IsCastInPlace;

        // Quản lý giá trị tùy chỉnh tham số cho từng cống (Yêu cầu 4)
        public string ActiveParamName { get; set; } = string.Empty;
        public string ActiveParamValue { get; set; } = string.Empty;
        public Dictionary<string, string> ParameterOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public const string NoOverrideSentinel = "__INFRA_NO_OVERRIDE__";

        public bool HasParamOverride(string? paramName)
        {
            return HasParamOverride(null, null, paramName);
        }

        public bool HasParamOverride(string? categoryName, string? paramName)
        {
            return HasParamOverride(null, categoryName, paramName);
        }

        public bool HasParamOverride(string? profileName, string? categoryName, string? paramName)
        {
            if (string.IsNullOrEmpty(paramName)) return false;
            string val = GetParamOverride(profileName, categoryName, paramName, NoOverrideSentinel);
            return !string.Equals(val, NoOverrideSentinel, StringComparison.Ordinal);
        }

        public string GetParamOverride(string? paramName, string defaultValue)
        {
            return GetParamOverride(null, null, paramName, defaultValue);
        }

        public string GetParamOverride(string? categoryName, string? paramName, string defaultValue)
        {
            return GetParamOverride(null, categoryName, paramName, defaultValue);
        }

        public string GetParamOverride(string? profileName, string? categoryName, string? paramName, string defaultValue)
        {
            if (string.IsNullOrEmpty(paramName)) return defaultValue;

            string effectiveProfile = !string.IsNullOrEmpty(profileName)
                ? profileName
                : (!string.IsNullOrEmpty(AssignedProfileName)
                    ? AssignedProfileName
                    : (IsCastInPlace ? "Cống đổ tại chỗ" : (IsTechnical ? "Cống kỹ thuật" : "Cống đúc sẵn")));

            // 1. Khóa đầy đủ theo cả Profile và Category (vd: "Cống đổ tại chỗ 1::Cống hộp (liên trong)::H")
            if (!string.IsNullOrEmpty(effectiveProfile) && !string.IsNullOrEmpty(categoryName))
            {
                string fullKey = $"{effectiveProfile.Trim()}::{categoryName.Trim()}::{paramName.Trim()}";
                if (ParameterOverrides.TryGetValue(fullKey, out var pVal) && !string.IsNullOrWhiteSpace(pVal))
                {
                    return pVal;
                }

                // 1b. Khớp tương đối theo nhóm cấu kiện TRONG CÙNG PROFILE (vd: "Đá dăm đệm liên ngoài trái" khớp với "Đá dăm đệm thân cống" của chính profile này)
                string catClean = categoryName.Trim().ToLowerInvariant();
                string profPrefix = $"{effectiveProfile.Trim().ToLowerInvariant()}::";
                foreach (var kvp in ParameterOverrides)
                {
                    string kLower = kvp.Key.ToLowerInvariant();
                    if (kLower.StartsWith(profPrefix) && kLower.EndsWith($"::{paramName.Trim().ToLowerInvariant()}"))
                    {
                        if ((catClean.Contains("đá dăm") || catClean.Contains("da dam")) && (kLower.Contains("đá dăm") || kLower.Contains("da dam")))
                            return kvp.Value;
                        if ((catClean.Contains("bê tông lót") || catClean.Contains("btl") || catClean.Contains("be tong lot")) && (kLower.Contains("bê tông lót") || kLower.Contains("btl") || kLower.Contains("be tong lot")))
                            return kvp.Value;
                        if (catClean.Contains("cống hộp") && kLower.Contains("cống hộp"))
                        {
                            bool cLeft = catClean.Contains("trái") || catClean.Contains("trai");
                            bool cRight = catClean.Contains("phải") || catClean.Contains("phai");
                            bool cMid = catClean.Contains("trong");
                            bool kLeft = kLower.Contains("trái") || kLower.Contains("trai");
                            bool kRight = kLower.Contains("phải") || kLower.Contains("phai");
                            bool kMid = kLower.Contains("trong");

                            // Nếu cả 2 đều chỉ định vị trí phân đoạn cụ thể khác nhau thì KHÔNG match chéo
                            if ((cLeft && (kRight || kMid)) || (cRight && (cLeft || kMid)) || (cMid && (kLeft || kRight)))
                                continue;

                            return kvp.Value;
                        }
                    }
                }
            }

            // 2. Khóa theo Profile và tên param (vd: "Cống đổ tại chỗ 1::B_DDD") - Chỉ dùng cho thân cống/đệm hoặc khi không chỉ định category
            bool isSpecificNonBarrel = !string.IsNullOrEmpty(categoryName) && 
                (categoryName.IndexOf("cửa xả", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 categoryName.IndexOf("cua xa", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 categoryName.IndexOf("sân gia cố", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 categoryName.IndexOf("san gia co", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 categoryName.IndexOf("hố ga", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 categoryName.IndexOf("ho ga", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 categoryName.IndexOf("hố kỹ thuật", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 categoryName.IndexOf("ho ky thuat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 categoryName.IndexOf("hộp nối", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 categoryName.IndexOf("hop noi", StringComparison.OrdinalIgnoreCase) >= 0);

            if (!string.IsNullOrEmpty(effectiveProfile) && !isSpecificNonBarrel)
            {
                string profKey = $"{effectiveProfile.Trim()}::{paramName.Trim()}";
                if (ParameterOverrides.TryGetValue(profKey, out var profVal) && !string.IsNullOrWhiteSpace(profVal))
                {
                    return profVal;
                }
            }

            // 3. Khóa theo Category (áp dụng cho các cấu kiện chung như Cửa xả, Sân gia cố)
            if (!string.IsNullOrEmpty(categoryName))
            {
                string catClean = categoryName.Trim().ToLowerInvariant();
                bool isOutletTarget = catClean.Contains("cửa xả") || catClean.Contains("cua xa") || catClean.Contains("tường") || catClean.Contains("sân cống");
                bool isApronTarget = catClean.Contains("sân gia cố") || catClean.Contains("san gia co") || catClean.Contains("tấm sân");
                bool isTechManholeTarget = catClean.Contains("hố kỹ thuật") || catClean.Contains("ho ky thuat");
                bool isJuncTarget = catClean.Contains("hộp nối") || catClean.Contains("hop noi");
                bool isBarrelTarget = !isOutletTarget && !isApronTarget && !isTechManholeTarget && !isJuncTarget;

                // Thân cống, Hố kỹ thuật và Hộp nối cống có Profile riêng biệt, KHÔNG lấy khóa Category trần không Profile để chống đá biến
                if (!isBarrelTarget && !isTechManholeTarget && !isJuncTarget)
                {
                    string catKey = $"{categoryName.Trim()}::{paramName.Trim()}";
                    if (ParameterOverrides.TryGetValue(catKey, out var catVal) && !string.IsNullOrWhiteSpace(catVal))
                    {
                        return catVal;
                    }
                }

                // 3b. Khớp tương đối trong cùng cụm cấu kiện (vd: "Cửa xả - Đá dăm đệm" không bị lẫn với "Sân gia cố - Đá dăm đệm")
                foreach (var kvp in ParameterOverrides)
                {
                    if (kvp.Key.EndsWith($"::{paramName.Trim()}", StringComparison.OrdinalIgnoreCase))
                    {
                        string kLower = kvp.Key.ToLowerInvariant();
                        bool kIsOutlet = kLower.Contains("cửa xả") || kLower.Contains("cua xa") || kLower.Contains("tường") || kLower.Contains("sân cống");
                        bool kIsApron = kLower.Contains("sân gia cố") || kLower.Contains("san gia co") || kLower.Contains("tấm sân");
                        bool kIsTechManhole = kLower.Contains("hố kỹ thuật") || kLower.Contains("ho ky thuat");
                        bool kIsJunc = kLower.Contains("hộp nối") || kLower.Contains("hop noi");
                        bool kIsBarrel = !kIsOutlet && !kIsApron && !kIsTechManhole && !kIsJunc;

                        // Chỉ cho phép khớp tương đối nếu thuộc cùng một phân nhóm lớn
                        if (isOutletTarget == kIsOutlet && isApronTarget == kIsApron && isTechManholeTarget == kIsTechManhole && isJuncTarget == kIsJunc && isBarrelTarget == kIsBarrel)
                        {
                            // TUYỆT ĐỐI KHÔNG KHỚP CHÉO GIỮA ĐÚC SẴN, ĐỔ TẠI CHỖ VÀ KỸ THUẬT (CẢ THÂN CỐNG, CỬA XẢ, SÂN GIA CỐ)
                            bool kIsCip = kLower.Contains("đổ tại chỗ") || kLower.Contains("do tai cho") || kLower.Contains("cip");
                            bool kIsPrecast = kLower.Contains("đúc sẵn") || kLower.Contains("duc san") || kLower.Contains("precast");
                            bool kIsCkt = kLower.Contains("kỹ thuật") || kLower.Contains("ky thuat");

                            if (IsPrecast && (kIsCip || kIsCkt)) continue;
                            if (IsCastInPlace && (kIsPrecast || kIsCkt)) continue;
                            if (IsTechnical && (kIsPrecast || kIsCip)) continue;

                            if ((catClean.Contains("đá dăm") || catClean.Contains("da dam")) && (kLower.Contains("đá dăm") || kLower.Contains("da dam")))
                                return kvp.Value;
                            if ((catClean.Contains("bê tông lót") || catClean.Contains("btl") || catClean.Contains("be tong lot")) && (kLower.Contains("bê tông lót") || kLower.Contains("btl") || kLower.Contains("be tong lot")))
                                return kvp.Value;
                        }
                    }
                }
            }

            // 4. Khóa toàn cục theo tên param CHỈ KHI tham số được gán dạng độc lập (không Profile, không Category)
            if (ParameterOverrides.TryGetValue(paramName.Trim(), out var val) && !string.IsNullOrWhiteSpace(val))
            {
                // Các tham số kích thước thân cống, hố kỹ thuật, hộp nối cống và đệm cống TUYỆT ĐỐI KHÔNG nhận fallback toàn cục để chống đá biến
                bool isSpecificParam = paramName.StartsWith("CH_", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.StartsWith("HOP NOI CONG_", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.StartsWith("THAN GIENG_", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.StartsWith("CO GIENG_", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.StartsWith("HGDH_", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.StartsWith("TAM DAN_", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("CONG HOP_B", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("CONG HOP_H", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("BTL_H", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("B_DDD", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("H_DDD", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("B_BTL", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("H_BTL", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("B", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("H", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("L", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("L_Std", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("CO VAI KE", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("ẨN PHẢI", StringComparison.OrdinalIgnoreCase) ||
                                       paramName.Equals("ẨN TRÁI", StringComparison.OrdinalIgnoreCase);

                if (!isSpecificParam)
                {
                    return val;
                }
            }

            return defaultValue;
        }

        public void SetParamOverride(string? paramName, string val)
        {
            SetParamOverride(null, null, paramName, val);
        }

        public void SetParamOverride(string? categoryName, string? paramName, string val)
        {
            SetParamOverride(null, categoryName, paramName, val);
        }

        public void SetParamOverride(string? profileName, string? categoryName, string? paramName, string val)
        {
            if (string.IsNullOrEmpty(paramName)) return;

            string effectiveProfile = !string.IsNullOrEmpty(profileName)
                ? profileName
                : (!string.IsNullOrEmpty(AssignedProfileName)
                    ? AssignedProfileName
                    : (IsCastInPlace ? "Cống đổ tại chỗ" : (IsTechnical ? "Cống kỹ thuật" : "Cống đúc sẵn")));

            if (!string.IsNullOrEmpty(effectiveProfile) && !string.IsNullOrEmpty(categoryName))
            {
                string fullKey = $"{effectiveProfile.Trim()}::{categoryName.Trim()}::{paramName.Trim()}";
                ParameterOverrides[fullKey] = val;
            }
            else if (!string.IsNullOrEmpty(categoryName))
            {
                string catKey = $"{categoryName.Trim()}::{paramName.Trim()}";
                ParameterOverrides[catKey] = val;
            }
            else if (!string.IsNullOrEmpty(effectiveProfile))
            {
                string profKey = $"{effectiveProfile.Trim()}::{paramName.Trim()}";
                ParameterOverrides[profKey] = val;
            }
            else
            {
                ParameterOverrides[paramName.Trim()] = val;
            }

            ActiveParamName = paramName;
            ActiveParamValue = val;
        }

        // Trạng thái kiểm tra (Validation)
        public bool IsSelected { get; set; } = true;
        public bool HasError { get; set; } = false;
        public string StatusNote { get; set; } = "Hợp lệ";

        public double DeltaZ => Z1 - Z2;
        public double DoDocTinhToan => TinhDoDocThucTe();

        // Tính toán kiểm tra sai số hình học
        public double TinhChieuDai2D()
        {
            double dx = X2 - X1;
            double dy = Y2 - Y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public double TinhChieuDai3D()
        {
            double dx = X2 - X1;
            double dy = Y2 - Y1;
            double dz = Z2 - Z1;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public double TinhDoDocThucTe()
        {
            double l2D = TinhChieuDai2D();
            if (l2D < 0.001) return 0;
            return Math.Abs(Z1 - Z2) / l2D * 100.0;
        }

        public double TinhGocAzimuthDeg()
        {
            double dx = X2 - X1;
            double dy = Y2 - Y1;
            double rad = Math.Atan2(dx, dy); // Azimuth từ trục Bắc (Y) thuận chiều kim đồng hồ
            double deg = rad * (180.0 / Math.PI);
            if (deg < 0) deg += 360.0;
            return deg;
        }

        public double TinhGocXoayMatBangRad()
        {
            double dx = X2 - X1;
            double dy = Y2 - Y1;
            return Math.Atan2(dy, dx); // Góc vector u so với trục X (Revit East)
        }
    }
}
