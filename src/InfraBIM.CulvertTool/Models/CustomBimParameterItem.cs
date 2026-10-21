using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InfraBIM.CulvertTool.Models
{
    /// <summary>
    /// Tham số BIM phi hình học tùy biến động (Tab 04)
    /// Hỗ trợ gán linh hoạt vào nhóm Constraints hoặc nhóm tùy chọn trong Revit
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

        public static readonly List<string> AvailableRevitGroups = new()
        {
            "Constraints",
            "Identity Data",
            "Data",
            "Text",
            "Phasing",
            "Other"
        };

        public static readonly List<string> AvailableSmartTags = new()
        {
            "{STT_DOT}",
            "{STT_DOT:00}",
            "{VI_TRI}",
            "{LyTrinh}",
            "{STT}",
            "{LoaiCong}",
            "{KhauDo}",
            "{CaoDoDau}",
            "{CaoDoCuoi}",
            "{DoDoc}",
            "{ChieuDai}",
            "{SoCua}",
            "{GocXoay}",
            "{VatLieu}",
            "{X1}",
            "{Y1}",
            "{X2}",
            "{Y2}"
        };

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChanged(); } }
        }

        private string _paramName = "Loại công trình";
        public string ParamName
        {
            get => _paramName;
            set { if (_paramName != value) { _paramName = value; OnPropertyChanged(); } }
        }

        private string _revitGroup = "Constraints";
        public string RevitGroup
        {
            get => _revitGroup;
            set { if (_revitGroup != value) { _revitGroup = value; OnPropertyChanged(); } }
        }

        private string _targetScope = "Tất cả cấu kiện";
        public string TargetScope
        {
            get => _targetScope;
            set { if (_targetScope != value) { _targetScope = value; OnPropertyChanged(); } }
        }

        private string _valueTemplate = "{LyTrinh}";
        public string ValueTemplate
        {
            get => _valueTemplate;
            set { if (_valueTemplate != value) { _valueTemplate = value; OnPropertyChanged(); } }
        }

        private string _previewValue = string.Empty;
        public string PreviewValue
        {
            get => _previewValue;
            set { if (_previewValue != value) { _previewValue = value; OnPropertyChanged(); } }
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set { if (_description != value) { _description = value; OnPropertyChanged(); } }
        }

        private string? _selectedSmartTagToInsert;
        public string? SelectedSmartTagToInsert
        {
            get => _selectedSmartTagToInsert;
            set
            {
                _selectedSmartTagToInsert = value;
                OnPropertyChanged();
                if (!string.IsNullOrEmpty(value))
                {
                    // Chèn tag vào ValueTemplate
                    ValueTemplate = string.IsNullOrEmpty(ValueTemplate) ? value : ValueTemplate + value;
                    _selectedSmartTagToInsert = null;
                    OnPropertyChanged(nameof(SelectedSmartTagToInsert));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
