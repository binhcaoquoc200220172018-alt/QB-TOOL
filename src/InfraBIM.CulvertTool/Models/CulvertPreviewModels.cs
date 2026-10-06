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
        public double BottomElevationZ { get; set; }
        public double TopElevationZ { get; set; }
        public double HeightM { get; set; } = 2.0;
    }

    /// <summary>
    /// Toàn bộ dữ liệu tính toán hình học phục vụ vẽ Preview & Kiểm tra cao độ
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

        // CAO ĐỘ THIẾT KẾ & KIỂM TRA THỦY LỰC
        public double Z_Top1 { get; set; }           // Cao độ đỉnh cống thượng lưu
        public double Z_Top2 { get; set; }           // Cao độ đỉnh cống hạ lưu
        public double Z_Bot_BTL1 { get; set; }       // Cao độ đáy BTL thượng lưu
        public double Z_Bot_BTL2 { get; set; }       // Cao độ đáy BTL hạ lưu
        public double Z_Bot_Cat1 { get; set; }       // Cao độ đáy đá dăm thượng lưu
        public double Z_Bot_Cat2 { get; set; }       // Cao độ đáy đá dăm hạ lưu

        public double CalculatedSlopePercent { get; set; } // Độ dốc tính từ (Z1 - Z2) / L * 100%
        public double SlopeDiffPercent { get; set; }       // Sai lệch |DoDocPercent - CalculatedSlopePercent|
        public bool IsReverseSlope { get; set; }          // Z1 < Z2 (Dốc ngược)
        public bool IsFlatSlope { get; set; }             // i < 0.1%
        public bool IsSteepSlope { get; set; }            // i > 5.0%
        public string ElevationStatus { get; set; } = "HỢP LỆ";
        public string ElevationStatusColor { get; set; } = "#10B981"; // Mint / Red / Yellow
        public string ElevationNote { get; set; } = string.Empty;

        public double L_Std { get; set; } = 1.0;
        public double L_Min { get; set; } = 0.5;
        public double L_Ngam { get; set; } = 0.0;
        public double B_Box { get; set; } = 1.5;
        public double B_HT1 { get; set; } = 1.5;
        public double B_HT2 { get; set; } = 1.5;
        public double OffsetZ_BTL { get; set; } = -0.10;
        public double OffsetZ_Cat { get; set; } = -0.20;

        public bool IsCastInPlace { get; set; } = false;
        public double JointGapM { get; set; } = 0.01;
        public bool IsRoundCulvert { get; set; } = false;
        public bool IsDoublePipe { get; set; } = false;
        public bool HasGoiCong { get; set; } = false;
        public bool HasMongTren { get; set; } = false;
        public bool HasDaDamDem { get; set; } = true;

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

        // Hình học 3D thực tế trích xuất trực tiếp từ các FamilyInstance trong Revit Document
        public List<(System.Windows.Media.Media3D.MeshGeometry3D Mesh, System.Windows.Media.Brush Brush)>? RealRevitMeshes { get; set; }
    }
}
