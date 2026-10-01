namespace InfraBIM.CulvertTool.Models
{
    /// <summary>
    /// Cấu hình dữ liệu và tham số BIM (Tab 03)
    /// </summary>
    public class BimInfoConfig
    {
        public string TenCongTrinh { get; set; } = "DT770B_CỐNG NGANG_CBS";
        public string TenNhom { get; set; } = "CỐNG TRÒN_2D1500";
        public string MoTa { get; set; } = "THÂN CỐNG ĐÚC SẴN";

        public string MauTenDotCong { get; set; } = "TCĐS.{STT}";
        public string MauTenSanCongTL { get; set; } = "SÂN CỐNG TL";
        public string MauTenSanCongHL { get; set; } = "SÂN CỐNG HL";
        public string MauTenHopNoi { get; set; } = "HT.{STT}";
        public string MauTenBeTongLot { get; set; } = "BTL.HT";

        // Tùy chọn gán tham số
        public bool AutoSetCoordinates { get; set; } = true;
        public bool AutoSetElevations { get; set; } = true;
        public bool AutoCreateSharedParameters { get; set; } = true;
    }
}
