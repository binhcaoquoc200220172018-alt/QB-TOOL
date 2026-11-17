using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using InfraBIM.ManholeTool.Models;
using InfraBIM.ManholeTool.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace InfraBIM.ManholeTool.ViewModels
{
    public class ManholeMainViewModel : ViewModelBase
    {
        private readonly UIApplication _uiApp;
        private readonly ExternalEvent _externalEvent;
        private readonly RevitExternalEventHandler _eventHandler;

        public Document Doc => _uiApp.ActiveUIDocument.Document;

        #region TAB 1: File Excel & Nạp Family

        private string _excelFilePath = string.Empty;
        public string ExcelFilePath
        {
            get => _excelFilePath;
            set { SetField(ref _excelFilePath, value); }
        }

        private bool _runHoGaTrai = true;
        public bool RunHoGaTrai
        {
            get => _runHoGaTrai;
            set { SetField(ref _runHoGaTrai, value); RefreshDisplayList(); }
        }

        private bool _runHoGaPhai = true;
        public bool RunHoGaPhai
        {
            get => _runHoGaPhai;
            set { SetField(ref _runHoGaPhai, value); RefreshDisplayList(); }
        }

        private int _selectedReviewFilterIndex = 0; // 0: Tất cả, 1: Chỉ Hố Ga Trái, 2: Chỉ Hố Ga Phải
        public int SelectedReviewFilterIndex
        {
            get => _selectedReviewFilterIndex;
            set
            {
                if (SetField(ref _selectedReviewFilterIndex, value))
                {
                    RefreshDisplayList();
                    OnPropertyChanged(nameof(IsReviewFilterAll));
                    OnPropertyChanged(nameof(IsReviewFilterTrai));
                    OnPropertyChanged(nameof(IsReviewFilterPhai));
                }
            }
        }

        public bool IsReviewFilterAll => _selectedReviewFilterIndex == 0;
        public bool IsReviewFilterTrai => _selectedReviewFilterIndex == 1;
        public bool IsReviewFilterPhai => _selectedReviewFilterIndex == 2;

        public ObservableCollection<ManholeRowData> HoGaTraiList { get; } = new();
        public ObservableCollection<ManholeRowData> HoGaPhaiList { get; } = new();
        public ObservableCollection<ManholeRowData> DisplayManholeList { get; } = new();

        private string _familyFilePath = string.Empty;
        public string FamilyFilePath
        {
            get => _familyFilePath;
            set { SetField(ref _familyFilePath, value); }
        }

        public ObservableCollection<string> AvailableFamilies { get; } = new();
        private string _selectedFamilyName = string.Empty;
        public string SelectedFamilyName
        {
            get => _selectedFamilyName;
            set
            {
                if (SetField(ref _selectedFamilyName, value))
                {
                    OnFamilyChanged();
                }
            }
        }

        public ObservableCollection<FamilySymbol> AvailableSymbols { get; } = new();
        private FamilySymbol? _selectedSymbol;
        public FamilySymbol? SelectedSymbol
        {
            get => _selectedSymbol;
            set
            {
                if (SetField(ref _selectedSymbol, value))
                {
                    OnSymbolChanged();
                }
            }
        }

        public ObservableCollection<Level> AvailableLevels { get; } = new();
        private Level? _selectedLevel;
        public Level? SelectedLevel
        {
            get => _selectedLevel;
            set { SetField(ref _selectedLevel, value); }
        }

        private bool _placeByCdDinh = true;
        public bool PlaceByCdDinh
        {
            get => _placeByCdDinh;
            set { SetField(ref _placeByCdDinh, value); }
        }

        private bool _placeByCdDay;
        public bool PlaceByCdDay
        {
            get => _placeByCdDay;
            set
            {
                if (SetField(ref _placeByCdDay, value))
                {
                    _placeByCdDinh = !value;
                    OnPropertyChanged(nameof(PlaceByCdDinh));
                }
            }
        }

        private bool _useSurveyPoint = true;
        public bool UseSurveyPoint
        {
            get => _useSurveyPoint;
            set { SetField(ref _useSurveyPoint, value); }
        }

        #endregion

        #region TAB 2: Biến Hình Học Theo STT

        public ObservableCollection<string> AvailableHeightParams { get; } = new();
        private string _selectedHeightParam = string.Empty;
        public string SelectedHeightParam
        {
            get => _selectedHeightParam;
            set { SetField(ref _selectedHeightParam, value); }
        }

        private double _heightOffsetM = 0.0;
        public double HeightOffsetM
        {
            get => _heightOffsetM;
            set { SetField(ref _heightOffsetM, value); }
        }

        public ObservableCollection<ManholeParameterItem> GeoParameters { get; } = new();

        private string _sttTargetSide = "Cả hai";
        public string SttTargetSide
        {
            get => _sttTargetSide;
            set { SetField(ref _sttTargetSide, value); }
        }

        private string _sttRangeText = string.Empty;
        public string SttRangeText
        {
            get => _sttRangeText;
            set { SetField(ref _sttRangeText, value); }
        }

        public ObservableCollection<SttParamDisplayRow> SttDisplayRows { get; } = new();

        // Dictionary lưu trữ giá trị override theo Key ("TRAI_1", "PHAI_2", "ALL_3") -> (ParamName -> Value)
        public Dictionary<string, Dictionary<string, string>> SttOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region TAB 3: KIỂM TRA MÔ HÌNH (PREVIEW HÌNH HỌC & CAO ĐỘ ĐỈNH / ĐÁY)

        private string _previewSheetName = "HO GA TRAI";
        public string PreviewSheetName
        {
            get => _previewSheetName;
            set
            {
                if (SetField(ref _previewSheetName, value))
                {
                    RefreshPreviewList();
                }
            }
        }

        public ObservableCollection<ManholeInspectionItem> PreviewInspectionList { get; } = new();

        private ManholeInspectionItem? _selectedInspectionItem;
        public ManholeInspectionItem? SelectedInspectionItem
        {
            get => _selectedInspectionItem;
            set
            {
                if (SetField(ref _selectedInspectionItem, value))
                {
                    OnSelectedInspectionChanged();
                }
            }
        }

        private ExtractedManholeMeshGroup? _extractedRealGeometry;
        public ExtractedManholeMeshGroup? ExtractedRealGeometry
        {
            get => _extractedRealGeometry;
            set { SetField(ref _extractedRealGeometry, value); }
        }

        private bool _isRealGeometryLoaded;
        public bool IsRealGeometryLoaded
        {
            get => _isRealGeometryLoaded;
            set { SetField(ref _isRealGeometryLoaded, value); }
        }

        private ManholePreviewMode _previewViewMode = ManholePreviewMode.FullPipeline;
        public ManholePreviewMode PreviewViewMode
        {
            get => _previewViewMode;
            set { SetField(ref _previewViewMode, value); }
        }

        #endregion

        #region TAB 4: Gán Vật Liệu

        public ObservableCollection<ManholeMaterialItem> MaterialMappings { get; } = new();
        public ObservableCollection<string> AvailableProjectMaterials { get; } = new();

        #endregion

        #region TAB 5: Thông Tin Phi Hình Học & Metadata

        public ManholeBimMetadata BimMetadata { get; } = new();
        public ObservableCollection<string> AvailableTextParams { get; } = new();
        public ObservableCollection<CustomNonGeoParamItem> CustomNonGeoParams { get; } = new();

        #endregion

        #region Bottom Bar & Execution Status

        private bool _isExecuting;
        public bool IsExecuting
        {
            get => _isExecuting;
            set { SetField(ref _isExecuting, value); }
        }

        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set { SetField(ref _progressValue, value); }
        }

        private int _progressMax = 100;
        public int ProgressMax
        {
            get => _progressMax;
            set { SetField(ref _progressMax, value); }
        }

        private string _statusMessage = "Sẵn sàng";
        public string StatusMessage
        {
            get => _statusMessage;
            set { SetField(ref _statusMessage, value); }
        }

        #endregion

        #region Commands

        public RelayCommand BrowseExcelCommand { get; }
        public RelayCommand ExportSampleExcelCommand { get; }
        public RelayCommand BrowseFamilyCommand { get; }
        public RelayCommand LoadFamilyCommand { get; }
        public RelayCommand ApplySttCommand { get; }
        public RelayCommand ClearSttOverridesCommand { get; }
        public RelayCommand AutoMatchMaterialsCommand { get; }
        public RelayCommand SaveConfigCommand { get; }
        public RelayCommand LoadConfigCommand { get; }
        public RelayCommand ExecuteCommand { get; }
        public RelayCommand ExtractGeometryCommand { get; }
        public RelayCommand SwitchToFullPipelineCommand { get; }
        public RelayCommand SwitchToSingleFocusCommand { get; }
        public RelayCommand SelectPrevInspectionCommand { get; }
        public RelayCommand SelectNextInspectionCommand { get; }
        public RelayCommand SetReviewFilterAllCommand { get; }
        public RelayCommand SetReviewFilterTraiCommand { get; }
        public RelayCommand SetReviewFilterPhaiCommand { get; }

        #endregion

        public ManholeMainViewModel(UIApplication uiApp, ExternalEvent externalEvent, RevitExternalEventHandler eventHandler)
        {
            _uiApp = uiApp;
            _externalEvent = externalEvent;
            _eventHandler = eventHandler;

            BrowseExcelCommand = new RelayCommand(OnBrowseExcel);
            ExportSampleExcelCommand = new RelayCommand(OnExportSampleExcel);
            BrowseFamilyCommand = new RelayCommand(OnBrowseFamily);
            LoadFamilyCommand = new RelayCommand(OnLoadFamily);
            ApplySttCommand = new RelayCommand(OnApplyStt);
            ClearSttOverridesCommand = new RelayCommand(OnClearSttOverrides);
            AutoMatchMaterialsCommand = new RelayCommand(OnAutoMatchMaterials);
            SaveConfigCommand = new RelayCommand(OnSaveConfig);
            LoadConfigCommand = new RelayCommand(OnLoadConfig);
            ExecuteCommand = new RelayCommand(OnExecute);
            ExtractGeometryCommand = new RelayCommand(OnExtractGeometry);
            SwitchToFullPipelineCommand = new RelayCommand(_ => PreviewViewMode = ManholePreviewMode.FullPipeline);
            SwitchToSingleFocusCommand = new RelayCommand(_ => PreviewViewMode = ManholePreviewMode.SingleFocus);
            SelectPrevInspectionCommand = new RelayCommand(_ => OnSelectPrevInspection());
            SelectNextInspectionCommand = new RelayCommand(_ => OnSelectNextInspection());
            SetReviewFilterAllCommand = new RelayCommand(_ => SelectedReviewFilterIndex = 0);
            SetReviewFilterTraiCommand = new RelayCommand(_ => SelectedReviewFilterIndex = 1);
            SetReviewFilterPhaiCommand = new RelayCommand(_ => SelectedReviewFilterIndex = 2);

            // Tải dữ liệu ban đầu từ Revit Document
            LoadInitialRevitData();
        }

        private void LoadInitialRevitData()
        {
            try
            {
                // 1. Tải danh sách Level
                AvailableLevels.Clear();
                foreach (var lvl in ManholeFamilyScanner.GetAllLevels(Doc))
                {
                    AvailableLevels.Add(lvl);
                }
                SelectedLevel = AvailableLevels.FirstOrDefault();

                // 2. Tải danh sách Material
                AvailableProjectMaterials.Clear();
                foreach (var mat in ManholeFamilyScanner.GetAllProjectMaterials(Doc))
                {
                    AvailableProjectMaterials.Add(mat);
                }

                // 3. Tải danh sách Family có sẵn trong Project
                RefreshAvailableFamilies();
            }
            catch { }
        }

        private void RefreshAvailableFamilies()
        {
            AvailableFamilies.Clear();
            var symbols = ManholeFamilyScanner.GetAvailableManholeSymbols(Doc);
            var famNames = symbols.Select(s => s.FamilyName).Distinct().OrderBy(n => n);
            foreach (var fn in famNames)
            {
                AvailableFamilies.Add(fn);
            }

            if (AvailableFamilies.Count > 0)
            {
                SelectedFamilyName = AvailableFamilies[0];
            }
        }

        private void OnFamilyChanged()
        {
            AvailableSymbols.Clear();
            if (string.IsNullOrWhiteSpace(SelectedFamilyName)) return;

            var symbols = ManholeFamilyScanner.GetAvailableManholeSymbols(Doc)
                .Where(s => string.Equals(s.FamilyName, SelectedFamilyName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Name);

            foreach (var s in symbols)
            {
                AvailableSymbols.Add(s);
            }

            SelectedSymbol = AvailableSymbols.FirstOrDefault();
        }

        private void OnSymbolChanged()
        {
            GeoParameters.Clear();
            MaterialMappings.Clear();
            CustomNonGeoParams.Clear();
            AvailableHeightParams.Clear();
            AvailableTextParams.Clear();

            if (SelectedSymbol == null) return;

            var (geo, mat, nonGeo) = ManholeFamilyScanner.ScanFamilyParameters(Doc, SelectedSymbol);

            // Tab 2: Biến hình học
            foreach (var g in geo)
            {
                GeoParameters.Add(g);
                AvailableHeightParams.Add(g.Name);
            }

            // Tự động tìm biến Chiều cao thân mặc định
            SelectedHeightParam = AvailableHeightParams.FirstOrDefault(p =>
                p.ToUpperInvariant().Contains("CHIEU CAO") ||
                p.ToUpperInvariant().Contains("CHIỀU CAO") ||
                p.ToUpperInvariant().Contains("HEIGHT") ||
                p.ToUpperInvariant().Contains("H_THAN") ||
                p == "H" || p == "h") ?? AvailableHeightParams.FirstOrDefault() ?? string.Empty;

            // Tab 3: Biến vật liệu
            foreach (var m in mat)
            {
                MaterialMappings.Add(m);
            }
            OnAutoMatchMaterials();

            // Tab 4: Biến văn bản & Metadata
            AvailableTextParams.Add("Mark");
            AvailableTextParams.Add("Comments");
            foreach (var ng in nonGeo)
            {
                CustomNonGeoParams.Add(ng);
                if (!AvailableTextParams.Contains(ng.ParamName))
                {
                    AvailableTextParams.Add(ng.ParamName);
                }
            }

            // Mặc định chọn biến nhận Tên hố ga và Vị trí
            if (AvailableTextParams.Contains("Mark")) BimMetadata.TenHoGaParamName = "Mark";
            if (AvailableTextParams.Contains("Comments")) BimMetadata.ViTriParamName = "Comments";
        }

        private void OnBrowseExcel()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All Files (*.*)|*.*",
                Title = "Chọn File Excel Bảng Dữ Liệu Hố Ga"
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                ExcelFilePath = dlg.FileName;
                LoadExcelData();
            }
        }

        private void LoadExcelData()
        {
            if (!File.Exists(ExcelFilePath)) return;

            try
            {
                StatusMessage = "Đang đọc file Excel...";
                var (trai, phai) = ManholeExcelService.ReadManholeExcel(ExcelFilePath);

                HoGaTraiList.Clear();
                foreach (var item in trai) HoGaTraiList.Add(item);

                HoGaPhaiList.Clear();
                foreach (var item in phai) HoGaPhaiList.Add(item);

                RefreshDisplayList();
                RebuildSttDisplayRows();
                RefreshPreviewList();

                StatusMessage = $"Đã tải {HoGaTraiList.Count} hố ga trái, {HoGaPhaiList.Count} hố ga phải.";
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi Đọc Excel", ex.Message);
                StatusMessage = "Lỗi khi đọc file Excel.";
            }
        }

        public void RefreshDisplayList()
        {
            DisplayManholeList.Clear();
            bool showTrai = (_selectedReviewFilterIndex == 0 && RunHoGaTrai) || _selectedReviewFilterIndex == 1;
            bool showPhai = (_selectedReviewFilterIndex == 0 && RunHoGaPhai) || _selectedReviewFilterIndex == 2;

            if (showTrai)
            {
                foreach (var r in HoGaTraiList) DisplayManholeList.Add(r);
            }
            if (showPhai)
            {
                foreach (var r in HoGaPhaiList) DisplayManholeList.Add(r);
            }
        }

        private void RebuildSttDisplayRows()
        {
            SttDisplayRows.Clear();
            foreach (var r in HoGaTraiList)
            {
                var row = new SttParamDisplayRow { Side = "HO GA TRAI", STT = r.STT, TenHoGa = r.TenHoGa };
                string key = $"TRAI_{r.STT}";
                if (SttOverrides.TryGetValue(key, out var dict))
                {
                    row.CustomParams = dict;
                }
                else
                {
                    row.UpdateSummary();
                }
                SttDisplayRows.Add(row);
            }

            foreach (var r in HoGaPhaiList)
            {
                var row = new SttParamDisplayRow { Side = "HO GA PHAI", STT = r.STT, TenHoGa = r.TenHoGa };
                string key = $"PHAI_{r.STT}";
                if (SttOverrides.TryGetValue(key, out var dict))
                {
                    row.CustomParams = dict;
                }
                else
                {
                    row.UpdateSummary();
                }
                SttDisplayRows.Add(row);
            }
        }

        private void OnExportSampleExcel()
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "Bang_Du_Lieu_Ho_Ga_Mau.xlsx",
                Title = "Xuất File Excel Mẫu Hố Ga 7 Cột"
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ManholeExcelService.ExportSampleExcel(dlg.FileName);
                    TaskDialog.Show("Xuất Mẫu Thành Công", $"Đã tạo file Excel mẫu chuẩn tại:\n{dlg.FileName}");
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Lỗi Xuất File", ex.Message);
                }
            }
        }

        private void OnBrowseFamily()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Revit Family (*.rfa)|*.rfa|All Files (*.*)|*.*",
                Title = "Chọn File Family Hố Ga (.rfa)"
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                FamilyFilePath = dlg.FileName;
                OnLoadFamily();
            }
        }

        private void OnLoadFamily()
        {
            if (!File.Exists(FamilyFilePath)) return;

            _eventHandler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                using var t = new Transaction(doc, "Nạp Family Hố Ga từ đường dẫn");
                t.Start();

                bool ok = doc.LoadFamily(FamilyFilePath, new CustomFamilyLoadOptions(), out Family loadedFamily);
                t.Commit();

                if (ok && loadedFamily != null)
                {
                    RefreshAvailableFamilies();
                    SelectedFamilyName = loadedFamily.Name;
                    StatusMessage = $"Đã nạp Family '{loadedFamily.Name}' thành công.";
                }
                else
                {
                    // Có thể Family đã có sẵn trong dự án với cùng tên
                    string famName = Path.GetFileNameWithoutExtension(FamilyFilePath);
                    RefreshAvailableFamilies();
                    SelectedFamilyName = famName;
                    StatusMessage = $"Family '{famName}' đã tồn tại trong dự án.";
                }
            });

            _externalEvent.Raise();
        }

        private void OnApplyStt()
        {
            if (string.IsNullOrWhiteSpace(SttRangeText))
            {
                TaskDialog.Show("Thông báo", "Vui lòng nhập dải số thứ tự STT (Ví dụ: 1-15, 20, 25-40).");
                return;
            }

            var sttList = ParseSttRange(SttRangeText);
            if (sttList.Count == 0)
            {
                TaskDialog.Show("Lỗi Cú Pháp", "Không thể nhận diện dải số thứ tự. Vui lòng kiểm tra lại định dạng.");
                return;
            }

            // Thu thập các giá trị tham số đang chỉnh sửa trên giao diện Tab 2
            var currentParamValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in GeoParameters)
            {
                if (!string.IsNullOrWhiteSpace(p.Value))
                {
                    currentParamValues[p.Name] = p.Value;
                }
            }

            if (currentParamValues.Count == 0)
            {
                TaskDialog.Show("Thông báo", "Chưa có giá trị tham số nào được nhập để áp dụng.");
                return;
            }

            // Áp dụng vào SttOverrides
            foreach (int stt in sttList)
            {
                if (SttTargetSide == "HO GA TRAI" || SttTargetSide == "Cả hai")
                {
                    string key = $"TRAI_{stt}";
                    SttOverrides[key] = new Dictionary<string, string>(currentParamValues, StringComparer.OrdinalIgnoreCase);
                }
                if (SttTargetSide == "HO GA PHAI" || SttTargetSide == "Cả hai")
                {
                    string key = $"PHAI_{stt}";
                    SttOverrides[key] = new Dictionary<string, string>(currentParamValues, StringComparer.OrdinalIgnoreCase);
                }
            }

            RebuildSttDisplayRows();
            StatusMessage = $"Đã áp dụng thông số cho {sttList.Count} STT ({SttTargetSide}).";
        }

        private void OnClearSttOverrides()
        {
            SttOverrides.Clear();
            RebuildSttDisplayRows();
            StatusMessage = "Đã xóa toàn bộ cấu hình riêng theo STT.";
        }

        private List<int> ParseSttRange(string input)
        {
            var result = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(input)) return result.ToList();

            string[] tokens = input.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                string t = token.Trim();
                if (t.Contains("-"))
                {
                    string[] parts = t.Split('-');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
                    {
                        for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                        {
                            result.Add(i);
                        }
                    }
                }
                else if (int.TryParse(t, out int single))
                {
                    result.Add(single);
                }
            }

            return result.OrderBy(x => x).ToList();
        }

        private void OnAutoMatchMaterials()
        {
            foreach (var item in MaterialMappings)
            {
                string pUpper = item.ParamName.ToUpperInvariant();
                string? bestMatch = AvailableProjectMaterials.FirstOrDefault(m =>
                {
                    string mUpper = m.ToUpperInvariant();
                    if (pUpper.Contains("CONCRETE") || pUpper.Contains("BE TONG") || pUpper.Contains("BÊ TÔNG"))
                    {
                        return mUpper.Contains("BÊ TÔNG") || mUpper.Contains("CONCRETE");
                    }
                    if (pUpper.Contains("STEEL") || pUpper.Contains("THEP") || pUpper.Contains("THÉP") || pUpper.Contains("GANG"))
                    {
                        return mUpper.Contains("GANG") || mUpper.Contains("THÉP") || mUpper.Contains("STEEL");
                    }
                    return false;
                });

                if (bestMatch != null)
                {
                    item.SelectedMaterialName = bestMatch;
                }
                else if (AvailableProjectMaterials.Count > 0 && string.IsNullOrWhiteSpace(item.SelectedMaterialName))
                {
                    item.SelectedMaterialName = AvailableProjectMaterials[0];
                }
            }
        }

        private void OnSaveConfig()
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "JSON Profile (*.json)|*.json",
                FileName = "Cau_Hinh_Ho_Ga.json",
                Title = "Lưu Cấu Hình Hố Ga"
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var profile = new ManholeConfigProfile
                    {
                        SelectedFamilyName = SelectedFamilyName,
                        SelectedSymbolName = SelectedSymbol?.Name ?? "",
                        SelectedLevelName = SelectedLevel?.Name ?? "",
                        PlaceByCdDinh = PlaceByCdDinh,
                        UseSurveyPoint = UseSurveyPoint,
                        HeightParamName = SelectedHeightParam,
                        HeightOffsetM = HeightOffsetM,
                        BimMetadata = BimMetadata,
                        SttOverrides = SttOverrides
                    };

                    foreach (var m in MaterialMappings)
                    {
                        profile.MaterialMappings[m.ParamName] = m.SelectedMaterialName;
                    }

                    ConfigStorageService.SaveConfig(dlg.FileName, profile);
                    TaskDialog.Show("Lưu Cấu Hình", "Đã lưu cấu hình thành công!");
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Lỗi Lưu Cấu Hình", ex.Message);
                }
            }
        }

        private void OnLoadConfig()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "JSON Profile (*.json)|*.json",
                Title = "Tải Cấu Hình Hố Ga"
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var profile = ConfigStorageService.LoadConfig(dlg.FileName);
                    if (profile != null)
                    {
                        if (!string.IsNullOrWhiteSpace(profile.SelectedFamilyName))
                            SelectedFamilyName = profile.SelectedFamilyName;
                        if (!string.IsNullOrWhiteSpace(profile.SelectedSymbolName))
                            SelectedSymbol = AvailableSymbols.FirstOrDefault(s => s.Name == profile.SelectedSymbolName);
                        if (!string.IsNullOrWhiteSpace(profile.SelectedLevelName))
                            SelectedLevel = AvailableLevels.FirstOrDefault(l => l.Name == profile.SelectedLevelName);

                        PlaceByCdDinh = profile.PlaceByCdDinh;
                        PlaceByCdDay = !profile.PlaceByCdDinh;
                        UseSurveyPoint = profile.UseSurveyPoint;
                        SelectedHeightParam = profile.HeightParamName;
                        HeightOffsetM = profile.HeightOffsetM;

                        if (profile.BimMetadata != null)
                        {
                            BimMetadata.TenCongTrinh = profile.BimMetadata.TenCongTrinh;
                            BimMetadata.HangMuc = profile.BimMetadata.HangMuc;
                            BimMetadata.GoiThau = profile.BimMetadata.GoiThau;
                            BimMetadata.DonViTuVan = profile.BimMetadata.DonViTuVan;
                            BimMetadata.NhaThauThiCong = profile.BimMetadata.NhaThauThiCong;
                            BimMetadata.LyTrinhTuyen = profile.BimMetadata.LyTrinhTuyen;
                            BimMetadata.GhiChu = profile.BimMetadata.GhiChu;
                            BimMetadata.MaCauKien = profile.BimMetadata.MaCauKien;
                            BimMetadata.TenHoGaParamName = profile.BimMetadata.TenHoGaParamName;
                            BimMetadata.ViTriParamName = profile.BimMetadata.ViTriParamName;
                            BimMetadata.SyncSubComponents = profile.BimMetadata.SyncSubComponents;
                        }

                        if (profile.MaterialMappings != null)
                        {
                            foreach (var item in MaterialMappings)
                            {
                                if (profile.MaterialMappings.TryGetValue(item.ParamName, out string? matName))
                                {
                                    item.SelectedMaterialName = matName;
                                }
                            }
                        }

                        if (profile.SttOverrides != null)
                        {
                            SttOverrides.Clear();
                            foreach (var kvp in profile.SttOverrides)
                            {
                                SttOverrides[kvp.Key] = kvp.Value;
                            }
                            RebuildSttDisplayRows();
                        }

                        StatusMessage = "Đã tải cấu hình thành công.";
                    }
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Lỗi Tải Cấu Hình", ex.Message);
                }
            }
        }

        private void OnExecute()
        {
            if (SelectedSymbol == null)
            {
                TaskDialog.Show("Thông báo", "Vui lòng chọn Family Type (FamilySymbol) của hố ga trước khi rải.");
                return;
            }

            if (!RunHoGaTrai && !RunHoGaPhai)
            {
                TaskDialog.Show("Thông báo", "Vui lòng chọn ít nhất một nhánh (Hố Ga Trái hoặc Hố Ga Phải).");
                return;
            }

            int countTrai = RunHoGaTrai ? HoGaTraiList.Count(r => r.IsSelected) : 0;
            int countPhai = RunHoGaPhai ? HoGaPhaiList.Count(r => r.IsSelected) : 0;

            if (countTrai + countPhai == 0)
            {
                TaskDialog.Show("Thông báo", "Không có dữ liệu hố ga nào để rải. Vui lòng nạp file Excel trước.");
                return;
            }

            IsExecuting = true;
            ProgressValue = 0;
            ProgressMax = countTrai + countPhai;
            StatusMessage = "Đang tiến hành rải hố ga...";

            _eventHandler.SetAction(app =>
            {
                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    var result = ManholeCoreEngine.BuildAllManholes(
                        doc: doc,
                        listTrai: HoGaTraiList,
                        listPhai: HoGaPhaiList,
                        runTrai: RunHoGaTrai,
                        runPhai: RunHoGaPhai,
                        symbol: SelectedSymbol,
                        level: SelectedLevel,
                        placeByCdDinh: PlaceByCdDinh,
                        useSurveyPoint: UseSurveyPoint,
                        heightParamName: SelectedHeightParam,
                        heightOffsetM: HeightOffsetM,
                        sttOverrides: SttOverrides,
                        materialMappings: MaterialMappings,
                        bimMetadata: BimMetadata,
                        customNonGeoParams: CustomNonGeoParams,
                        progressCallback: (cur, tot, msg) =>
                        {
                            ProgressValue = cur;
                            StatusMessage = msg;
                        });

                    IsExecuting = false;
                    ProgressValue = ProgressMax;
                    StatusMessage = $"Hoàn tất: {result.TraiCount} Hố ga trái, {result.PhaiCount} Hố ga phải.";

                    string msgSummary = $"TỔNG KẾT KẾT QUẢ RẢI HỐ GA:\n\n" +
                                        $"- Hố Ga Trái tạo thành công: {result.TraiCount}\n" +
                                        $"- Hố Ga Phải tạo thành công: {result.PhaiCount}\n" +
                                        $"- Số lượng lỗi: {result.ErrorCount}\n\n" +
                                        (result.ErrorCount > 0 ? "Chi tiết lỗi:\n" + string.Join("\n", result.Logs.Take(10)) : "Chúc mừng! Toàn bộ hố ga đã được đặt chính xác vào mô hình.");

                    TaskDialog.Show("Kết Quả Rải Hố Ga", msgSummary);
                }
                catch (Exception ex)
                {
                    IsExecuting = false;
                    StatusMessage = "Lỗi trong quá trình rải hố ga.";
                    TaskDialog.Show("Lỗi Thực Thi", ex.ToString());
                }
            });

            _externalEvent.Raise();
        }

        #region TAB 3: Helper Methods Kiểm Tra Mô Hình

        public void RefreshPreviewList()
        {
            PreviewInspectionList.Clear();
            var source = PreviewSheetName == "HO GA PHAI" ? HoGaPhaiList : HoGaTraiList;

            for (int i = 0; i < source.Count; i++)
            {
                var row = source[i];
                var item = new ManholeInspectionItem
                {
                    STT = row.STT,
                    TenHoGa = row.TenHoGa,
                    Side = row.Side,
                    ToaDoX = row.ToaDoX,
                    ToaDoY = row.ToaDoY,
                    CdDinh = row.CdDinh,
                    CdDay = row.CdDay,
                    CdDayCong = row.CdDayCong,
                    LyTrinh = row.LyTrinh,
                    GocPv = row.GocPv
                };
                item.UpdateCalculations();

                // Tính toán độ dốc và khoảng cách tới hố tiếp theo nếu có
                if (i < source.Count - 1)
                {
                    var nextRow = source[i + 1];
                    double dx = nextRow.ToaDoX - row.ToaDoX;
                    double dy = nextRow.ToaDoY - row.ToaDoY;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    item.KhoangCachDenHoSau = Math.Round(dist, 2);
                    if (dist > 0.1)
                    {
                        double slope = ((row.CdDay - nextRow.CdDay) / dist) * 100.0;
                        item.DoDocDenHoSau = Math.Round(slope, 2);
                    }
                }

                PreviewInspectionList.Add(item);
            }

            SelectedInspectionItem = PreviewInspectionList.FirstOrDefault();
        }

        private void OnSelectedInspectionChanged()
        {
            ExtractedRealGeometry = null;
            IsRealGeometryLoaded = false;
            foreach (var item in PreviewInspectionList)
            {
                item.IsSelectedIn3D = (SelectedInspectionItem != null && item.STT == SelectedInspectionItem.STT && item.Side == SelectedInspectionItem.Side);
            }
        }

        private void OnSelectPrevInspection()
        {
            if (PreviewInspectionList.Count == 0) return;
            int idx = SelectedInspectionItem != null ? PreviewInspectionList.IndexOf(SelectedInspectionItem) : 0;
            if (idx > 0)
                SelectedInspectionItem = PreviewInspectionList[idx - 1];
            else
                SelectedInspectionItem = PreviewInspectionList[PreviewInspectionList.Count - 1];
        }

        private void OnSelectNextInspection()
        {
            if (PreviewInspectionList.Count == 0) return;
            int idx = SelectedInspectionItem != null ? PreviewInspectionList.IndexOf(SelectedInspectionItem) : -1;
            if (idx >= 0 && idx < PreviewInspectionList.Count - 1)
                SelectedInspectionItem = PreviewInspectionList[idx + 1];
            else
                SelectedInspectionItem = PreviewInspectionList[0];
        }

        private void OnExtractGeometry()
        {
            if (SelectedSymbol == null)
            {
                TaskDialog.Show("Thông báo", "Vui lòng chọn Family Type trước khi trích xuất hình học thực tế.");
                return;
            }

            double hM = SelectedInspectionItem?.ChieuCaoThan ?? 2.0;

            StatusMessage = "Đang trích xuất hình học thực tế từ Family Revit...";

            _eventHandler.SetAction(app =>
            {
                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    var meshGroup = ManholeMeshExtractor.ExtractRealGeometryFromRevit(
                        doc, 
                        SelectedSymbol, 
                        SelectedHeightParam, 
                        hM);

                    if (meshGroup != null && meshGroup.Parts.Count > 0)
                    {
                        ExtractedRealGeometry = meshGroup;
                        IsRealGeometryLoaded = true;
                        StatusMessage = $"Đã trích xuất {meshGroup.Parts.Count} khối 3D thực tế từ Family '{SelectedSymbol.FamilyName}'.";
                        TaskDialog.Show("Thành Công", $"Đã trích xuất {meshGroup.Parts.Count} khối hình học 3D thực tế từ Family Revit vào Viewport!");
                    }
                    else
                    {
                        StatusMessage = "Family không chứa Solid hoặc không thể trích xuất trực tiếp, sử dụng mô hình 3D tham số hóa.";
                        TaskDialog.Show("Thông báo", "Không trích xuất được khối Solid từ Family, Viewport chuyển sang hiển thị mô hình 3D tham số hóa chuẩn xác.");
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = "Lỗi khi trích xuất hình học: " + ex.Message;
                    TaskDialog.Show("Lỗi Trích Xuất", ex.Message);
                }
            });

            _externalEvent.Raise();
        }

        #endregion
    }
}
