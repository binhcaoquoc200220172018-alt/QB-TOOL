using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using InfraBIM.CulvertTool.Models;
using InfraBIM.CulvertTool.Services;
using Microsoft.Win32;

namespace InfraBIM.CulvertTool.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly UIApplication _uiApp;
        private readonly ExternalEvent _externalEvent;
        private readonly RevitExternalEventHandler _eventHandler;

        public Document Doc => _uiApp.ActiveUIDocument.Document;

        #region Properties - Top Filter & Excel
        private string _selectedTypeFilter = "TẤT CẢ";
        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                if (SetProperty(ref _selectedTypeFilter, value))
                {
                    FilterCulvertRows();
                }
            }
        }

        private string _excelFilePath = string.Empty;
        public string ExcelFilePath
        {
            get => _excelFilePath;
            set => SetProperty(ref _excelFilePath, value);
        }

        public ObservableCollection<string> SheetNames { get; } = new();

        private string _selectedSheetName = string.Empty;
        public string SelectedSheetName
        {
            get => _selectedSheetName;
            set
            {
                if (SetProperty(ref _selectedSheetName, value) && !string.IsNullOrEmpty(value))
                {
                    LoadExcelData();
                }
            }
        }

        public ObservableCollection<string> CoordinateSystems { get; } = new()
        {
            "VN2000 sang Revit (Survey Point + True North)",
            "Tọa độ cục bộ Revit (Local - Không bù trừ)"
        };

        private int _selectedCoordSysIndex = 0;
        public int SelectedCoordSysIndex
        {
            get => _selectedCoordSysIndex;
            set => SetProperty(ref _selectedCoordSysIndex, value);
        }

        public ObservableCollection<string> DuplicateOptions { get; } = new()
        {
            "Cập nhật theo Lý trình",
            "Bỏ qua dòng trùng"
        };

        private int _selectedDuplicateOptionIndex = 0;
        public int SelectedDuplicateOptionIndex
        {
            get => _selectedDuplicateOptionIndex;
            set => SetProperty(ref _selectedDuplicateOptionIndex, value);
        }
        #endregion

        #region Properties - Families & 3 Fixed Component Groups
        public ObservableCollection<FamilySymbolWrapper> AllAvailableFamilies { get; } = new();

        // 3 Cụm cấu kiện cố định chuẩn
        public ObservableCollection<CulvertComponentItem> BarrelComponents { get; } = new();
        public ObservableCollection<CulvertComponentItem> OutletComponents { get; } = new();
        public ObservableCollection<CulvertComponentItem> ManholeComponents { get; } = new();

        // Danh sách gộp tất cả cấu kiện phục vụ Tab 02 Parameter Mapping và Tab 04 Vật liệu
        public ObservableCollection<CulvertComponentItem> AssemblyComponents { get; } = new();

        private CulvertComponentItem? _selectedAssemblyComponent;
        public CulvertComponentItem? SelectedAssemblyComponent
        {
            get => _selectedAssemblyComponent;
            set => SetProperty(ref _selectedAssemblyComponent, value);
        }

        // Mẫu loại cống đang chọn để Highlight nút bấm
        private string _selectedCulvertTemplateType = "CỐNG HỘP ĐÚC SẴN";
        public string SelectedCulvertTemplateType
        {
            get => _selectedCulvertTemplateType;
            set => SetProperty(ref _selectedCulvertTemplateType, value);
        }
        #endregion

        #region Properties - Geometry & Double Culvert Spacing (Decimal Text Inputs)
        private double _lStd = 1.0;
        public double L_Std
        {
            get => _lStd;
            set
            {
                if (SetProperty(ref _lStd, value))
                {
                    _lStdText = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(L_Std_Text));
                    UpdatePreviewGeometry();
                }
            }
        }

        private string _lStdText = "1.00";
        public string L_Std_Text
        {
            get => _lStdText;
            set
            {
                if (SetProperty(ref _lStdText, value))
                {
                    if (TryParseFlexible(value, out double v) && v > 0)
                    {
                        _lStd = v;
                        UpdatePreviewGeometry();
                    }
                }
            }
        }

        private double _kheHo = 0.01;
        public double Khe_Ho
        {
            get => _kheHo;
            set
            {
                if (SetProperty(ref _kheHo, value))
                {
                    _kheHoText = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(Khe_Ho_Text));
                    UpdatePreviewGeometry();
                }
            }
        }

        private string _kheHoText = "0.01";
        public string Khe_Ho_Text
        {
            get => _kheHoText;
            set
            {
                if (SetProperty(ref _kheHoText, value))
                {
                    if (TryParseFlexible(value, out double v) && v >= 0)
                    {
                        _kheHo = v;
                        UpdatePreviewGeometry();
                    }
                }
            }
        }

        private double _khoangCachTimDefault = 2.0;
        public double KhoangCachTimDefault
        {
            get => _khoangCachTimDefault;
            set
            {
                if (SetProperty(ref _khoangCachTimDefault, value))
                {
                    _khoangCachTimText = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(KhoangCachTimDefault_Text));
                    UpdatePreviewGeometry();
                }
            }
        }

        private string _khoangCachTimText = "2.00";
        public string KhoangCachTimDefault_Text
        {
            get => _khoangCachTimText;
            set
            {
                if (SetProperty(ref _khoangCachTimText, value))
                {
                    if (TryParseFlexible(value, out double v) && v > 0)
                    {
                        _khoangCachTimDefault = v;
                        UpdatePreviewGeometry();
                    }
                }
            }
        }

        // Bề rộng hố thu B_Box (khai báo trong Tab 02)
        private double _bBox = 1.50;
        public double B_Box
        {
            get => _bBox;
            set
            {
                if (SetProperty(ref _bBox, value))
                {
                    _bBoxText = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(B_Box_Text));
                    UpdatePreviewGeometry();
                }
            }
        }

        private string _bBoxText = "1.50";
        public string B_Box_Text
        {
            get => _bBoxText;
            set
            {
                if (SetProperty(ref _bBoxText, value))
                {
                    if (TryParseFlexible(value, out double v) && v > 0)
                    {
                        _bBox = v;
                        UpdatePreviewGeometry();
                    }
                }
            }
        }

        // Kích hoạt cống tròn đôi
        private bool _isDoubleCulvertMode = false;
        public bool IsDoubleCulvertMode
        {
            get => _isDoubleCulvertMode;
            set => SetProperty(ref _isDoubleCulvertMode, value);
        }

        // Kích hoạt chế độ cống đổ tại chỗ
        private bool _isCastInPlaceMode = false;
        public bool IsCastInPlaceMode
        {
            get => _isCastInPlaceMode;
            set => SetProperty(ref _isCastInPlaceMode, value);
        }

        public ObservableCollection<string> ArrayModes { get; } = new()
        {
            "Rải từ giữa ra 2 bên (2 đầu co giãn đối xứng - Chịu lực tối ưu)",
            "Rải một chiều (Đốt cuối co giãn - Theo hướng dốc thoát nước)"
        };

        private int _selectedArrayModeIndex = 0;
        public int SelectedArrayModeIndex
        {
            get => _selectedArrayModeIndex;
            set
            {
                if (SetProperty(ref _selectedArrayModeIndex, value))
                {
                    UpdatePreviewGeometry();
                }
            }
        }

        private static bool TryParseFlexible(string? text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string clean = text.Trim().Replace(',', '.');
            return double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);
        }
        #endregion

        #region Properties - Tab 02 Parameter Mapping (Per Family)
        public ObservableCollection<CulvertComponentItem> ActiveAssignedFamiliesForTab02 { get; } = new();

        private CulvertComponentItem? _selectedComponentForTab02;
        public CulvertComponentItem? SelectedComponentForTab02
        {
            get => _selectedComponentForTab02;
            set
            {
                if (SetProperty(ref _selectedComponentForTab02, value))
                {
                    OnSelectedFamilyForTab02Changed();
                }
            }
        }

        // Tùy chọn lọc tham số Dimensions & Other
        private bool _showDimensions = true;
        public bool ShowDimensions
        {
            get => _showDimensions;
            set
            {
                if (SetProperty(ref _showDimensions, value))
                {
                    FilterParameterMappings();
                }
            }
        }

        private bool _showOther = true;
        public bool ShowOther
        {
            get => _showOther;
            set
            {
                if (SetProperty(ref _showOther, value))
                {
                    FilterParameterMappings();
                }
            }
        }

        public ObservableCollection<ParameterMappingItem> ParameterMappings { get; } = new();
        public ObservableCollection<ParameterMappingItem> FilteredParameterMappings { get; } = new();
        #endregion

        #region Properties - DataGrids & Validation
        public ObservableCollection<CulvertRowData> AllCulvertRows { get; } = new();
        public ObservableCollection<CulvertRowData> FilteredCulvertRows { get; } = new();

        public ObservableCollection<ValidationResultItem> ValidationResults { get; } = new();

        public BimInfoConfig BimConfig { get; } = new();

        // Quản lý tham số BIM tùy biến động (Tab 03)
        public ObservableCollection<CustomBimParameterItem> CustomBimParameters { get; } = new();

        private CustomBimParameterItem? _selectedCustomBimParameter;
        public CustomBimParameterItem? SelectedCustomBimParameter
        {
            get => _selectedCustomBimParameter;
            set => SetProperty(ref _selectedCustomBimParameter, value);
        }
        #endregion

        #region Properties - Tab 03 Preview (2D / 3D)
        private CulvertRowData? _selectedCulvertRowForPreview;
        public CulvertRowData? SelectedCulvertRowForPreview
        {
            get => _selectedCulvertRowForPreview;
            set
            {
                if (SetProperty(ref _selectedCulvertRowForPreview, value))
                {
                    UpdatePreviewGeometry();
                }
            }
        }

        private CulvertPreviewGeometry? _currentPreviewGeometry;
        public CulvertPreviewGeometry? CurrentPreviewGeometry
        {
            get => _currentPreviewGeometry;
            set => SetProperty(ref _currentPreviewGeometry, value);
        }

        private PreviewViewMode _previewMode = PreviewViewMode.Profile2D;
        public PreviewViewMode PreviewMode
        {
            get => _previewMode;
            set => SetProperty(ref _previewMode, value);
        }

        private string _previewSearchText = string.Empty;
        public string PreviewSearchText
        {
            get => _previewSearchText;
            set
            {
                if (SetProperty(ref _previewSearchText, value))
                {
                    FilterPreviewCulvertRows();
                }
            }
        }

        public ObservableCollection<CulvertRowData> FilteredPreviewCulvertRows { get; } = new();

        public Action<string>? RequestPreviewAction { get; set; }
        #endregion

        #region Properties - Batch Apply
        private ParameterMappingItem? _selectedParamForBatch;
        public ParameterMappingItem? SelectedParamForBatch
        {
            get => _selectedParamForBatch;
            set => SetProperty(ref _selectedParamForBatch, value);
        }

        private string _batchApplyValue = string.Empty;
        public string BatchApplyValue
        {
            get => _batchApplyValue;
            set => SetProperty(ref _batchApplyValue, value);
        }

        private int _batchFromSTT = 1;
        public int BatchFromSTT
        {
            get => _batchFromSTT;
            set => SetProperty(ref _batchFromSTT, value);
        }

        private int _batchToSTT = 999;
        public int BatchToSTT
        {
            get => _batchToSTT;
            set => SetProperty(ref _batchToSTT, value);
        }
        #endregion

        #region Properties - Validation Summary Cards
        private string _statusConclusion = "CHƯA KIỂM TRA";
        public string StatusConclusion
        {
            get => _statusConclusion;
            set => SetProperty(ref _statusConclusion, value);
        }

        private int _totalErrors = 0;
        public int TotalErrors
        {
            get => _totalErrors;
            set => SetProperty(ref _totalErrors, value);
        }

        private int _totalWarnings = 0;
        public int TotalWarnings
        {
            get => _totalWarnings;
            set => SetProperty(ref _totalWarnings, value);
        }

        private int _totalInfos = 0;
        public int TotalInfos
        {
            get => _totalInfos;
            set => SetProperty(ref _totalInfos, value);
        }

        private string _executionStatus = "Sẵn sàng";
        public string ExecutionStatus
        {
            get => _executionStatus;
            set => SetProperty(ref _executionStatus, value);
        }
        #endregion

        #region Properties - Tab 03 Material Management
        public ObservableCollection<CulvertMaterialItem> ComponentMaterials { get; } = new();
        public ObservableCollection<CulvertMaterialItem> FilteredComponentMaterials { get; } = new();

        private string _selectedMaterialGroupFilter = "All";
        public string SelectedMaterialGroupFilter
        {
            get => _selectedMaterialGroupFilter;
            set
            {
                if (SetProperty(ref _selectedMaterialGroupFilter, value))
                {
                    OnPropertyChanged(nameof(SelectedMaterialGroupFilterName));
                    ApplyMaterialGroupFilter();
                }
            }
        }

        public string SelectedMaterialGroupFilterName => _selectedMaterialGroupFilter switch
        {
            "Barrel" => "🧱 Thân cống",
            "Outlet" => "🌊 Cửa xả & Sân gia cố",
            "Manhole" => "🕳️ Hố ga (Hộp nối)",
            _ => "📁 Tất cả các cụm"
        };

        public ObservableCollection<string> AvailableRevitMaterials { get; } = new();

        private CulvertMaterialItem? _selectedMaterialItem;
        public CulvertMaterialItem? SelectedMaterialItem
        {
            get => _selectedMaterialItem;
            set => SetProperty(ref _selectedMaterialItem, value);
        }
        #endregion

        #region Commands
        public RelayCommand BrowseExcelCommand { get; }
        public RelayCommand CreateSampleExcelCommand { get; }
        public RelayCommand ReloadExcelCommand { get; }
        public RelayCommand ScanFamilyParamsCommand { get; }
        public RelayCommand BatchApplyCommand { get; }
        public RelayCommand ValidateCommand { get; }
        public RelayCommand ExecutePlacementCommand { get; }
        public RelayCommand CloseCommand { get; }

        // Commands for Dynamic Assembly
        public RelayCommand AddComponentCommand { get; }
        public RelayCommand RemoveComponentCommand { get; }
        public RelayCommand<string> ApplyAssemblyTemplateCommand { get; }

        // Commands for Tab 03 Preview
        public RelayCommand<string> SetPreviewModeCommand { get; }
        public RelayCommand FitPreviewCommand { get; }
        public RelayCommand ZoomInPreviewCommand { get; }
        public RelayCommand ZoomOutPreviewCommand { get; }
        public RelayCommand RefreshPreviewCommand { get; }

        // Commands for Material Assignment (Tab 04)
        public RelayCommand ApplyStandardMaterialPresetCommand { get; }
        public RelayCommand<string> ApplyColorPresetCommand { get; }
        public RelayCommand ScanRevitMaterialsCommand { get; }
        public RelayCommand CreateSelectedMaterialInRevitCommand { get; }
        public RelayCommand SyncMaterialsCommand { get; }
        public RelayCommand<string> FilterMaterialGroupCommand { get; }
        public RelayCommand AddMaterialItemCommand { get; }
        public RelayCommand RemoveMaterialItemCommand { get; }
        public RelayCommand OpenColorPickerCommand { get; }
        public RelayCommand<CulvertMaterialItem> OpenColorPickerForItemCommand { get; }

        // Commands for Dynamic BIM Parameters
        public RelayCommand AddCustomBimParameterCommand { get; }
        public RelayCommand RemoveCustomBimParameterCommand { get; }
        public RelayCommand ResetDefaultBimParametersCommand { get; }

        public Action? RequestClose { get; set; }
        #endregion

        public MainViewModel(UIApplication uiApp, ExternalEvent externalEvent, RevitExternalEventHandler eventHandler)
        {
            _uiApp = uiApp;
            _externalEvent = externalEvent;
            _eventHandler = eventHandler;

            BrowseExcelCommand = new RelayCommand(BrowseExcel);
            CreateSampleExcelCommand = new RelayCommand(CreateSampleExcel);
            ReloadExcelCommand = new RelayCommand(LoadExcelData);
            ScanFamilyParamsCommand = new RelayCommand(ScanCurrentSelectedFamilyParameters);
            BatchApplyCommand = new RelayCommand(ApplyBatchValue);
            ValidateCommand = new RelayCommand(RunValidation);
            ExecutePlacementCommand = new RelayCommand(ExecutePlacement);
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke());

            // Dynamic Assembly Commands
            AddComponentCommand = new RelayCommand(AddNewComponent);
            RemoveComponentCommand = new RelayCommand(RemoveSelectedComponent);
            ApplyAssemblyTemplateCommand = new RelayCommand<string>(ApplyAssemblyTemplate);

            // Tab 03 Preview Commands
            SetPreviewModeCommand = new RelayCommand<string>(SetPreviewMode);
            FitPreviewCommand = new RelayCommand(() => RequestPreviewAction?.Invoke("Fit"));
            ZoomInPreviewCommand = new RelayCommand(() => RequestPreviewAction?.Invoke("ZoomIn"));
            ZoomOutPreviewCommand = new RelayCommand(() => RequestPreviewAction?.Invoke("ZoomOut"));
            RefreshPreviewCommand = new RelayCommand(UpdatePreviewGeometry);

            // Material Commands (Tab 04)
            ApplyStandardMaterialPresetCommand = new RelayCommand(ApplyStandardMaterialPreset);
            ApplyColorPresetCommand = new RelayCommand<string>(ApplyColorPreset);
            ScanRevitMaterialsCommand = new RelayCommand(ScanRevitMaterials);
            CreateSelectedMaterialInRevitCommand = new RelayCommand(CreateSelectedMaterialInRevit);
            SyncMaterialsCommand = new RelayCommand(SyncMaterialsFromAssemblyComponents);
            FilterMaterialGroupCommand = new RelayCommand<string>(group =>
            {
                SelectedMaterialGroupFilter = group ?? "All";
            });
            AddMaterialItemCommand = new RelayCommand(AddNewMaterialItem);
            RemoveMaterialItemCommand = new RelayCommand(RemoveSelectedMaterialItem);
            OpenColorPickerCommand = new RelayCommand(OpenColorPicker);
            OpenColorPickerForItemCommand = new RelayCommand<CulvertMaterialItem>(OpenColorPickerForItem);

            // Dynamic BIM Parameter Commands
            AddCustomBimParameterCommand = new RelayCommand(AddNewCustomBimParameter);
            RemoveCustomBimParameterCommand = new RelayCommand(RemoveSelectedCustomBimParameter);
            ResetDefaultBimParametersCommand = new RelayCommand(ResetDefaultCustomBimParameters);

            LoadAvailableFamilies();
            ApplyAssemblyTemplate("CỐNG HỘP ĐƠN");
            ScanRevitMaterials();
            SyncMaterialsFromAssemblyComponents();
            ResetDefaultCustomBimParameters();
        }

        private void BrowseExcel()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Tất cả bảng tính (*.xlsx;*.xlsm;*.csv)|*.xlsx;*.xlsm;*.csv|Excel Files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "Chọn bảng dữ liệu cống ngang Excel hoặc CSV"
            };

            if (dlg.ShowDialog() == true)
            {
                ExcelFilePath = dlg.FileName;
                SheetNames.Clear();
                var sheets = ExcelReaderService.GetSheetNames(ExcelFilePath);
                foreach (var s in sheets) SheetNames.Add(s);

                if (SheetNames.Count > 0)
                {
                    SelectedSheetName = SheetNames[0];
                }
            }
        }

        private void CreateSampleExcel()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "Mau_Du_Lieu_Cong_Ngang.xlsx",
                Title = "Lưu file Excel mẫu 19 cột chuẩn"
            };

            if (dlg.ShowDialog() == true)
            {
                ExcelReaderService.CreateSampleExcelTemplate(dlg.FileName);
                ExcelFilePath = dlg.FileName;
                SheetNames.Clear();
                var sheets = ExcelReaderService.GetSheetNames(ExcelFilePath);
                foreach (var s in sheets) SheetNames.Add(s);
                if (SheetNames.Count > 0) SelectedSheetName = SheetNames[0];

                MessageBox.Show($"Đã tạo thành công file Excel mẫu tại:\n{dlg.FileName}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadExcelData()
        {
            if (string.IsNullOrEmpty(ExcelFilePath) || !File.Exists(ExcelFilePath)) return;

            try
            {
                var rows = ExcelReaderService.ReadCulvertRows(ExcelFilePath, SelectedSheetName);
                AllCulvertRows.Clear();
                foreach (var r in rows) AllCulvertRows.Add(r);

                FilterCulvertRows();
                RunValidation();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đọc file Excel: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterCulvertRows()
        {
            FilteredCulvertRows.Clear();
            foreach (var r in AllCulvertRows)
            {
                if (SelectedTypeFilter == "TẤT CẢ")
                {
                    FilteredCulvertRows.Add(r);
                }
                else if (SelectedTypeFilter == "CỐNG HỘP" && r.LoaiCong.Contains("HOP"))
                {
                    FilteredCulvertRows.Add(r);
                }
                else if (SelectedTypeFilter == "CỐNG TRÒN" && r.LoaiCong.Contains("TRON"))
                {
                    FilteredCulvertRows.Add(r);
                }
                else if (SelectedTypeFilter == "CỐNG ĐÔI" && r.SoCua >= 2)
                {
                    FilteredCulvertRows.Add(r);
                }
            }

            FilterPreviewCulvertRows();
        }

        private FamilySymbolWrapper? FindSymbol(string familyKeyword, string? typeKeyword = null)
        {
            var matches = AllAvailableFamilies.Where(f =>
                f.Symbol.FamilyName.IndexOf(familyKeyword, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (!matches.Any())
            {
                matches = AllAvailableFamilies.Where(f =>
                    f.Symbol.Name.IndexOf(familyKeyword, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            if (!matches.Any()) return null;

            if (!string.IsNullOrEmpty(typeKeyword))
            {
                var typeMatch = matches.FirstOrDefault(f =>
                    f.Symbol.Name.IndexOf(typeKeyword, StringComparison.OrdinalIgnoreCase) >= 0);
                if (typeMatch != null) return typeMatch;
            }
            return matches.FirstOrDefault();
        }

        private void LoadAvailableFamilies()
        {
            AllAvailableFamilies.Clear();

            var collector = new FilteredElementCollector(Doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>();

            foreach (var sym in collector)
            {
                var wrapper = new FamilySymbolWrapper(sym);
                AllAvailableFamilies.Add(wrapper);
            }

            InitOutletComponents();
            InitManholeComponents();
            ApplyAssemblyTemplate(SelectedCulvertTemplateType ?? "CỐNG HỘP ĐÚC SẴN");
        }

        private void InitOutletComponents()
        {
            if (OutletComponents.Count > 0) return;

            OutletComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cửa xả",
                CategoryType = "Cửa xả - Tường đầu",
                SelectedSymbol = FindSymbol("TNN_CUA XA", "TUONG DAU") ?? FindSymbol("TNN_CUA XA"),
                OffsetZ = 0.0,
                Note = "Tường đầu cửa xả"
            });
            OutletComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cửa xả",
                CategoryType = "Cửa xả - Tường cánh",
                SelectedSymbol = FindSymbol("TNN_CUA XA", "TUONG CANH") ?? FindSymbol("TNN_CUA XA"),
                OffsetZ = 0.0,
                Note = "Tường cánh cửa xả"
            });
            OutletComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cửa xả",
                CategoryType = "Cửa xả - Sân cống",
                SelectedSymbol = FindSymbol("TNN_CUA XA", "SAN CONG") ?? FindSymbol("TNN_CUA XA"),
                OffsetZ = 0.0,
                Note = "Sân cống cửa xả"
            });
            OutletComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cửa xả",
                CategoryType = "Cửa xả - Bê tông lót",
                SelectedSymbol = FindSymbol("TNN_CUA XA", "BE TONG LOT") ?? FindSymbol("BE TONG LOT"),
                OffsetZ = -0.10,
                Note = "Bê tông lót cửa xả"
            });
            OutletComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cửa xả",
                CategoryType = "Cửa xả - CPDD / Đá dăm",
                SelectedSymbol = FindSymbol("TNN_CUA XA", "CPDD") ?? FindSymbol("DA DAM"),
                OffsetZ = -0.20,
                Note = "Lớp CPDD / đá dăm đệm cửa xả"
            });

            OutletComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cửa xả",
                CategoryType = "Sân gia cố - Tấm sân",
                SelectedSymbol = FindSymbol("TNN_SAN GIA CO_CUA XA", "SAN GIA CO") ?? FindSymbol("TNN_SAN GIA CO"),
                OffsetZ = 0.0,
                Note = "Tấm sân gia cố nối dài"
            });
            OutletComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cửa xả",
                CategoryType = "Sân gia cố - Bê tông lót",
                SelectedSymbol = FindSymbol("TNN_SAN GIA CO_CUA XA", "BE TONG LOT") ?? FindSymbol("BE TONG LOT"),
                OffsetZ = -0.10,
                Note = "Bê tông lót sân gia cố"
            });
        }

        private void InitManholeComponents()
        {
            if (ManholeComponents.Count > 0) return;

            ManholeComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Hố ga",
                CategoryType = "Thân hố ga",
                SelectedSymbol = FindSymbol("TNM_HG_DO TAI CHO") ?? FindSymbol("TNM_HG_DUC SAN") ?? FindSymbol("TNM_HG"),
                OffsetZ = 0.0,
                Note = "Thân buồng thu hố ga (hộp nối)"
            });
            ManholeComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Hố ga",
                CategoryType = "Cổ giếng",
                SelectedSymbol = FindSymbol("TNM_HG_CO GIENG") ?? FindSymbol("CO GIENG"),
                OffsetZ = 0.50,
                Note = "Cổ giếng hố ga"
            });
            ManholeComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Hố ga",
                CategoryType = "Khuôn hầm",
                SelectedSymbol = FindSymbol("TNM_HG_KHUON HAM") ?? FindSymbol("KHUON HAM"),
                OffsetZ = 0.80,
                Note = "Khuôn đỡ nắp hầm ga"
            });
            ManholeComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Hố ga",
                CategoryType = "Nắp đan",
                SelectedSymbol = FindSymbol("TNM_HG_NAP DAN") ?? FindSymbol("NAP DAN"),
                OffsetZ = 0.90,
                Note = "Tấm đan nắp đậy hố ga"
            });
            ManholeComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Hố ga",
                CategoryType = "Bê tông lót hố ga",
                SelectedSymbol = FindSymbol("TNM_HG_BE TONG LOT") ?? FindSymbol("BE TONG LOT"),
                OffsetZ = -0.20,
                Note = "Bê tông lót đáy hố ga"
            });
        }

        #region Dynamic Component Assembly Methods
        private void AddNewComponent()
        {
            // Cấu kiện đã cố định theo 3 cụm chuẩn
        }

        private void RemoveSelectedComponent()
        {
            // Cấu kiện đã cố định theo 3 cụm chuẩn
        }

        public void ApplyAssemblyTemplate(string? templateType)
        {
            string type = templateType?.ToUpperInvariant() ?? "CỐNG HỘP ĐÚC SẴN";
            SelectedCulvertTemplateType = type;

            BarrelComponents.Clear();

            if (type.Contains("ĐỔ TẠI CHỖ") || type.Contains("DO TAI CHO"))
            {
                IsCastInPlaceMode = true;
                IsDoubleCulvertMode = false;
                L_Std = 4.0;
                Khe_Ho = 0.02;

                BarrelComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Thân cống",
                    CategoryType = "Thân cống hộp đổ tại chỗ",
                    SelectedSymbol = FindSymbol("TNN_CH_THAN CONG") ?? AllAvailableFamilies.FirstOrDefault(),
                    OffsetZ = 0.0,
                    Note = "Đốt thân cống hộp đổ tại chỗ"
                });
                BarrelComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Thân cống",
                    CategoryType = "Bê tông lót thân cống",
                    SelectedSymbol = FindSymbol("TNN_BE TONG LOT_THAN CONG") ?? FindSymbol("BE TONG LOT"),
                    OffsetZ = -0.10,
                    Note = "Lớp bê tông lót thân cống"
                });
                BarrelComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Thân cống",
                    CategoryType = "Đá dăm đệm thân cống",
                    SelectedSymbol = FindSymbol("TNN_DA DAM DEM_THAN CONG") ?? FindSymbol("DA DAM"),
                    OffsetZ = -0.20,
                    Note = "Lớp đá dăm đệm thân cống"
                });
            }
            else if (type.Contains("TRÒN") || type.Contains("TRON"))
            {
                IsCastInPlaceMode = false;
                IsDoubleCulvertMode = type.Contains("ĐÔI") || type.Contains("DOI");
                L_Std = 1.0;
                Khe_Ho = 0.00;

                BarrelComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Thân cống",
                    CategoryType = "Thân cống tròn",
                    SelectedSymbol = FindSymbol("TNN_CT_THAN CONG") ?? AllAvailableFamilies.FirstOrDefault(),
                    OffsetZ = 0.0,
                    Note = "Đốt ống cống tròn đúc sẵn"
                });
                BarrelComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Thân cống",
                    CategoryType = "Móng trên cống tròn",
                    SelectedSymbol = FindSymbol("TNN_CT_MONG TREN") ?? AllAvailableFamilies.FirstOrDefault(),
                    OffsetZ = -0.05,
                    Note = "Móng trên cống tròn"
                });
                BarrelComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Thân cống",
                    CategoryType = "Gối cống tròn",
                    SelectedSymbol = FindSymbol("TNN_CT_GOI CONG") ?? AllAvailableFamilies.FirstOrDefault(),
                    OffsetZ = -0.15,
                    Note = "Gối đỡ cống tròn"
                });
                BarrelComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Thân cống",
                    CategoryType = "Vữa xi măng mối nối",
                    SelectedSymbol = FindSymbol("TNM_CT_VUA XI MANG") ?? AllAvailableFamilies.FirstOrDefault(),
                    OffsetZ = -0.12,
                    Note = "Lớp vữa xi măng chèn mối nối"
                });
                BarrelComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Thân cống",
                    CategoryType = "Bê tông lót thân cống",
                    SelectedSymbol = FindSymbol("TNN_BE TONG LOT_THAN CONG") ?? FindSymbol("BE TONG LOT"),
                    OffsetZ = -0.30,
                    Note = "Bê tông lót móng cống tròn"
                });
                BarrelComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Thân cống",
                    CategoryType = "Đá dăm đệm thân cống",
                    SelectedSymbol = FindSymbol("TNN_DA DAM DEM_THAN CONG") ?? FindSymbol("DA DAM"),
                    OffsetZ = -0.40,
                    Note = "Đá dăm đệm cống tròn"
                });
            }
            else
            {
                IsCastInPlaceMode = false;
                IsDoubleCulvertMode = false;
                L_Std = 1.0;
                Khe_Ho = 0.01;

                BarrelComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Thân cống",
                    CategoryType = "Thân cống hộp đúc sẵn",
                    SelectedSymbol = FindSymbol("TNN_CH_THAN CONG") ?? AllAvailableFamilies.FirstOrDefault(),
                    OffsetZ = 0.0,
                    Note = "Đốt thân cống hộp đúc sẵn"
                });
                BarrelComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Thân cống",
                    CategoryType = "Bê tông lót thân cống",
                    SelectedSymbol = FindSymbol("TNN_BE TONG LOT_THAN CONG") ?? FindSymbol("BE TONG LOT"),
                    OffsetZ = -0.10,
                    Note = "Lớp bê tông lót thân cống"
                });
                BarrelComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Thân cống",
                    CategoryType = "Đá dăm đệm thân cống",
                    SelectedSymbol = FindSymbol("TNN_DA DAM DEM_THAN CONG") ?? FindSymbol("DA DAM"),
                    OffsetZ = -0.20,
                    Note = "Lớp đá dăm đệm thân cống"
                });
            }

            SyncAllAssemblyComponents();
            UpdatePreviewGeometry();
        }

        private void SyncAllAssemblyComponents()
        {
            foreach (var c in AssemblyComponents)
            {
                c.PropertyChanged -= OnAssemblyComponentPropertyChanged;
            }

            AssemblyComponents.Clear();
            foreach (var c in BarrelComponents) AssemblyComponents.Add(c);
            foreach (var c in OutletComponents) AssemblyComponents.Add(c);
            foreach (var c in ManholeComponents) AssemblyComponents.Add(c);

            foreach (var c in AssemblyComponents)
            {
                c.PropertyChanged += OnAssemblyComponentPropertyChanged;
            }

            RefreshActiveAssignedFamiliesForTab02();
            SyncMaterialsFromAssemblyComponents();
        }

        private void OnAssemblyComponentPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CulvertComponentItem.CategoryType) ||
                e.PropertyName == nameof(CulvertComponentItem.SelectedSymbol) ||
                e.PropertyName == nameof(CulvertComponentItem.IsActive))
            {
                RefreshActiveAssignedFamiliesForTab02();
                SyncMaterialsFromAssemblyComponents();
            }
        }

        public void RefreshActiveAssignedFamiliesForTab02()
        {
            var oldSelected = SelectedComponentForTab02;
            ActiveAssignedFamiliesForTab02.Clear();

            foreach (var comp in AssemblyComponents)
            {
                if (comp.IsActive && comp.SelectedSymbol != null)
                {
                    ActiveAssignedFamiliesForTab02.Add(comp);
                }
            }

            if (oldSelected != null && ActiveAssignedFamiliesForTab02.Contains(oldSelected))
            {
                SelectedComponentForTab02 = oldSelected;
            }
            else
            {
                SelectedComponentForTab02 = ActiveAssignedFamiliesForTab02.FirstOrDefault();
            }
        }
        #endregion

        #region Tab 03 Preview Methods
        public void SetPreviewMode(string? mode)
        {
            if (string.Equals(mode, "Plan", StringComparison.OrdinalIgnoreCase))
                PreviewMode = PreviewViewMode.Plan2D;
            else if (string.Equals(mode, "3D", StringComparison.OrdinalIgnoreCase))
                PreviewMode = PreviewViewMode.Isometric3D;
            else
                PreviewMode = PreviewViewMode.Profile2D;
        }

        public void FilterPreviewCulvertRows()
        {
            FilteredPreviewCulvertRows.Clear();
            string kw = (PreviewSearchText ?? "").Trim().ToLowerInvariant();

            foreach (var r in FilteredCulvertRows)
            {
                if (string.IsNullOrEmpty(kw) ||
                    r.STT.ToString().Contains(kw) ||
                    r.LyTrinh.ToLowerInvariant().Contains(kw) ||
                    r.LoaiCong.ToLowerInvariant().Contains(kw) ||
                    r.KhauDo.ToLowerInvariant().Contains(kw))
                {
                    FilteredPreviewCulvertRows.Add(r);
                }
            }

            if (SelectedCulvertRowForPreview == null || !FilteredPreviewCulvertRows.Contains(SelectedCulvertRowForPreview))
            {
                SelectedCulvertRowForPreview = FilteredPreviewCulvertRows.FirstOrDefault();
            }
            else
            {
                UpdatePreviewGeometry();
            }
        }

        public void UpdatePreviewGeometry()
        {
            var target = SelectedCulvertRowForPreview ?? FilteredPreviewCulvertRows.FirstOrDefault() ?? FilteredCulvertRows.FirstOrDefault();
            if (target == null)
            {
                CurrentPreviewGeometry = null;
                return;
            }

            var btlDotComp = BarrelComponents.FirstOrDefault(c => c.CategoryType.Contains("lót") || c.CategoryType.Contains("BTL"));
            bool hasBtlDot = btlDotComp?.IsActive ?? true;
            double btlOffsetDot = btlDotComp?.OffsetZ ?? -0.10;

            double catOffset = BarrelComponents.FirstOrDefault(c => c.CategoryType.Contains("cát") || c.CategoryType.Contains("đá dăm") || c.CategoryType.Contains("đệm"))?.OffsetZ ?? -0.20;
            var mode = (SelectedArrayModeIndex == 0) ? CulvertArrayMode.CenterOut : CulvertArrayMode.OneWay;

            string lc = (target.LoaiCong ?? "").ToUpperInvariant();
            bool isRound = lc.Contains("TRON") || lc.Contains("TRÒN") || lc.Contains("CT") || SelectedCulvertTemplateType.Contains("TRÒN");
            bool isDouble = IsDoubleCulvertMode || (target.SoCua >= 2 && isRound);

            CurrentPreviewGeometry = CulvertPreviewService.ComputePreview(
                target,
                L_Std,
                Khe_Ho,
                B_Box,
                mode,
                isCastInPlace: IsCastInPlaceMode,
                isRound: isRound,
                isDouble: isDouble,
                offsetZ_BTL: btlOffsetDot,
                offsetZ_Cat: catOffset,
                hasBtlDot: hasBtlDot);
        }
        #endregion

        #region Tab 04 Material Methods
        public void ApplyMaterialGroupFilter()
        {
            FilteredComponentMaterials.Clear();
            string filter = _selectedMaterialGroupFilter ?? "All";

            foreach (var item in ComponentMaterials)
            {
                if (filter == "All")
                {
                    FilteredComponentMaterials.Add(item);
                }
                else if (filter == "Barrel" && (item.GroupType == "Thân cống" || item.GroupType?.Contains("Thân") == true))
                {
                    FilteredComponentMaterials.Add(item);
                }
                else if (filter == "Outlet" && (item.GroupType == "Cửa xả & Sân gia cố" || item.GroupType?.Contains("Cửa") == true || item.GroupType?.Contains("Sân") == true))
                {
                    FilteredComponentMaterials.Add(item);
                }
                else if (filter == "Manhole" && (item.GroupType == "Hố ga (Hộp nối)" || item.GroupType?.Contains("Hố") == true || item.GroupType?.Contains("Hộp") == true))
                {
                    FilteredComponentMaterials.Add(item);
                }
            }

            if (SelectedMaterialItem == null || !FilteredComponentMaterials.Contains(SelectedMaterialItem))
            {
                SelectedMaterialItem = FilteredComponentMaterials.FirstOrDefault();
            }
        }

        public void SyncMaterialsFromAssemblyComponents()
        {
            if (ComponentMaterials.Count == AssemblyComponents.Count && ComponentMaterials.Count > 0)
            {
                for (int i = 0; i < AssemblyComponents.Count; i++)
                {
                    var comp = AssemblyComponents[i];
                    var mat = ComponentMaterials[i];
                    mat.GroupType = comp.GroupType;
                    mat.CategoryType = comp.CategoryType;
                    mat.FamilyDisplayName = comp.SelectedSymbol?.DisplayName ?? "(Chưa chọn Family)";
                    mat.IsActive = comp.IsActive;
                }
            }
            else
            {
                var existing = ComponentMaterials.ToDictionary(m => m.CategoryType, m => m);
                ComponentMaterials.Clear();

                foreach (var comp in AssemblyComponents)
                {
                    if (string.IsNullOrWhiteSpace(comp.CategoryType)) continue;

                    if (existing.TryGetValue(comp.CategoryType, out var oldItem))
                    {
                        oldItem.GroupType = comp.GroupType;
                        oldItem.FamilyDisplayName = comp.SelectedSymbol?.DisplayName ?? "(Chưa chọn Family)";
                        oldItem.IsActive = comp.IsActive;
                        ComponentMaterials.Add(oldItem);
                    }
                    else
                    {
                        var newItem = CreateDefaultMaterialItem(comp.CategoryType, comp.SelectedSymbol?.DisplayName ?? "(Chưa chọn Family)", comp.IsActive);
                        newItem.GroupType = comp.GroupType;
                        ComponentMaterials.Add(newItem);
                    }
                }
            }

            ApplyMaterialGroupFilter();

            if (SelectedMaterialItem == null || !FilteredComponentMaterials.Contains(SelectedMaterialItem))
            {
                SelectedMaterialItem = FilteredComponentMaterials.FirstOrDefault();
            }
        }

        private CulvertMaterialItem CreateDefaultMaterialItem(string category, string familyName, bool isActive)
        {
            var item = new CulvertMaterialItem
            {
                CategoryType = category,
                FamilyDisplayName = familyName,
                IsActive = isActive,
                Transparency = 0.0,
                MaterialParamName = "Material"
            };

            string catUpper = category.ToUpperInvariant();

            if (catUpper.Contains("CHUẨN") || catUpper.Contains("CHỨA") || catUpper.Contains("ĐỐT"))
            {
                item.MaterialName = "BTCT_M300_Cống đúc sẵn";
                item.SetColorRgb(189, 195, 199);
            }
            else if (catUpper.Contains("BÙ") || catUpper.Contains("CO GIÃN"))
            {
                item.MaterialName = "BTCT_M300_Cống đúc sẵn";
                item.SetColorRgb(189, 195, 199);
            }
            else if (catUpper.Contains("SÂN") || catUpper.Contains("CỬA XẢ") || catUpper.Contains("THƯỢNG LƯU") || catUpper.Contains("HẠ LƯU"))
            {
                item.MaterialName = "BT_M250_Đổ tại chỗ";
                item.SetColorRgb(149, 165, 166);
            }
            else if (catUpper.Contains("HỘP NỐI") || catUpper.Contains("HỐ THU") || catUpper.Contains("HỐ GA"))
            {
                item.MaterialName = "BT_M250_Hố ga";
                item.SetColorRgb(127, 140, 141);
            }
            else if (catUpper.Contains("LÓT") || catUpper.Contains("BTL"))
            {
                item.MaterialName = "BT_M100_Lót móng";
                item.SetColorRgb(74, 85, 104);
            }
            else if (catUpper.Contains("CÁT") || catUpper.Contains("ĐỆM CÁT"))
            {
                item.MaterialName = "Cát đầm chặt K95";
                item.SetColorRgb(236, 201, 75);
            }
            else if (catUpper.Contains("ĐÁ DĂM") || catUpper.Contains("GỐI"))
            {
                item.MaterialName = "Đá dăm 1x2 đệm";
                item.SetColorRgb(86, 101, 115);
            }
            else
            {
                item.MaterialName = "Vật liệu hạ tầng kỹ thuật";
                item.SetColorRgb(160, 174, 192);
            }

            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CulvertMaterialItem.MaterialName))
                {
                    UpdateMaterialStatusNote(item);
                }
            };

            UpdateMaterialStatusNote(item);
            return item;
        }

        public void ApplyStandardMaterialPreset()
        {
            foreach (var item in ComponentMaterials)
            {
                string catUpper = item.CategoryType.ToUpperInvariant();

                if (catUpper.Contains("CHUẨN") || catUpper.Contains("ĐỐT"))
                {
                    item.MaterialName = "BTCT_M300_Cống đúc sẵn";
                    item.SetColorRgb(189, 195, 199);
                }
                else if (catUpper.Contains("BÙ") || catUpper.Contains("CO GIÃN"))
                {
                    item.MaterialName = "BTCT_M300_Cống đúc sẵn";
                    item.SetColorRgb(189, 195, 199);
                }
                else if (catUpper.Contains("SÂN") || catUpper.Contains("THƯỢNG LƯU") || catUpper.Contains("HẠ LƯU"))
                {
                    item.MaterialName = "BT_M250_Đổ tại chỗ";
                    item.SetColorRgb(149, 165, 166);
                }
                else if (catUpper.Contains("HỘP NỐI") || catUpper.Contains("HỐ THU") || catUpper.Contains("HỐ GA"))
                {
                    item.MaterialName = "BT_M250_Hố ga";
                    item.SetColorRgb(127, 140, 141);
                }
                else if (catUpper.Contains("LÓT") || catUpper.Contains("BTL"))
                {
                    item.MaterialName = "BT_M100_Lót móng";
                    item.SetColorRgb(74, 85, 104);
                }
                else if (catUpper.Contains("CÁT") || catUpper.Contains("ĐỆM CÁT"))
                {
                    item.MaterialName = "Cát đầm chặt K95";
                    item.SetColorRgb(236, 201, 75);
                }
                else if (catUpper.Contains("ĐÁ DĂM") || catUpper.Contains("GỐI"))
                {
                    item.MaterialName = "Đá dăm 1x2 đệm";
                    item.SetColorRgb(86, 101, 115);
                }

                UpdateMaterialStatusNote(item);
            }

            MessageBox.Show("Đã áp dụng mẫu vật liệu & màu sắc TCVN chuẩn cho toàn bộ cấu kiện.", "Áp dụng mẫu TCVN", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ApplyColorPreset(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return;

            if (SelectedMaterialItem != null)
            {
                SelectedMaterialItem.ColorHex = hex;
            }
        }

        public void ScanRevitMaterials()
        {
            AvailableRevitMaterials.Clear();
            var mats = BimMaterialService.GetAllMaterialNames(Doc);
            foreach (var m in mats)
            {
                AvailableRevitMaterials.Add(m);
            }

            foreach (var item in ComponentMaterials)
            {
                UpdateMaterialStatusNote(item);
            }
        }

        private void UpdateMaterialStatusNote(CulvertMaterialItem item)
        {
            if (string.IsNullOrWhiteSpace(item.MaterialName))
            {
                item.StatusNote = "Chưa đặt tên";
                return;
            }

            bool exists = AvailableRevitMaterials.Any(m => string.Equals(m, item.MaterialName.Trim(), StringComparison.OrdinalIgnoreCase));
            item.StatusNote = exists ? "Đã có trong Revit" : "Sẽ tạo mới";
        }

        public void CreateSelectedMaterialInRevit()
        {
            if (SelectedMaterialItem == null || string.IsNullOrWhiteSpace(SelectedMaterialItem.MaterialName))
            {
                MessageBox.Show("Vui lòng chọn cấu kiện và nhập tên vật liệu cần tạo.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _eventHandler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                using (var t = new Transaction(doc, $"Tạo vật liệu {SelectedMaterialItem.MaterialName}"))
                {
                    t.Start();
                    var mat = BimMaterialService.GetOrCreateMaterial(
                        doc,
                        SelectedMaterialItem.MaterialName,
                        SelectedMaterialItem.ColorR,
                        SelectedMaterialItem.ColorG,
                        SelectedMaterialItem.ColorB,
                        SelectedMaterialItem.Transparency);
                    t.Commit();
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ScanRevitMaterials();
                    MessageBox.Show($"Đã tạo/cập nhật thành công vật liệu '{SelectedMaterialItem.MaterialName}' trong dự án Revit.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });

            _externalEvent.Raise();
        }

        private void AddNewMaterialItem()
        {
            var newItem = new CulvertMaterialItem
            {
                IsActive = true,
                GroupType = _selectedMaterialGroupFilter switch
                {
                    "Barrel" => "Thân cống",
                    "Outlet" => "Cửa xả & Sân gia cố",
                    "Manhole" => "Hố ga (Hộp nối)",
                    _ => "Thân cống"
                },
                CategoryType = "Cấu kiện tùy chọn",
                FamilyDisplayName = "(Tùy biến)",
                MaterialName = "BTCT_M300_Mới",
                Transparency = 0.0,
                MaterialParamName = "Material"
            };
            newItem.SetColorRgb(189, 195, 199);
            newItem.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CulvertMaterialItem.MaterialName))
                {
                    UpdateMaterialStatusNote(newItem);
                }
            };
            UpdateMaterialStatusNote(newItem);
            ComponentMaterials.Add(newItem);
            ApplyMaterialGroupFilter();
            SelectedMaterialItem = newItem;
        }

        private void RemoveSelectedMaterialItem()
        {
            if (SelectedMaterialItem != null)
            {
                var item = SelectedMaterialItem;
                ComponentMaterials.Remove(item);
                ApplyMaterialGroupFilter();
                SelectedMaterialItem = FilteredComponentMaterials.FirstOrDefault();
            }
        }

        public void OpenColorPicker()
        {
            if (SelectedMaterialItem == null)
            {
                System.Windows.MessageBox.Show("Vui lòng chọn một cấu kiện trong bảng trước khi chọn màu.", "Chưa chọn cấu kiện", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            using var dlg = new System.Windows.Forms.ColorDialog
            {
                AllowFullOpen = true,
                AnyColor = true,
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(SelectedMaterialItem.ColorR, SelectedMaterialItem.ColorG, SelectedMaterialItem.ColorB)
            };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SelectedMaterialItem.SetColorRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B);
            }
        }

        public void OpenColorPickerForItem(CulvertMaterialItem? item)
        {
            if (item == null) return;
            SelectedMaterialItem = item;
            OpenColorPicker();
        }
        #endregion

        #region Tab 02 Parameter Mapping Methods
        private void OnSelectedFamilyForTab02Changed()
        {
            if (SelectedComponentForTab02?.SelectedSymbol?.Symbol == null)
            {
                FilterParameterMappings();
                return;
            }

            var sym = SelectedComponentForTab02.SelectedSymbol.Symbol;
            string cat = SelectedComponentForTab02.CategoryType;

            // Nếu chưa có tham số nào của Family này trong ParameterMappings, tự động quét ngay
            bool hasParams = ParameterMappings.Any(m => m.FamilyName == sym.FamilyName || m.CategoryName == cat);
            if (!hasParams)
            {
                var scanned = FamilyParameterScannerService.ScanParametersForFamily(Doc, sym, cat);
                foreach (var p in scanned)
                {
                    ParameterMappings.Add(p);
                }
            }

            FilterParameterMappings();
        }

        private void FilterParameterMappings()
        {
            FilteredParameterMappings.Clear();
            if (SelectedComponentForTab02?.SelectedSymbol?.Symbol == null) return;

            var sym = SelectedComponentForTab02.SelectedSymbol.Symbol;
            string cat = SelectedComponentForTab02.CategoryType;

            foreach (var m in ParameterMappings)
            {
                bool matchFam = string.Equals(m.FamilyName, sym.FamilyName, StringComparison.OrdinalIgnoreCase);
                bool matchCat = string.Equals(m.CategoryName, cat, StringComparison.OrdinalIgnoreCase);

                if (matchFam || matchCat)
                {
                    bool allow = false;
                    if (ShowDimensions && m.IsDimension) allow = true;
                    if (ShowOther && m.IsOther) allow = true;
                    if (!ShowDimensions && !ShowOther) allow = true; // nếu bỏ tích cả 2 thì hiện toàn bộ

                    if (allow)
                    {
                        FilteredParameterMappings.Add(m);
                    }
                }
            }

            SelectedParamForBatch = FilteredParameterMappings.FirstOrDefault();
        }

        private void ScanCurrentSelectedFamilyParameters()
        {
            if (SelectedComponentForTab02?.SelectedSymbol?.Symbol == null)
            {
                MessageBox.Show("Vui lòng chọn một Family trong danh sách để quét tham số.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sym = SelectedComponentForTab02.SelectedSymbol.Symbol;
            string cat = SelectedComponentForTab02.CategoryType;

            // Xóa tham số cũ của Family này để quét mới nhất
            var toRemove = ParameterMappings.Where(m => m.FamilyName == sym.FamilyName || m.CategoryName == cat).ToList();
            foreach (var item in toRemove) ParameterMappings.Remove(item);

            var scanned = FamilyParameterScannerService.ScanParametersForFamily(Doc, sym, cat);
            foreach (var p in scanned)
            {
                ParameterMappings.Add(p);
            }

            FilterParameterMappings();

            int dimCount = scanned.Count(p => p.IsDimension);
            int otherCount = scanned.Count(p => p.IsOther);
            MessageBox.Show($"Đã quét thành công Family '{sym.FamilyName}' ({cat}):\n- Tổng số tham số: {scanned.Count}\n- Tham số Kích thước (Dimensions): {dimCount} tham số\n- Tham số nhóm Khác (Other): {otherCount} tham số.", "Quét hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplyBatchValue()
        {
            if (SelectedParamForBatch == null || string.IsNullOrEmpty(BatchApplyValue))
            {
                MessageBox.Show("Vui lòng chọn tham số và nhập giá trị cần gán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedParamForBatch.CustomValue = BatchApplyValue;

            int count = 0;
            foreach (var r in AllCulvertRows)
            {
                if (r.STT >= BatchFromSTT && r.STT <= BatchToSTT)
                {
                    count++;
                }
            }

            MessageBox.Show($"Đã gán giá trị '{BatchApplyValue}' cho tham số '{SelectedParamForBatch.InternalName}' của Family '{SelectedParamForBatch.FamilyName}' (Phạm vi cống STT {BatchFromSTT} - {BatchToSTT}, {count} cống).", "Hoàn thành", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        #region Custom BIM Parameters Methods (Tab 03)
        private void AddNewCustomBimParameter()
        {
            var newItem = new CustomBimParameterItem
            {
                IsActive = true,
                ParamName = "BIM_Parameter",
                TargetScope = "Tất cả cấu kiện",
                ValueTemplate = "{LyTrinh}",
                Description = "Thuộc tính tùy biến dự án"
            };
            CustomBimParameters.Add(newItem);
            SelectedCustomBimParameter = newItem;
        }

        private void RemoveSelectedCustomBimParameter()
        {
            if (SelectedCustomBimParameter != null)
            {
                CustomBimParameters.Remove(SelectedCustomBimParameter);
                SelectedCustomBimParameter = CustomBimParameters.FirstOrDefault();
            }
        }

        public void ResetDefaultCustomBimParameters()
        {
            CustomBimParameters.Clear();
            CustomBimParameters.Add(new CustomBimParameterItem { IsActive = true, ParamName = "BIM_LyTrinh", TargetScope = "Tất cả cấu kiện", ValueTemplate = "{LyTrinh}", Description = "Lý trình cống (Km...)" });
            CustomBimParameters.Add(new CustomBimParameterItem { IsActive = true, ParamName = "BIM_LoaiCong", TargetScope = "Tất cả cấu kiện", ValueTemplate = "{LoaiCong}", Description = "Chủng loại cống (Tròn / Hộp)" });
            CustomBimParameters.Add(new CustomBimParameterItem { IsActive = true, ParamName = "BIM_KhauDo", TargetScope = "Tất cả cấu kiện", ValueTemplate = "{KhauDo}", Description = "Khẩu độ thiết kế (D... / BxH)" });
            CustomBimParameters.Add(new CustomBimParameterItem { IsActive = true, ParamName = "BIM_DoDoc", TargetScope = "Đốt cống", ValueTemplate = "{DoDoc}%", Description = "Độ dốc dọc lòng cống" });
            CustomBimParameters.Add(new CustomBimParameterItem { IsActive = true, ParamName = "BIM_GocXoay", TargetScope = "Đốt cống", ValueTemplate = "{GocXoay}°", Description = "Góc xoay tim cống so với Bắc thực" });
            CustomBimParameters.Add(new CustomBimParameterItem { IsActive = true, ParamName = "Comments", TargetScope = "Tất cả cấu kiện", ValueTemplate = "Cống {STT} - {LoaiCong} ({KhauDo})", Description = "Ghi chú Revit mặc định" });
            CustomBimParameters.Add(new CustomBimParameterItem { IsActive = true, ParamName = "Mark", TargetScope = "Tất cả cấu kiện", ValueTemplate = "CN-{STT}", Description = "Mã hiệu cấu kiện" });
        }
        #endregion

        #region Validation Methods (Tab 04)
        public void RunValidation()
        {
            ValidationResults.Clear();
            int err = 0;
            int warn = 0;
            int info = 0;

            if (AllCulvertRows.Count == 0)
            {
                ValidationResults.Add(new ValidationResultItem
                {
                    Level = ValidationLevel.Warning,
                    RowIndex = 0,
                    Category = "Dữ liệu",
                    Message = "Chưa nạp bảng tính dữ liệu cống."
                });
                warn++;
            }

            foreach (var r in AllCulvertRows)
            {
                // Kiểm tra tọa độ P1, P2
                if (r.X1 == 0 && r.Y1 == 0)
                {
                    ValidationResults.Add(new ValidationResultItem
                    {
                        Level = ValidationLevel.Error,
                        RowIndex = r.STT,
                        Category = "Tọa độ",
                        Message = $"Cống STT {r.STT} ({r.LyTrinh}): Thiếu tọa độ Sân cống 1 (X1=0, Y1=0)."
                    });
                    err++;
                }

                if (r.X2 == 0 && r.Y2 == 0)
                {
                    ValidationResults.Add(new ValidationResultItem
                    {
                        Level = ValidationLevel.Error,
                        RowIndex = r.STT,
                        Category = "Tọa độ",
                        Message = $"Cống STT {r.STT} ({r.LyTrinh}): Thiếu tọa độ Sân cống 2 (X2=0, Y2=0)."
                    });
                    err++;
                }

                double l2D = r.TinhChieuDai2D();
                if (l2D < 1.0)
                {
                    ValidationResults.Add(new ValidationResultItem
                    {
                        Level = ValidationLevel.Error,
                        RowIndex = r.STT,
                        Category = "Hình học",
                        Message = $"Cống STT {r.STT} ({r.LyTrinh}): Chiều dài quá ngắn ({l2D:F2} m)."
                    });
                    err++;
                }

                // Kiểm tra sai số chiều dài
                if (r.ChieuDai > 0)
                {
                    double diff = Math.Abs(l2D - r.ChieuDai);
                    if (diff > 0.5)
                    {
                        ValidationResults.Add(new ValidationResultItem
                        {
                            Level = ValidationLevel.Warning,
                            RowIndex = r.STT,
                            Category = "Sai số",
                            Message = $"Cống STT {r.STT} ({r.LyTrinh}): Chiều dài tọa độ ({l2D:F2}m) lệch so với thiết kế ({r.ChieuDai:F2}m) là {diff:F2}m."
                        });
                        warn++;
                    }
                }

                // Kiểm tra khoảng cách hộp nối
                if (r.SoHopNoi == 1 && (r.KC_HN1 <= 0 || r.KC_HN1 >= l2D))
                {
                    ValidationResults.Add(new ValidationResultItem
                    {
                        Level = ValidationLevel.Error,
                        RowIndex = r.STT,
                        Category = "Hộp nối",
                        Message = $"Cống STT {r.STT}: Khoảng cách Hộp nối 1 KC_HN1 ({r.KC_HN1}m) không nằm trong chiều dài cống."
                    });
                    err++;
                }
                else if (r.SoHopNoi >= 2)
                {
                    if (r.KC_HN1 + r.KC_HN2 >= l2D)
                    {
                        ValidationResults.Add(new ValidationResultItem
                        {
                            Level = ValidationLevel.Error,
                            RowIndex = r.STT,
                            Category = "Hộp nối",
                            Message = $"Cống STT {r.STT}: Tổng khoảng cách 2 hộp nối (KC_HN1 + KC_HN2) vượt quá chiều dài cống (Va chạm vị trí)."
                        });
                        err++;
                    }
                }
            }

            if (err == 0 && warn == 0 && AllCulvertRows.Count > 0)
            {
                ValidationResults.Add(new ValidationResultItem
                {
                    Level = ValidationLevel.Info,
                    RowIndex = 0,
                    Category = "Kiểm tra",
                    Message = $"Tất cả {AllCulvertRows.Count} cống đều đạt tiêu chuẩn hình học và sẵn sàng dựng mô hình."
                });
                info++;
            }

            TotalErrors = err;
            TotalWarnings = warn;
            TotalInfos = info;
            StatusConclusion = (err == 0) ? "HỢP LỆ (SẴN SÀNG)" : $"CÓ {err} LỖI CẦN XỬ LÝ";
        }
        #endregion

        #region Execution
        private void ExecutePlacement()
        {
            if (AllCulvertRows.Count == 0)
            {
                MessageBox.Show("Vui lòng nạp file Excel trước khi thực hiện.", "Chưa có dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!BarrelComponents.Any(c => c.IsActive && c.SelectedSymbol != null))
            {
                MessageBox.Show("Vui lòng kích hoạt và chọn Family cho Cụm Thân cống ở Khung 2.", "Chưa chọn Family", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RunValidation();
            if (TotalErrors > 0)
            {
                var res = MessageBox.Show($"Dữ liệu đang có {TotalErrors} lỗi hình học. Bạn có chắc chắn muốn tiếp tục bỏ qua các cống lỗi để tạo các cống hợp lệ?", "Cảnh báo lỗi", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;
            }

            ExecutionStatus = "Đang dựng mô hình trong Revit...";

            var rowsToBuild = AllCulvertRows.Where(r => r.IsSelected && !r.HasError).ToList();
            var mode = (SelectedArrayModeIndex == 0) ? CulvertArrayMode.CenterOut : CulvertArrayMode.OneWay;
            bool useSurveyPoint = (SelectedCoordSysIndex == 0);

            _eventHandler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var result = CulvertCoreEngine.BuildAllCulvertsV2(
                    doc,
                    rowsToBuild,
                    BimConfig,
                    CustomBimParameters,
                    ParameterMappings,
                    BarrelComponents.ToList(),
                    OutletComponents.ToList(),
                    ManholeComponents.ToList(),
                    L_Std,
                    Khe_Ho,
                    B_Box,
                    KhoangCachTimDefault,
                    mode,
                    IsCastInPlaceMode,
                    useSurveyPoint,
                    ComponentMaterials);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ExecutionStatus = $"Hoàn tất: Tạo thành công {result.SuccessCount}/{rowsToBuild.Count} cống.";
                    MessageBox.Show($"Quá trình dựng cống hoàn thành!\n- Thành công: {result.SuccessCount}\n- Thất bại: {result.ErrorCount}", "Kết quả thực hiện", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });

            _externalEvent.Raise();
        }
        #endregion
    }
}
