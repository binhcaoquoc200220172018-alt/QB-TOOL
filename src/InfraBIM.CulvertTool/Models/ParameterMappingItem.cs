using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InfraBIM.CulvertTool.Models
{
    /// <summary>
    /// Ánh xạ (mapping) và tùy biến tham số hình học của Family (Tab 02)
    /// Hỗ trợ quét đầy đủ tham số Dimensions (Instance & Type) và tham số nhóm Other
    /// </summary>
    public class ParameterMappingItem : INotifyPropertyChanged
    {
        public static readonly List<string> AvailableMappedFields = new()
        {
            "Tùy biến",
            "KhauDo",
            "ChieuDai",
            "DoDoc",
            "GocXoay",
            "L_Ngam_San",
            "KC_HN1",
            "KC_HN2",
            "KhoangCachTim",
            "X1",
            "Y1",
            "Z1",
            "X2",
            "Y2",
            "Z2"
        };

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        private string _categoryName = string.Empty;
        public string CategoryName
        {
            get => _categoryName;
            set { _categoryName = value; OnPropertyChanged(); }
        }

        private string _familyName = string.Empty;
        public string FamilyName
        {
            get => _familyName;
            set { _familyName = value; OnPropertyChanged(); }
        }

        private string _internalName = string.Empty;
        public string InternalName
        {
            get => _internalName;
            set { _internalName = value; OnPropertyChanged(); }
        }

        private string _displayName = string.Empty;
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }

        private string _groupName = "Dimensions (Kích thước)";
        public string GroupName
        {
            get => _groupName;
            set { _groupName = value; OnPropertyChanged(); }
        }

        private bool _isDimension = true;
        public bool IsDimension
        {
            get => _isDimension;
            set { _isDimension = value; OnPropertyChanged(); }
        }

        private bool _isOther = false;
        public bool IsOther
        {
            get => _isOther;
            set { _isOther = value; OnPropertyChanged(); }
        }

        private string _dataType = "Length";
        public string DataType
        {
            get => _dataType;
            set { _dataType = value; OnPropertyChanged(); }
        }

        private string _defaultValue = string.Empty;
        public string DefaultValue
        {
            get => _defaultValue;
            set { _defaultValue = value; OnPropertyChanged(); }
        }

        private string _customValue = string.Empty;
        public string CustomValue
        {
            get => _customValue;
            set { _customValue = value; OnPropertyChanged(); }
        }

        private string _mappedField = "Tùy biến";
        public string MappedField
        {
            get => _mappedField;
            set { _mappedField = value; OnPropertyChanged(); }
        }

        private bool _isInstance = true;
        public bool IsInstance
        {
            get => _isInstance;
            set { _isInstance = value; OnPropertyChanged(); }
        }

        public string InstanceTypeBadge => IsInstance ? "⚡ Instance (Biến thể)" : "🏷️ Type (Loại)";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
