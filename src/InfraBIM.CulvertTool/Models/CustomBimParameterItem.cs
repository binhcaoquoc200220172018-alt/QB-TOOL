using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InfraBIM.CulvertTool.Models
{
    /// <summary>
    /// Tham số BIM phi hình học tùy biến động (Tab 03)
    /// Hỗ trợ gán linh hoạt theo tiêu chuẩn từng dự án BIM
    /// </summary>
    public class CustomBimParameterItem : INotifyPropertyChanged
    {
        public static readonly List<string> AvailableTargetScopes = new()
        {
            "Tất cả cấu kiện",
            "Đốt cống",
            "Sân cống",
            "Hộp nối",
            "Bê tông lót",
            "Đệm cát / Gối cống"
        };

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private string _paramName = "BIM_LyTrinh";
        public string ParamName
        {
            get => _paramName;
            set { _paramName = value; OnPropertyChanged(); }
        }

        private string _targetScope = "Tất cả cấu kiện";
        public string TargetScope
        {
            get => _targetScope;
            set { _targetScope = value; OnPropertyChanged(); }
        }

        private string _valueTemplate = "{LyTrinh}";
        public string ValueTemplate
        {
            get => _valueTemplate;
            set { _valueTemplate = value; OnPropertyChanged(); }
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
