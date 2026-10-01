namespace InfraBIM.CulvertTool.Models
{
    public enum ValidationLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Bản ghi kết quả kiểm tra hình học và dữ liệu đầu vào (Tab 04)
    /// </summary>
    public class ValidationResultItem
    {
        public ValidationLevel Level { get; set; } = ValidationLevel.Info;
        public int RowIndex { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public string LevelBadge => Level switch
        {
            ValidationLevel.Error => "❌ LỖI",
            ValidationLevel.Warning => "⚠️ CẢNH BÁO",
            _ => "ℹ️ THÔNG TIN"
        };
    }
}
