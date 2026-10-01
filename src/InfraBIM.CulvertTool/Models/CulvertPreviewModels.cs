using System.Collections.Generic;

namespace InfraBIM.CulvertTool.Models
{
    public enum PreviewViewMode
    {
        Profile2D = 0,
        Plan2D = 1,
        Isometric3D = 2
    }

    /// <summary>
    /// Thông tin 1 đốt cống trong bản vẽ rải đốt mô phỏng
    /// </summary>
    public class PreviewSegmentItem
    {
        public int Index { get; set; }
        public string SegmentName { get; set; } = string.Empty;
        public bool IsStandard { get; set; }
        public double LengthM { get; set; }
        public double StartDistanceM { get; set; }
        public double EndDistanceM { get; set; }
        public double StartElevationZ { get; set; }
        public double EndElevationZ { get; set; }
        public string Suffix { get; set; } = string.Empty;
    }

    /// <summary>
    /// Thông tin sân cống trong bản vẽ mô phỏng
    /// </summary>
    public class PreviewApronItem
    {
        public string Title { get; set; } = string.Empty;
        public double PositionX { get; set; }
        public double ElevationZ { get; set; }
        public double LengthM { get; set; } = 2.0;
        public double WallHeightM { get; set; } = 1.8;
        public double SlabThicknessM { get; set; } = 0.3;
    }

    /// <summary>
    /// Thông tin hộp nối / hố thu trong bản vẽ mô phỏng
    /// </summary>
    public class PreviewManholeItem
    {
        public int Index { get; set; }
        public string Title { get; set; } = string.Empty;
        public double DistanceFromP1M { get; set; }
        public double WidthM { get; set; } = 1.5;
        public double ElevationZ { get; set; }
        public double HeightM { get; set; } = 2.0;
    }

    /// <summary>
    /// Toàn bộ dữ liệu tính toán hình học phục vụ vẽ Preview
    /// </summary>
    public class CulvertPreviewGeometry
    {
        public int STT { get; set; }
        public string LyTrinh { get; set; } = string.Empty;
        public string LoaiCong { get; set; } = string.Empty;
        public int SoCua { get; set; } = 1;
        public string KhauDo { get; set; } = string.Empty;
        public double KhoangCachTim { get; set; } = 2.0;
        public double TotalLengthM { get; set; }
        public double DoDocPercent { get; set; }
        public double GocXoayDeg { get; set; }
        public double Z1 { get; set; }
        public double Z2 { get; set; }
        public double DeltaH { get; set; }

        public double L_Std { get; set; } = 1.0;
        public double L_Min { get; set; } = 0.5;
        public double L_Ngam { get; set; } = 0.3;
        public double B_Box { get; set; } = 1.5;
        public double OffsetZ_BTL { get; set; } = -0.10;
        public double OffsetZ_Cat { get; set; } = -0.20;

        public bool HasBTL_Dot { get; set; } = true;
        public bool HasBTL_San { get; set; } = false;
        public bool HasBTL_HN { get; set; } = false;
        public double OffsetZ_BTL_Dot { get; set; } = -0.10;
        public double OffsetZ_BTL_San { get; set; } = -0.10;
        public double OffsetZ_BTL_HN { get; set; } = -0.30;

        public PreviewApronItem ApronTL { get; set; } = new();
        public PreviewApronItem ApronHL { get; set; } = new();
        public List<PreviewSegmentItem> Segments { get; } = new();
        public List<PreviewManholeItem> Manholes { get; } = new();

        public int StandardCount { get; set; }
        public int CompensatingCount { get; set; }
        public double CompensatingLengthM { get; set; }
        public bool IsCompensatingValid { get; set; } = true;
        public string ValidationMessage { get; set; } = "Hình học hợp lệ";

        // Khẩu độ thực tế quy đổi sang mét
        public double BarrelWidthM { get; set; } = 1.5;
        public double BarrelHeightM { get; set; } = 1.5;
        public double WallThicknessM { get; set; } = 0.20;
    }
}
