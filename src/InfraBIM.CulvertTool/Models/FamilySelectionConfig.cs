namespace InfraBIM.CulvertTool.Models
{
    /// <summary>
    /// Cấu hình chọn Family Symbol cho các cấu kiện
    /// </summary>
    public class FamilySelectionConfig
    {
        public string SelectedDotChuanName { get; set; } = string.Empty;
        public string SelectedDotBuName { get; set; } = string.Empty;
        public string SelectedSanCongTLName { get; set; } = string.Empty;
        public string SelectedSanCongHLName { get; set; } = string.Empty;
        public string SelectedHopNoiName { get; set; } = string.Empty;
        public string SelectedBeTongLotName { get; set; } = string.Empty;
    }
}
