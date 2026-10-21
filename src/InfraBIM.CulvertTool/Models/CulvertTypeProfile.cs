using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InfraBIM.CulvertTool.Models
{
    /// <summary>
    /// Đại diện cho một Hồ sơ loại cống độc lập (hỗ trợ động N loại cống đổ tại chỗ / đúc sẵn)
    /// Quản lý riêng: Tên loại cống, Chiều dài đốt chuẩn L_Std, Khe co giãn, Chế độ rải, và Bộ cấu kiện Family
    /// </summary>
    public class CulvertTypeProfile : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        private string _typeName = "Cống đổ tại chỗ 1";
        public string TypeName
        {
            get => _typeName;
            set
            {
                if (_typeName != value)
                {
                    _typeName = value;
                    OnPropertyChanged();
                    UpdateComponentProfileNames();
                }
            }
        }

        private string _culvertKind = "CastInPlace"; // CastInPlace, Precast, Technical
        public string CulvertKind
        {
            get => _culvertKind;
            set { _culvertKind = value; OnPropertyChanged(); }
        }

        private double _lStd = 4.0;
        public double L_Std
        {
            get => _lStd;
            set
            {
                if (Math.Abs(_lStd - value) > 0.0001)
                {
                    _lStd = value;
                    _lStdText = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(L_Std_Text));
                }
            }
        }

        private string _lStdText = "4.00";
        public string L_Std_Text
        {
            get => _lStdText;
            set
            {
                if (_lStdText != value)
                {
                    _lStdText = value;
                    OnPropertyChanged();
                    if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v) && v > 0)
                    {
                        _lStd = v;
                        OnPropertyChanged(nameof(L_Std));
                    }
                }
            }
        }

        private double _kheHo = 0.02;
        public double Khe_Ho
        {
            get => _kheHo;
            set
            {
                if (Math.Abs(_kheHo - value) > 0.0001)
                {
                    _kheHo = value;
                    _kheHoText = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Khe_Ho_Text));
                }
            }
        }

        private string _kheHoText = "0.02";
        public string Khe_Ho_Text
        {
            get => _kheHoText;
            set
            {
                if (_kheHoText != value)
                {
                    _kheHoText = value;
                    OnPropertyChanged();
                    if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v) && v >= 0)
                    {
                        _kheHo = v;
                        OnPropertyChanged(nameof(Khe_Ho));
                    }
                }
            }
        }

        private int _selectedArrayModeIndex = 0; // 0: CenterOut, 1: OneWay
        public int SelectedArrayModeIndex
        {
            get => _selectedArrayModeIndex;
            set { _selectedArrayModeIndex = value; OnPropertyChanged(); }
        }

        public CulvertTypeProfile()
        {
        }

        public CulvertTypeProfile(string typeName, double lStd = 4.0, double kheHo = 0.02, int arrayModeIndex = 0)
        {
            _typeName = typeName;
            _lStd = lStd;
            _lStdText = lStd.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            _kheHo = kheHo;
            _kheHoText = kheHo.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            _selectedArrayModeIndex = arrayModeIndex;
        }

        public ObservableCollection<CulvertComponentItem> Components { get; } = new();

        public void UpdateComponentProfileNames()
        {
            foreach (var comp in Components)
            {
                comp.ProfileName = _typeName;
                comp.GroupType = _typeName;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public override string ToString() => TypeName;
    }
}
