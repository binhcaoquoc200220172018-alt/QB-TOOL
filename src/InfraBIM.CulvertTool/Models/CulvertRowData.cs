using System;

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

        // Cột cuối: Ghi chú loại cống rải tự động
        public string GhiChu { get; set; } = string.Empty;

        /// <summary>
        /// Tự động xác định loại cống rải chính xác theo cấu trúc thiết lập
        /// </summary>
        public string DetermineCulvertType()
        {
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
