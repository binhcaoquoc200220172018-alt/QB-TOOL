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
        public string LoaiCong { get; set; } = "CONG_TRON"; // CONG_TRON / CONG_HOP
        public int SoCua { get; set; } = 1;
        public string KhauDo { get; set; } = "D1500";

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

        // Cột R -> S: Khe hở và ngàm
        public double L_Ngam_San { get; set; } = 0.30;
        public double Khe_Ho_HN { get; set; } = 0.05;

        // Bổ sung: Khoảng cách giữa 2 tim cống tròn đôi (m)
        public double KhoangCachTim { get; set; } = 2.0;

        // Trạng thái kiểm tra (Validation)
        public bool IsSelected { get; set; } = true;
        public bool HasError { get; set; } = false;
        public string StatusNote { get; set; } = "Hợp lệ";

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
