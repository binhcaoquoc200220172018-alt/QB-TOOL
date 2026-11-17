using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InfraBIM.ManholeTool.Models
{
    /// <summary>
    /// Quản lý thông tin phi hình học, dự án và cấu hình ánh xạ dữ liệu BIM
    /// </summary>
    public class ManholeBimMetadata : INotifyPropertyChanged
    {
        private string _tenCongTrinh = "DỰ ÁN HẠ TẦNG KỸ THUẬT";
        private string _hangMuc = "THOÁT NƯỚC MƯA";
        private string _goiThau = "GÓI THẦU XL-01";
        private string _donViTuVan = "CÔNG TY TƯ VẤN THIẾT KẾ";
        private string _nhaThauThiCong = "CÔNG TY XÂY DỰNG HẠ TẦNG";
        private string _lyTrinhTuyen = "TUYẾN CHÍNH";
        private string _ghiChu = "HỐ GA BÊ TÔNG ĐÚC SẴN / ĐỔ TẠI CHỖ";
        private string _maCauKien = "HG";

        private string _tenHoGaParamName = "Mark";
        private string _viTriParamName = "Comments";
        private string _leftPrefix = "Phía Trái";
        private string _rightPrefix = "Phía Phải";
        private bool _syncSubComponents = true;

        public string TenCongTrinh
        {
            get => _tenCongTrinh;
            set { _tenCongTrinh = value; OnPropertyChanged(); }
        }

        public string HangMuc
        {
            get => _hangMuc;
            set { _hangMuc = value; OnPropertyChanged(); }
        }

        public string GoiThau
        {
            get => _goiThau;
            set { _goiThau = value; OnPropertyChanged(); }
        }

        public string DonViTuVan
        {
            get => _donViTuVan;
            set { _donViTuVan = value; OnPropertyChanged(); }
        }

        public string NhaThauThiCong
        {
            get => _nhaThauThiCong;
            set { _nhaThauThiCong = value; OnPropertyChanged(); }
        }

        public string LyTrinhTuyen
        {
            get => _lyTrinhTuyen;
            set { _lyTrinhTuyen = value; OnPropertyChanged(); }
        }

        public string GhiChu
        {
            get => _ghiChu;
            set { _ghiChu = value; OnPropertyChanged(); }
        }

        public string MaCauKien
        {
            get => _maCauKien;
            set { _maCauKien = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Tên Parameter nhận giá trị TEN HO GA từ Excel (mặc định: Mark)
        /// </summary>
        public string TenHoGaParamName
        {
            get => _tenHoGaParamName;
            set { _tenHoGaParamName = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Tên Parameter nhận phân loại vị trí tuyến (mặc định: Comments)
        /// </summary>
        public string ViTriParamName
        {
            get => _viTriParamName;
            set { _viTriParamName = value; OnPropertyChanged(); }
        }

        public string LeftPrefix
        {
            get => _leftPrefix;
            set { _leftPrefix = value; OnPropertyChanged(); }
        }

        public string RightPrefix
        {
            get => _rightPrefix;
            set { _rightPrefix = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Đồng bộ thông tin phi hình học xuống tất cả các Family bộ phận con (SubComponents)
        /// </summary>
        public bool SyncSubComponents
        {
            get => _syncSubComponents;
            set { _syncSubComponents = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    /// <summary>
    /// Tham số phi hình học tùy biến
    /// </summary>
    public class CustomNonGeoParamItem : INotifyPropertyChanged
    {
        private string _paramName = string.Empty;
        private string _paramValue = string.Empty;
        private bool _isReadOnly;

        public string ParamName
        {
            get => _paramName;
            set { _paramName = value; OnPropertyChanged(); }
        }

        public string ParamValue
        {
            get => _paramValue;
            set { _paramValue = value; OnPropertyChanged(); }
        }

        public bool IsReadOnly
        {
            get => _isReadOnly;
            set { _isReadOnly = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
