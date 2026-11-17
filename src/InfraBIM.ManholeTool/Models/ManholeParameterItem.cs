using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace InfraBIM.ManholeTool.Models
{
    /// <summary>
    /// Biểu diễn một tham số hình học/kích thước trong Family Hố ga để tùy chỉnh theo STT
    /// </summary>
    public class ManholeParameterItem : INotifyPropertyChanged
    {
        private int _stt;
        private string _name = string.Empty;
        private string _value = string.Empty;
        private string _dataType = string.Empty;
        private StorageType _storageType;
        private bool _isType;
        private bool _isReadOnly;
        private string _group = "Dimensions";
        private bool _isSelected;
        private bool _isYesNo;
        private bool _boolValue;

        public int STT
        {
            get => _stt;
            set { _stt = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public string DataType
        {
            get => _dataType;
            set { _dataType = value; OnPropertyChanged(); }
        }

        public StorageType StorageType
        {
            get => _storageType;
            set { _storageType = value; OnPropertyChanged(); }
        }

        public bool IsType
        {
            get => _isType;
            set { _isType = value; OnPropertyChanged(); }
        }

        public bool IsReadOnly
        {
            get => _isReadOnly;
            set { _isReadOnly = value; OnPropertyChanged(); }
        }

        public string Group
        {
            get => _group;
            set { _group = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool IsYesNo
        {
            get => _isYesNo;
            set { _isYesNo = value; OnPropertyChanged(); }
        }

        public bool BoolValue
        {
            get => _boolValue;
            set
            {
                _boolValue = value;
                _value = value ? "1" : "0";
                OnPropertyChanged();
                OnPropertyChanged(nameof(Value));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
