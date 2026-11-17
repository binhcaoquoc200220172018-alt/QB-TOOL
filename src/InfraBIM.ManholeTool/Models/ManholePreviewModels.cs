using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Media3D;

namespace InfraBIM.ManholeTool.Models
{
    /// <summary>
    /// Chế độ xem trong Viewport 3D Kiểm Tra Mô Hình
    /// </summary>
    public enum ManholePreviewMode
    {
        FullPipeline = 0, // Mặc định: Xem toàn tuyến hố ga + đường ống cống ngầm
        SingleFocus = 1   // Cận cảnh hố ga đang chọn + thước đo cao độ thông minh
    }

    /// <summary>
    /// Đối tượng chứa thông tin kiểm tra hình học và cao độ của 1 hố ga
    /// </summary>
    public class ManholeInspectionItem : INotifyPropertyChanged
    {
        private int _stt;
        private string _tenHoGa = string.Empty;
        private string _side = "HO GA TRAI";
        private double _toaDoX;
        private double _toaDoY;
        private double _cdDinh;
        private double _cdDay;
        private double _cdDayCong;
        private double _gocPv;
        private string _lyTrinh = string.Empty;
        private double _chieuCaoThan;
        private bool _hasWarning;
        private string _warningMessage = string.Empty;
        private double _doDocDenHoSau;
        private double _khoangCachDenHoSau;
        private bool _isSelectedIn3D;

        public int STT
        {
            get => _stt;
            set { _stt = value; OnPropertyChanged(); }
        }

        public string TenHoGa
        {
            get => _tenHoGa;
            set { _tenHoGa = value; OnPropertyChanged(); }
        }

        public string Side
        {
            get => _side;
            set { _side = value; OnPropertyChanged(); }
        }

        public double ToaDoX
        {
            get => _toaDoX;
            set { _toaDoX = value; OnPropertyChanged(); }
        }

        public double ToaDoY
        {
            get => _toaDoY;
            set { _toaDoY = value; OnPropertyChanged(); }
        }

        public double CdDinh
        {
            get => _cdDinh;
            set
            {
                _cdDinh = value;
                UpdateCalculations();
                OnPropertyChanged();
            }
        }

        public double CdDay
        {
            get => _cdDay;
            set
            {
                _cdDay = value;
                UpdateCalculations();
                OnPropertyChanged();
            }
        }

        public double CdDayCong
        {
            get => _cdDayCong;
            set
            {
                _cdDayCong = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChieuSauLangCan));
            }
        }

        public string LyTrinh
        {
            get => _lyTrinh;
            set { _lyTrinh = value; OnPropertyChanged(); }
        }

        public bool IsSelectedIn3D
        {
            get => _isSelectedIn3D;
            set { _isSelectedIn3D = value; OnPropertyChanged(); }
        }

        public double ChieuSauLangCan => _cdDayCong > 0.01 && _cdDay > 0.01 ? Math.Round(_cdDayCong - _cdDay, 3) : 0.0;

        public double GocPv
        {
            get => _gocPv;
            set { _gocPv = value; OnPropertyChanged(); }
        }

        public double ChieuCaoThan
        {
            get => _chieuCaoThan;
            private set { _chieuCaoThan = value; OnPropertyChanged(); }
        }

        public bool HasWarning
        {
            get => _hasWarning;
            private set { _hasWarning = value; OnPropertyChanged(); }
        }

        public string WarningMessage
        {
            get => _warningMessage;
            private set { _warningMessage = value; OnPropertyChanged(); }
        }

        public double DoDocDenHoSau
        {
            get => _doDocDenHoSau;
            set { _doDocDenHoSau = value; OnPropertyChanged(); }
        }

        public double KhoangCachDenHoSau
        {
            get => _khoangCachDenHoSau;
            set { _khoangCachDenHoSau = value; OnPropertyChanged(); }
        }

        public void UpdateCalculations()
        {
            ChieuCaoThan = Math.Round(_cdDinh - _cdDay, 4);
            if (_cdDinh <= _cdDay)
            {
                HasWarning = true;
                WarningMessage = "CẢNH BÁO: CĐ ĐỈNH <= CĐ ĐÁY! (Lỗi cao độ)";
            }
            else if (ChieuCaoThan < 0.5)
            {
                HasWarning = true;
                WarningMessage = "LƯU Ý: Chiều cao hố ga < 0.5m (quá nông)";
            }
            else
            {
                HasWarning = false;
                WarningMessage = "HỢP LỆ (Cao độ chuẩn xác)";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    /// <summary>
    /// Dữ liệu Mesh 3D hình học thực tế trích xuất từ Revit Family
    /// </summary>
    public class ExtractedManholeMeshGroup
    {
        public string FamilyName { get; set; } = string.Empty;
        public string SymbolName { get; set; } = string.Empty;
        public List<(MeshGeometry3D Mesh, System.Windows.Media.Brush Brush, string CategoryName)> Parts { get; } = new();
        public Rect3D Bounds { get; set; }
    }
}
