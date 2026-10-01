using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InfraBIM.CulvertTool.Models
{
    /// <summary>
    /// Lưu trữ thiết lập vật liệu, tên vật liệu và màu sắc đồ họa cho từng cấu kiện cống
    /// </summary>
    public class CulvertMaterialItem : INotifyPropertyChanged
    {
        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private string _categoryType = string.Empty;
        public string CategoryType
        {
            get => _categoryType;
            set { _categoryType = value; OnPropertyChanged(); }
        }

        private string _familyDisplayName = string.Empty;
        public string FamilyDisplayName
        {
            get => _familyDisplayName;
            set { _familyDisplayName = value; OnPropertyChanged(); }
        }

        private string _materialName = string.Empty;
        public string MaterialName
        {
            get => _materialName;
            set { _materialName = value; OnPropertyChanged(); }
        }

        private string _colorHex = "#BDC3C7";
        public string ColorHex
        {
            get => _colorHex;
            set
            {
                _colorHex = value;
                OnPropertyChanged();
                UpdateRgbFromHex(value);
            }
        }

        private byte _colorR = 189;
        public byte ColorR
        {
            get => _colorR;
            set { _colorR = value; OnPropertyChanged(); UpdateHexFromRgb(); }
        }

        private byte _colorG = 195;
        public byte ColorG
        {
            get => _colorG;
            set { _colorG = value; OnPropertyChanged(); UpdateHexFromRgb(); }
        }

        private byte _colorB = 199;
        public byte ColorB
        {
            get => _colorB;
            set { _colorB = value; OnPropertyChanged(); UpdateHexFromRgb(); }
        }

        private double _transparency = 0.0;
        public double Transparency
        {
            get => _transparency;
            set { _transparency = value; OnPropertyChanged(); }
        }

        private string _materialParamName = "Material";
        public string MaterialParamName
        {
            get => _materialParamName;
            set { _materialParamName = value; OnPropertyChanged(); }
        }

        private string _statusNote = "Chưa áp dụng";
        public string StatusNote
        {
            get => _statusNote;
            set { _statusNote = value; OnPropertyChanged(); }
        }

        public System.Windows.Media.Brush ColorBrush
        {
            get
            {
                var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(_colorR, _colorG, _colorB));
                brush.Freeze();
                return brush;
            }
        }

        public void SetColorRgb(byte r, byte g, byte b)
        {
            _colorR = r;
            _colorG = g;
            _colorB = b;
            _colorHex = $"#{r:X2}{g:X2}{b:X2}";
            OnPropertyChanged(nameof(ColorR));
            OnPropertyChanged(nameof(ColorG));
            OnPropertyChanged(nameof(ColorB));
            OnPropertyChanged(nameof(ColorHex));
            OnPropertyChanged(nameof(ColorBrush));
        }

        private void UpdateRgbFromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return;
            string clean = hex.Trim().TrimStart('#');
            if (clean.Length == 6)
            {
                if (byte.TryParse(clean.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
                    byte.TryParse(clean.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
                    byte.TryParse(clean.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
                {
                    _colorR = r;
                    _colorG = g;
                    _colorB = b;
                    OnPropertyChanged(nameof(ColorR));
                    OnPropertyChanged(nameof(ColorG));
                    OnPropertyChanged(nameof(ColorB));
                    OnPropertyChanged(nameof(ColorBrush));
                }
            }
        }

        private void UpdateHexFromRgb()
        {
            _colorHex = $"#{_colorR:X2}{_colorG:X2}{_colorB:X2}";
            OnPropertyChanged(nameof(ColorHex));
            OnPropertyChanged(nameof(ColorBrush));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
