using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InfraBIM.ManholeTool.Models
{
    /// <summary>
    /// Biểu diễn một ánh xạ tham số Vật liệu trong Family Hố ga sang Vật liệu trong Revit Project
    /// </summary>
    public class ManholeMaterialItem : INotifyPropertyChanged
    {
        private string _paramName = string.Empty;
        private string _selectedMaterialName = string.Empty;
        private string _targetSide = "ALL"; // ALL, HO GA TRAI, HO GA PHAI
        private bool _isType;

        public string ParamName
        {
            get => _paramName;
            set { _paramName = value; OnPropertyChanged(); }
        }

        public string SelectedMaterialName
        {
            get => _selectedMaterialName;
            set { _selectedMaterialName = value; OnPropertyChanged(); }
        }

        public string TargetSide
        {
            get => _targetSide;
            set { _targetSide = value; OnPropertyChanged(); }
        }

        public bool IsType
        {
            get => _isType;
            set { _isType = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
