using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InfraBIM.ManholeTool.Models
{
    /// <summary>
    /// Hiển thị bộ tham số đã được gán cho một STT cụ thể trên bảng tổng hợp
    /// </summary>
    public class SttParamDisplayRow : INotifyPropertyChanged
    {
        private string _side = "HO GA TRAI";
        private int _stt;
        private string _tenHoGa = string.Empty;
        private string _paramSummary = string.Empty;
        private Dictionary<string, string> _customParams = new(StringComparer.OrdinalIgnoreCase);

        public string Side
        {
            get => _side;
            set { _side = value; OnPropertyChanged(); }
        }

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

        public string ParamSummary
        {
            get => _paramSummary;
            set { _paramSummary = value; OnPropertyChanged(); }
        }

        public Dictionary<string, string> CustomParams
        {
            get => _customParams;
            set
            {
                _customParams = value;
                UpdateSummary();
                OnPropertyChanged();
            }
        }

        public void UpdateSummary()
        {
            if (_customParams == null || _customParams.Count == 0)
            {
                ParamSummary = "Mặc định theo Type/Symbol";
                return;
            }

            var parts = new List<string>();
            foreach (var kvp in _customParams)
            {
                parts.Add($"{kvp.Key}={kvp.Value}");
            }
            ParamSummary = string.Join("; ", parts);
            OnPropertyChanged(nameof(ParamSummary));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    /// <summary>
    /// Cấu hình lưu trữ để xuất/nhập file JSON
    /// </summary>
    public class ManholeConfigProfile
    {
        public string ProfileName { get; set; } = "Cấu hình Hố Ga Mặc Định";
        public string SelectedFamilyName { get; set; } = string.Empty;
        public string SelectedSymbolName { get; set; } = string.Empty;
        public string SelectedLevelName { get; set; } = string.Empty;
        public bool PlaceByCdDinh { get; set; } = true;
        public bool UseSurveyPoint { get; set; } = true;

        public string HeightParamName { get; set; } = string.Empty;
        public double HeightOffsetM { get; set; } = 0.0;

        public Dictionary<string, string> MaterialMappings { get; set; } = new();
        public ManholeBimMetadata BimMetadata { get; set; } = new();
        public Dictionary<string, Dictionary<string, string>> SttOverrides { get; set; } = new(); // Key: "TRAI_1", "PHAI_2" -> (ParamName -> Value)
    }
}
