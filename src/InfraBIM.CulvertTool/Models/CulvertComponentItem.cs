using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InfraBIM.CulvertTool.Models
{
    /// <summary>
    /// Đại diện cho một cấu kiện trong cụm lắp ghép linh hoạt (Dynamic Component Assembly)
    /// </summary>
    public class CulvertComponentItem : INotifyPropertyChanged
    {
        public static readonly List<string> AvailableCategories = new()
        {
            "Đốt cống chuẩn",
            "Đốt cống bù / co giãn",
            "Sân cống thượng lưu",
            "Sân cống hạ lưu",
            "Hộp nối / Hố thu",
            "Bê tông lót đốt cống",
            "Đệm cát / Đá dăm",
            "Gối cống / Đệm cống",
            "Cấu kiện phụ khác"
        };

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private string _categoryType = "Đốt cống chuẩn";
        public string CategoryType
        {
            get => _categoryType;
            set { _categoryType = value; OnPropertyChanged(); }
        }

        private FamilySymbolWrapper? _selectedSymbol;
        public FamilySymbolWrapper? SelectedSymbol
        {
            get => _selectedSymbol;
            set { _selectedSymbol = value; OnPropertyChanged(); }
        }

        private double _offsetZ = 0.0;
        public double OffsetZ
        {
            get => _offsetZ;
            set { _offsetZ = value; OnPropertyChanged(); }
        }

        private string _note = string.Empty;
        public string Note
        {
            get => _note;
            set { _note = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
