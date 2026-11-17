using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InfraBIM.ManholeTool.Models
{
    /// <summary>
    /// Dữ liệu biểu diễn một hố ga đọc từ Excel (7 cột tiêu chuẩn)
    /// </summary>
    public class ManholeRowData : INotifyPropertyChanged
    {
        private int _stt;
        private string _tenHoGa = string.Empty;
        private double _toaDoX;
        private double _toaDoY;
        private double _cdDinh;
        private double _cdDay;
        private double _cdDayCong;
        private double _gocPv;
        private string _lyTrinh = string.Empty;
        private string _side = "HO GA TRAI";
        private bool _isSelected = true;

        /// <summary>
        /// Số thứ tự hố ga (Cột 1)
        /// </summary>
        public int STT
        {
            get => _stt;
            set { _stt = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Tên định danh hố ga (Cột 2: GT-1, GP-1...)
        /// </summary>
        public string TenHoGa
        {
            get => _tenHoGa;
            set { _tenHoGa = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Tọa độ X (Cột 3, đơn vị: Mét)
        /// </summary>
        public double ToaDoX
        {
            get => _toaDoX;
            set { _toaDoX = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Tọa độ Y (Cột 4, đơn vị: Mét)
        /// </summary>
        public double ToaDoY
        {
            get => _toaDoY;
            set { _toaDoY = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Cao độ đỉnh hố ga (Cột 5, đơn vị: Mét)
        /// </summary>
        public double CdDinh
        {
            get => _cdDinh;
            set
            {
                _cdDinh = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChieuCaoThan));
                OnPropertyChanged(nameof(HasWarning));
                OnPropertyChanged(nameof(WarningMessage));
            }
        }

        /// <summary>
        /// Cao độ đáy hố ga (Cột 6, đơn vị: Mét)
        /// </summary>
        public double CdDay
        {
            get => _cdDay;
            set
            {
                _cdDay = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChieuCaoThan));
                OnPropertyChanged(nameof(HasWarning));
                OnPropertyChanged(nameof(WarningMessage));
            }
        }

        /// <summary>
        /// Cao độ đáy cống ngang (C - nếu có trong bảng dữ liệu)
        /// </summary>
        public double CdDayCong
        {
            get => _cdDayCong;
            set { _cdDayCong = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Lý trình của hố ga (nếu có, ví dụ: Km0+120.00)
        /// </summary>
        public string LyTrinh
        {
            get => _lyTrinh;
            set { _lyTrinh = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Góc phương vị / góc xoay trên mặt bằng (Cột 7, đơn vị: Độ - Degrees)
        /// </summary>
        public double GocPv
        {
            get => _gocPv;
            set { _gocPv = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Nhánh / Bên tuyến: "HO GA TRAI" hoặc "HO GA PHAI"
        /// </summary>
        public string Side
        {
            get => _side;
            set { _side = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Trạng thái chọn để rải
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Chiều cao thân tự động tính = CD DINH - CD DAY (Mét)
        /// </summary>
        public double ChieuCaoThan => Math.Round(_cdDinh - _cdDay, 4);

        /// <summary>
        /// Kiểm tra cảnh báo nếu CD DINH <= CD DAY
        /// </summary>
        public bool HasWarning => _cdDinh <= _cdDay;

        /// <summary>
        /// Thông điệp cảnh báo hiển thị trên giao diện
        /// </summary>
        public string WarningMessage => HasWarning ? "CẢNH BÁO: Cao độ đỉnh <= Cao độ đáy!" : "Hợp lệ";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
