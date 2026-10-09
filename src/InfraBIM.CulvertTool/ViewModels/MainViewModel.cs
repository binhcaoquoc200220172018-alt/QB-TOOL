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

        private string _familyFolderPath = @"C:\Users\ADMIN\Desktop\TEST TOOL\FAMLY REVIT_HTKT";
        public string FamilyFolderPath
        {
            get => _familyFolderPath;
            set => SetProperty(ref _familyFolderPath, value);
        }

        private string _familyLoadStatus = string.Empty;
        public string FamilyLoadStatus
        {
            get => _familyLoadStatus;
            set => SetProperty(ref _familyLoadStatus, value);
        }
        #endregion

        #region Properties - Families & 3 Fixed Component Groups
        public ObservableCollection<FamilySymbolWrapper> AllAvailableFamilies { get; } = new();

        // 4 Cụm cấu kiện cố định chuẩn
        public ObservableCollection<CulvertComponentItem> BarrelComponents { get; } = new();
        public ObservableCollection<CulvertComponentItem> PrecastBarrelComponents { get; } = new();
        public ObservableCollection<CulvertComponentItem> CastInPlaceBarrelComponents { get; } = new();
        public ObservableCollection<CulvertComponentItem> OutletComponents { get; } = new();
        public ObservableCollection<CulvertComponentItem> ApronComponents { get; } = new();
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
        // 1. CỐNG HỘP ĐÚC SẴN (PRECAST)
        private double _lStdPrecast = 1.0;
        public double L_Std_Precast
        {
            get => _lStdPrecast;
            set
            {
                if (SetProperty(ref _lStdPrecast, value))
                {
                    _lStdPrecastText = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(L_Std_Precast_Text));
                    _lStd = value;
                    _lStdText = _lStdPrecastText;
                    OnPropertyChanged(nameof(L_Std));
                    OnPropertyChanged(nameof(L_Std_Text));
                    UpdatePreviewGeometry();
                }
            }
        }

        private string _lStdPrecastText = "1.00";
        public string L_Std_Precast_Text
        {
            get => _lStdPrecastText;
            set
            {
                if (SetProperty(ref _lStdPrecastText, value))
                {
                    if (TryParseFlexible(value, out double v) && v > 0)
                    {
                        _lStdPrecast = v;
                        _lStd = v;
                        UpdatePreviewGeometry();
                    }
                }
            }
        }

        private double _kheHoPrecast = 0.01;
        public double Khe_Ho_Precast
        {
            get => _kheHoPrecast;
            set
            {
                if (SetProperty(ref _kheHoPrecast, value))
                {
                    _kheHoPrecastText = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(Khe_Ho_Precast_Text));
                    _kheHo = value;
                    _kheHoText = _kheHoPrecastText;
                    OnPropertyChanged(nameof(Khe_Ho));
                    OnPropertyChanged(nameof(Khe_Ho_Text));
                    UpdatePreviewGeometry();
                }
            }
        }

        private string _kheHoPrecastText = "0.01";
        public string Khe_Ho_Precast_Text
        {
            get => _kheHoPrecastText;
            set
            {
                if (SetProperty(ref _kheHoPrecastText, value))
                {
                    if (TryParseFlexible(value, out double v) && v >= 0)
                    {
                        _kheHoPrecast = v;
                        _kheHo = v;
                        UpdatePreviewGeometry();
                    }
                }
            }
        }

        private int _selectedArrayModeIndexPrecast = 1;
        public int SelectedArrayModeIndex_Precast
        {
            get => _selectedArrayModeIndexPrecast;
            set
            {
                if (SetProperty(ref _selectedArrayModeIndexPrecast, value))
                {
                    UpdatePreviewGeometry();
                }
            }
        }

        // 2. CỐNG HỘP ĐỔ TẠI CHỖ (CAST-IN-PLACE)
        private double _lStdCastInPlace = 4.0;
        public double L_Std_CastInPlace
        {
            get => _lStdCastInPlace;
            set
            {
                if (SetProperty(ref _lStdCastInPlace, value))
                {
                    _lStdCastInPlaceText = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(L_Std_CastInPlace_Text));
                    UpdatePreviewGeometry();
                }
            }
        }

        private string _lStdCastInPlaceText = "4.00";
        public string L_Std_CastInPlace_Text
        {
            get => _lStdCastInPlaceText;
            set
            {
                if (SetProperty(ref _lStdCastInPlaceText, value))
                {
                    if (TryParseFlexible(value, out double v) && v > 0)
                    {
                        _lStdCastInPlace = v;
                        UpdatePreviewGeometry();
                    }
                }
            }
        }

        private double _kheHoCastInPlace = 0.02;
        public double Khe_Ho_CastInPlace
        {
            get => _kheHoCastInPlace;
            set
            {
                if (SetProperty(ref _kheHoCastInPlace, value))
                {
                    _kheHoCastInPlaceText = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(Khe_Ho_CastInPlace_Text));
                    UpdatePreviewGeometry();
                }
            }
        }

        private string _kheHoCastInPlaceText = "0.02";
        public string Khe_Ho_CastInPlace_Text
        {
            get => _kheHoCastInPlaceText;
            set
            {
                if (SetProperty(ref _kheHoCastInPlaceText, value))
                {
                    if (TryParseFlexible(value, out double v) && v >= 0)
                    {
                        _kheHoCastInPlace = v;
                        UpdatePreviewGeometry();
                    }
                }
            }
        }

        private int _selectedArrayModeIndexCastInPlace = 1;
        public int SelectedArrayModeIndex_CastInPlace
        {
            get => _selectedArrayModeIndexCastInPlace;
            set
            {
                if (SetProperty(ref _selectedArrayModeIndexCastInPlace, value))
                {
                    UpdatePreviewGeometry();
                }
            }
        }

        // Thuộc tính tương thích ngược
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

        #region Properties - Tab 02 Parameter Mapping (Per Family & Per Group)
        public ObservableCollection<CulvertComponentItem> ActiveAssignedFamiliesForTab02 { get; } = new();
        public ObservableCollection<CulvertComponentItem> FilteredAssignedFamiliesForTab02 { get; } = new();

        private string _selectedTab02GroupFilter = "Barrel";
        public string SelectedTab02GroupFilter
        {
            get => _selectedTab02GroupFilter;
            set
            {
                if (SetProperty(ref _selectedTab02GroupFilter, value))
                {
                    OnPropertyChanged(nameof(SelectedTab02GroupName));
                }
            }
        }

        public string SelectedTab02GroupName => _selectedTab02GroupFilter switch
        {
            "Precast" => "🧱 Cống đúc sẵn",
            "CastInPlace" => "🏗️ Cống đổ tại chỗ",
            "Barrel" => "🧱 Thân cống",
            "Outlet" => "🌊 Cửa xả",
            "Apron" => "🛡️ Sân gia cố",
            "Manhole" => "🕳️ Hố ga (Hộp nối)",
            _ => "📁 Tất cả"
        };

        public RelayCommand<string> FilterTab02GroupCommand { get; }

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

        // Tùy chọn lọc tham số Dimensions & Other theo Hình 1, 2, 3 anh đã khoanh đỏ
        private string _tab02ParamCategoryFilter = "All"; // "All", "Dimensions", "Other"
        public string Tab02ParamCategoryFilter
        {
            get => _tab02ParamCategoryFilter;
            set
            {
                if (SetProperty(ref _tab02ParamCategoryFilter, value))
                {
                    FilterParameterMappings();
                }
            }
        }

        // Tùy chọn lọc tham số Dimensions & Visibility & Other & All
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

        private bool _showVisibility = true;
        public bool ShowVisibility
        {
            get => _showVisibility;
            set
            {
                if (SetProperty(ref _showVisibility, value))
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

        private bool _showAllGroups = true;
        public bool ShowAllGroups
        {
            get => _showAllGroups;
            set
            {
                if (SetProperty(ref _showAllGroups, value))
                {
                    FilterParameterMappings();
                }
            }
        }

        // Tùy chọn lọc Type & Instance Parameters (Hình 6: Type vs Instance)
        private bool _showTypeParams = true;
        public bool ShowTypeParams
        {
            get => _showTypeParams;
            set
            {
                if (SetProperty(ref _showTypeParams, value))
                {
                    FilterParameterMappings();
                }
            }
        }

        private bool _showInstanceParams = true;
        public bool ShowInstanceParams
        {
            get => _showInstanceParams;
            set
            {
                if (SetProperty(ref _showInstanceParams, value))
                {
                    FilterParameterMappings();
                }
            }
        }

        private string _paramSearchFilter = string.Empty;
        public string ParamSearchFilter
        {
            get => _paramSearchFilter;
            set
            {
                if (SetProperty(ref _paramSearchFilter, value))
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

        private PreviewViewMode _previewMode = PreviewViewMode.Isometric3D;
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
            set
            {
                if (SetProperty(ref _selectedParamForBatch, value))
                {
                    UpdateBatchTableForSelectedParam();
                }
            }
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
            "Outlet" => "🌊 Cửa xả",
            "Apron" => "🛡️ Sân gia cố",
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
        public RelayCommand BrowseFamilyFolderCommand { get; }
        public RelayCommand LoadFamiliesFromFolderCommand { get; }
        public RelayCommand ScanFamilyParamsCommand { get; }
        public RelayCommand BatchApplyCommand { get; }
        public RelayCommand ApplyAllCustomParamsToStationRangeCommand { get; }
        public RelayCommand CopyCustomParamsToOtherComponentsCommand { get; }
        public RelayCommand SelectAllParamsForCopyCommand { get; }
        public RelayCommand UnselectAllParamsForCopyCommand { get; }
        public RelayCommand ValidateCommand { get; }
        public RelayCommand ExecutePlacementCommand { get; }
        public RelayCommand CloseCommand { get; }

        // Commands for Dynamic Assembly
        public RelayCommand AddComponentCommand { get; }
        public RelayCommand RemoveComponentCommand { get; }
        public RelayCommand<string> ApplyAssemblyTemplateCommand { get; }

        // Commands for Tab 03 Preview & 3D Review
        public RelayCommand Open3DReviewCommand { get; }
        public RelayCommand<string> SetTab02ParamCategoryFilterCommand { get; }
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
            ReloadExcelCommand = new RelayCommand(ReloadExcelData);
            BrowseFamilyFolderCommand = new RelayCommand(BrowseFamilyFolder);
            LoadFamiliesFromFolderCommand = new RelayCommand(ExecuteLoadFamiliesFromFolder);
            ScanFamilyParamsCommand = new RelayCommand(ScanCurrentSelectedFamilyParameters);
            FilterTab02GroupCommand = new RelayCommand<string>(FilterTab02Group);
            BatchApplyCommand = new RelayCommand(ApplyBatchValue);
            ApplyAllCustomParamsToStationRangeCommand = new RelayCommand(ApplyAllCustomParamsToStationRange);
            CopyCustomParamsToOtherComponentsCommand = new RelayCommand(CopyCustomParamsToOtherComponents);
            SelectAllParamsForCopyCommand = new RelayCommand(SelectAllParamsForCopy);
            UnselectAllParamsForCopyCommand = new RelayCommand(UnselectAllParamsForCopy);
            ValidateCommand = new RelayCommand(RunValidation);
            ExecutePlacementCommand = new RelayCommand(ExecutePlacement);
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke());

            // Dynamic Assembly Commands
            AddComponentCommand = new RelayCommand(AddNewComponent);
            RemoveComponentCommand = new RelayCommand(RemoveSelectedComponent);
            ApplyAssemblyTemplateCommand = new RelayCommand<string>(ApplyAssemblyTemplate);

            // Tab 03 Preview & 3D Review Commands
            Open3DReviewCommand = new RelayCommand(Open3DReview);
            SetTab02ParamCategoryFilterCommand = new RelayCommand<string>(filter =>
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    Tab02ParamCategoryFilter = filter;
                }
            });
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

        private void BrowseFamilyFolder()
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Chọn thư mục chứa Family Revit (.rfa) để nạp tự động vào Revit",
                SelectedPath = Directory.Exists(FamilyFolderPath) ? FamilyFolderPath : @"C:\Users\ADMIN\Desktop\TEST TOOL\FAMLY REVIT_HTKT",
                ShowNewFolderButton = false
            };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FamilyFolderPath = dlg.SelectedPath;
                ExecuteLoadFamiliesFromFolder();
            }
        }

        public void ExecuteLoadFamiliesFromFolder()
        {
            if (string.IsNullOrWhiteSpace(FamilyFolderPath) || !Directory.Exists(FamilyFolderPath))
            {
                MessageBox.Show("Thư mục Family không tồn tại hoặc chưa được chọn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string[] rfaFiles = Directory.GetFiles(FamilyFolderPath, "*.rfa", SearchOption.AllDirectories);
            if (rfaFiles.Length == 0)
            {
                MessageBox.Show("Không tìm thấy file .rfa nào trong thư mục đã chọn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FamilyLoadStatus = $"Đang nạp {rfaFiles.Length} Family vào Revit...";

            _eventHandler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null) return;

                int loadedCount = 0;
                int errorCount = 0;

                using (Transaction t = new Transaction(doc, "Load Culvert Families"))
                {
                    t.Start();
                    var loadOptions = new CustomFamilyLoadOptions();
                    foreach (var file in rfaFiles)
                    {
                        try
                        {
                            if (doc.LoadFamily(file, loadOptions, out Family f))
                            {
                                loadedCount++;
                            }
                            else
                            {
                                loadedCount++;
                            }
                        }
                        catch
                        {
                            errorCount++;
                        }
                    }
                    t.Commit();
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    LoadAvailableFamilies();
                    FamilyLoadStatus = $"Đã nạp {loadedCount}/{rfaFiles.Length} Family thành công!";
                    MessageBox.Show($"Đã tự động nạp thành công {loadedCount}/{rfaFiles.Length} Family vào Revit!", "Nạp Family Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });

            _externalEvent.Raise();
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

        private void ReloadExcelData()
        {
            if (string.IsNullOrEmpty(ExcelFilePath) || !File.Exists(ExcelFilePath)) return;

            try
            {
                string curSheet = SelectedSheetName;
                var sheets = ExcelReaderService.GetSheetNames(ExcelFilePath);
                SheetNames.Clear();
                foreach (var s in sheets) SheetNames.Add(s);

                if (!string.IsNullOrEmpty(curSheet) && SheetNames.Contains(curSheet))
                {
                    SelectedSheetName = curSheet;
                }
                else if (SheetNames.Count > 0)
                {
                    SelectedSheetName = SheetNames[0];
                }

                LoadExcelData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đọc lại file Excel: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                string lc = (r.LoaiCong ?? "").ToUpperInvariant();
                string resolved = (r.ResolvedCulvertType ?? "").ToUpperInvariant();

                if (SelectedTypeFilter == "TẤT CẢ")
                {
                    FilteredCulvertRows.Add(r);
                }
                else if (SelectedTypeFilter == "CỐNG HỘP" && (lc.Contains("HOP") || lc.Contains("HỘP") || resolved.Contains("HỘP")))
                {
                    FilteredCulvertRows.Add(r);
                }
                else if (SelectedTypeFilter == "CỐNG TRÒN" && (lc.Contains("TRON") || lc.Contains("TRÒN") || resolved.Contains("TRÒN")))
                {
                    FilteredCulvertRows.Add(r);
                }
                else if (SelectedTypeFilter == "CỐNG ĐÔI" && (r.SoCua >= 2 || resolved.Contains("ĐÔI")))
                {
                    FilteredCulvertRows.Add(r);
                }
            }

            FilterPreviewCulvertRows();
        }

        private FamilySymbolWrapper? FindSymbol(string familyKeyword, string? typeKeyword = null)
        {
            // 1. Khớp chính xác 100% FamilyName trước tiên (ví dụ "TNN_CH_THAN CONG" không nhầm sang "TNN_CH_THAN CONG_2x3x2")
            var exactMatches = AllAvailableFamilies.Where(f =>
                string.Equals(f.Symbol.FamilyName, familyKeyword, StringComparison.OrdinalIgnoreCase)).ToList();

            if (exactMatches.Count > 0)
            {
                if (!string.IsNullOrEmpty(typeKeyword))
                {
                    var tm = exactMatches.FirstOrDefault(f =>
                        f.Symbol.Name.IndexOf(typeKeyword, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (tm != null) return tm;
                }
                return exactMatches.FirstOrDefault();
            }

            // 2. Fallback nếu không có khớp chính xác: tìm kiếm Contains
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

            InitPrecastBarrelComponents(forceRefresh: true);
            InitCastInPlaceBarrelComponents(forceRefresh: true);
            InitOutletComponents(forceRefresh: true);
            InitApronComponents(forceRefresh: true);
            InitManholeComponents(forceRefresh: true);
            ApplyAssemblyTemplate(SelectedCulvertTemplateType ?? "CỐNG HỘP ĐÚC SẴN");
        }

        private void InitPrecastBarrelComponents(bool forceRefresh = false)
        {
            if (!forceRefresh && PrecastBarrelComponents.Count > 0) return;
            foreach (var c in PrecastBarrelComponents) c.PropertyChanged -= OnAssemblyComponentPropertyChanged;
            PrecastBarrelComponents.Clear();

            PrecastBarrelComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cống đúc sẵn",
                CategoryType = "Thân cống hộp đúc sẵn",
                SelectedSymbol = FindSymbol("TNN_CH_THAN CONG", "1.5x1.5") ?? FindSymbol("TNN_CH_THAN CONG"),
                OffsetZ = 0.0,
                Note = "Đốt thân cống hộp đúc sẵn"
            });
            PrecastBarrelComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cống đúc sẵn",
                CategoryType = "Bê tông lót thân cống",
                SelectedSymbol = FindSymbol("TNN_CH_BE TONG LOT") ?? FindSymbol("TNN_CH_DEM CONG"),
                OffsetZ = -0.10,
                Note = "Lớp bê tông lót thân cống đúc sẵn"
            });
            PrecastBarrelComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cống đúc sẵn",
                CategoryType = "Đá dăm đệm thân cống",
                SelectedSymbol = FindSymbol("TNN_CH_DA DAM DEM"),
                OffsetZ = -0.20,
                Note = "Lớp đá dăm đệm thân cống đúc sẵn"
            });

            foreach (var c in PrecastBarrelComponents) c.PropertyChanged += OnAssemblyComponentPropertyChanged;
        }

        private void InitCastInPlaceBarrelComponents(bool forceRefresh = false)
        {
            if (!forceRefresh && CastInPlaceBarrelComponents.Count > 0) return;
            foreach (var c in CastInPlaceBarrelComponents) c.PropertyChanged -= OnAssemblyComponentPropertyChanged;
            CastInPlaceBarrelComponents.Clear();

            CastInPlaceBarrelComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cống đổ tại chỗ",
                CategoryType = "Thân cống hộp đổ tại chỗ",
                SelectedSymbol = FindSymbol("TNN_CH_THAN CONG_2x3x2") ?? FindSymbol("TNN_CH_THAN CONG"),
                OffsetZ = 0.0,
                Note = "Đốt thân cống hộp đổ tại chỗ (2x3x2)"
            });
            CastInPlaceBarrelComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cống đổ tại chỗ",
                CategoryType = "Bê tông lót thân cống",
                SelectedSymbol = FindSymbol("TNN_CH_BE TONG LOT_2x3x2") ?? FindSymbol("TNN_CH_DEM CONG_2x3x2") ?? FindSymbol("TNN_CH_BE TONG LOT"),
                OffsetZ = -0.10,
                Note = "Lớp đệm / bê tông lót thân cống đổ tại chỗ"
            });
            CastInPlaceBarrelComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cống đổ tại chỗ",
                CategoryType = "Đá dăm đệm thân cống",
                SelectedSymbol = FindSymbol("TNN_CH_DA DAM DEM_2x3x2") ?? FindSymbol("TNN_CH_DA DAM DEM"),
                OffsetZ = -0.20,
                Note = "Lớp đá dăm đệm thân cống đổ tại chỗ"
            });

            foreach (var c in CastInPlaceBarrelComponents) c.PropertyChanged += OnAssemblyComponentPropertyChanged;
        }


        private void InitOutletComponents(bool forceRefresh = false)
        {
            if (!forceRefresh && OutletComponents.Count > 0) return;
            OutletComponents.Clear();

            OutletComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cửa xả",
                CategoryType = "Cửa xả - Tường đầu",
                SelectedSymbol = FindSymbol("TNN_CX_SAN CONG", "TUONG DAU") ?? FindSymbol("TNN_CX_TUONG DAU"),
                OffsetZ = 0.0,
                Note = "Tường đầu cửa xả"
            });
            OutletComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cửa xả",
                CategoryType = "Cửa xả - Tường cánh",
                SelectedSymbol = FindSymbol("TNN_CX_SAN CONG", "TUONG CANH") ?? FindSymbol("TNN_CX_TUONG CANH"),
                OffsetZ = 0.0,
                Note = "Tường cánh cửa xả"
            });
            OutletComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cửa xả",
                CategoryType = "Cửa xả - Sân cống",
                SelectedSymbol = FindSymbol("TNN_CX_SAN CONG", "SAN CONG") ?? FindSymbol("TNN_CX_SAN CONG"),
                OffsetZ = 0.0,
                Note = "Sân cống cửa xả"
            });
            OutletComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cửa xả",
                CategoryType = "Cửa xả - Bê tông lót",
                SelectedSymbol = FindSymbol("TNN_CX_SAN CONG", "BE TONG LOT") ?? FindSymbol("TNN_CX_BE TONG LOT"),
                OffsetZ = 0.0,
                Note = "Bê tông lót cửa xả"
            });
            OutletComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Cửa xả",
                CategoryType = "Cửa xả - Đá dăm đệm",
                SelectedSymbol = FindSymbol("TNN_CX_SAN CONG", "DA DAM DEM") ?? FindSymbol("TNN_CX_DA DAM DEM"),
                OffsetZ = 0.0,
                Note = "Lớp đá dăm đệm cửa xả"
            });
        }

        private void InitApronComponents(bool forceRefresh = false)
        {
            if (!forceRefresh && ApronComponents.Count > 0) return;
            ApronComponents.Clear();

            ApronComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Sân gia cố",
                CategoryType = "Sân gia cố - Tấm sân",
                SelectedSymbol = FindSymbol("TNN_CX_SAN GIA CO", "SAN GIA CO") ?? FindSymbol("TNN_CX_SGC_SAN GIA CO") ?? FindSymbol("TNN_CX_SAN GIA CO"),
                OffsetZ = 0.0,
                Note = "Tấm sân gia cố nối dài"
            });
            ApronComponents.Add(new CulvertComponentItem
            {
                IsActive = true,
                GroupType = "Sân gia cố",
                CategoryType = "Sân gia cố - Bê tông lót",
                SelectedSymbol = FindSymbol("TNN_CX_SAN GIA CO", "BE TONG LOT") ?? FindSymbol("TNN_CX_SGC_BE TONG LOT"),
                OffsetZ = 0.0,
                Note = "Bê tông lót sân gia cố"
            });
        }

        private void InitManholeComponents(bool forceRefresh = false)
        {
            if (!forceRefresh && ManholeComponents.Count > 0) return;
            ManholeComponents.Clear();

            var hopNoiSym = FindSymbol("HOP NOI CONG DOC") ?? FindSymbol("HOP NOI");
            if (hopNoiSym != null)
            {
                ManholeComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Hố ga (Hộp nối)",
                    CategoryType = "Hộp nối cống dọc",
                    SelectedSymbol = hopNoiSym,
                    OffsetZ = 0.0,
                    Note = "Hộp nối cống dọc thoát nước"
                });
            }
            else
            {
                ManholeComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Hố ga (Hộp nối)",
                    CategoryType = "Thân hố ga",
                    SelectedSymbol = FindSymbol("TNM_HG_DO TAI CHO") ?? FindSymbol("TNM_HG_DUC SAN") ?? FindSymbol("TNM_HG"),
                    OffsetZ = 0.0,
                    Note = "Thân buồng thu hố ga (hộp nối)"
                });
                ManholeComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Hố ga (Hộp nối)",
                    CategoryType = "Cổ giếng",
                    SelectedSymbol = FindSymbol("TNM_HG_CO GIENG") ?? FindSymbol("CO GIENG"),
                    OffsetZ = 0.50,
                    Note = "Cổ giếng hố ga"
                });
                ManholeComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Hố ga (Hộp nối)",
                    CategoryType = "Khuôn hầm",
                    SelectedSymbol = FindSymbol("TNM_HG_KHUON HAM") ?? FindSymbol("KHUON HAM"),
                    OffsetZ = 0.80,
                    Note = "Khuôn đỡ nắp hầm ga"
                });
                ManholeComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Hố ga (Hộp nối)",
                    CategoryType = "Nắp đan",
                    SelectedSymbol = FindSymbol("TNM_HG_NAP DAN") ?? FindSymbol("NAP DAN"),
                    OffsetZ = 0.90,
                    Note = "Tấm đan nắp đậy hố ga"
                });
                ManholeComponents.Add(new CulvertComponentItem
                {
                    IsActive = true,
                    GroupType = "Hố ga (Hộp nối)",
                    CategoryType = "Bê tông lót hố ga",
                    SelectedSymbol = FindSymbol("TNM_HG_BE TONG LOT") ?? FindSymbol("BE TONG LOT"),
                    OffsetZ = -0.20,
                    Note = "Bê tông lót đáy hố ga"
                });
            }
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

            foreach (var c in BarrelComponents)
            {
                c.PropertyChanged -= OnAssemblyComponentPropertyChanged;
            }
            BarrelComponents.Clear();

            if (type.Contains("ĐỔ TẠI CHỖ") || type.Contains("DO TAI CHO"))
            {
                IsCastInPlaceMode = true;
                IsDoubleCulvertMode = false;
                L_Std = 4.0;
                Khe_Ho = 0.02;

                if (CastInPlaceBarrelComponents.Count == 0)
                {
                    InitCastInPlaceBarrelComponents(forceRefresh: true);
                }
                foreach (var item in CastInPlaceBarrelComponents)
                {
                    BarrelComponents.Add(item);
                }
            }
            else // CỐNG HỘP ĐÚC SẴN (Mặc định)
            {
                IsCastInPlaceMode = false;
                IsDoubleCulvertMode = false;
                L_Std = 1.0;
                Khe_Ho = 0.01;

                if (PrecastBarrelComponents.Count == 0)
                {
                    InitPrecastBarrelComponents(forceRefresh: true);
                }
                foreach (var item in PrecastBarrelComponents)
                {
                    BarrelComponents.Add(item);
                }
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
            foreach (var c in ApronComponents) AssemblyComponents.Add(c);
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

        public void FilterTab02Group(string? filter)
        {
            SelectedTab02GroupFilter = filter ?? "All";
            ApplyTab02FamilyFilter();
        }

        public void ApplyTab02FamilyFilter()
        {
            var oldSelected = SelectedComponentForTab02;
            FilteredAssignedFamiliesForTab02.Clear();

            string f = SelectedTab02GroupFilter ?? "All";

            foreach (var comp in ActiveAssignedFamiliesForTab02)
            {
                bool isPrecast = comp.GroupType == "Cống đúc sẵn" || comp.CategoryType.Contains("đúc sẵn") || PrecastBarrelComponents.Contains(comp);
                bool isCastInPlace = comp.GroupType == "Cống đổ tại chỗ" || comp.CategoryType.Contains("đổ tại chỗ") || CastInPlaceBarrelComponents.Contains(comp);
                bool isOutlet = comp.GroupType == "Cửa xả" || comp.GroupType?.Contains("Cửa") == true || OutletComponents.Contains(comp);
                bool isApron = comp.GroupType == "Sân gia cố" || comp.GroupType?.Contains("Gia cố") == true || comp.GroupType?.Contains("Sân") == true || ApronComponents.Contains(comp);
                bool isManhole = comp.GroupType == "Hố ga (Hộp nối)" || comp.GroupType?.Contains("Hố") == true || comp.GroupType?.Contains("Hộp") == true || ManholeComponents.Contains(comp);

                bool pass = false;
                if (f == "All") pass = true;
                else if (f == "Precast" && isPrecast) pass = true;
                else if (f == "CastInPlace" && isCastInPlace) pass = true;
                else if (f == "Barrel" && (isPrecast || isCastInPlace)) pass = true;
                else if (f == "Outlet" && isOutlet) pass = true;
                else if (f == "Apron" && isApron) pass = true;
                else if (f == "Manhole" && isManhole) pass = true;

                if (pass)
                {
                    FilteredAssignedFamiliesForTab02.Add(comp);
                }
            }

            if (oldSelected != null && FilteredAssignedFamiliesForTab02.Contains(oldSelected))
            {
                SelectedComponentForTab02 = oldSelected;
            }
            else
            {
                SelectedComponentForTab02 = FilteredAssignedFamiliesForTab02.FirstOrDefault();
            }
        }

        public void RefreshActiveAssignedFamiliesForTab02()
        {
            ActiveAssignedFamiliesForTab02.Clear();

            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var allCompsToInspect = new List<CulvertComponentItem>();
            allCompsToInspect.AddRange(PrecastBarrelComponents);
            allCompsToInspect.AddRange(CastInPlaceBarrelComponents);
            allCompsToInspect.AddRange(OutletComponents);
            allCompsToInspect.AddRange(ApronComponents);
            allCompsToInspect.AddRange(ManholeComponents);

            foreach (var comp in allCompsToInspect)
            {
                if (comp.IsActive && comp.SelectedSymbol != null)
                {
                    string fam = comp.SelectedSymbol.Symbol?.FamilyName ?? comp.CategoryType;
                    string key = $"{comp.GroupType}_{fam}_{comp.CategoryType}";
                    if (!seenKeys.Contains(key))
                    {
                        seenKeys.Add(key);
                        ActiveAssignedFamiliesForTab02.Add(comp);
                    }
                }
            }

            ApplyTab02FamilyFilter();
        }
        #endregion

        #region Tab 03 Preview Methods
        public void SetPreviewMode(string? mode)
        {
            if (string.Equals(mode, "Plan", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "Plan2D", StringComparison.OrdinalIgnoreCase))
                PreviewMode = PreviewViewMode.Plan2D;
            else if (string.Equals(mode, "3D", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "Isometric3D", StringComparison.OrdinalIgnoreCase))
                PreviewMode = PreviewViewMode.Isometric3D;
            else
                PreviewMode = PreviewViewMode.Profile2D;
        }

        public void Open3DReview()
        {
            if (SelectedCulvertRowForPreview == null)
            {
                SelectedCulvertRowForPreview = FilteredCulvertRows.FirstOrDefault() ?? AllCulvertRows.FirstOrDefault();
            }
            PreviewMode = PreviewViewMode.Isometric3D;
            UpdatePreviewGeometry();

            var reviewWindow = new Views.Culvert3DPreviewWindow(this);
            try
            {
                if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
                {
                    reviewWindow.Owner = Application.Current.MainWindow;
                }
            }
            catch { }
            reviewWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            reviewWindow.ShowDialog();
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

            // YÊU CẦU 1 (HÌNH 1 & 2): Lấy hình học thực từ Family Revit nếu mô hình đã xuất hoặc có trong Revit Document
            if (CurrentPreviewGeometry != null && Doc != null)
            {
                try
                {
                    if (RevitCulvertMeshExtractor.TryExtractRevitGeometry(Doc, target, out var revitMeshes))
                    {
                        CurrentPreviewGeometry.RealRevitMeshes = revitMeshes;
                    }
                }
                catch { }
            }
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
                else if (filter == "Outlet" && (item.GroupType == "Cửa xả" || item.GroupType?.Contains("Cửa") == true))
                {
                    FilteredComponentMaterials.Add(item);
                }
                else if (filter == "Apron" && (item.GroupType == "Sân gia cố" || item.GroupType?.Contains("Gia cố") == true || item.GroupType?.Contains("Sân") == true))
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
                    "Outlet" => "Cửa xả",
                    "Apron" => "Sân gia cố",
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
        private bool _isSyncingClusterParams = false;

        private void OnParameterItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingClusterParams) return;
            if (e.PropertyName != nameof(ParameterMappingItem.CustomValue)) return;
            if (sender is not ParameterMappingItem changedItem) return;

            // TUYỆT ĐỐI KHÔNG sao chép / đồng bộ tham số kiểu Yes/No
            if (changedItem.IsYesNoParameter) return;

            // Tự động đồng bộ sang các cấu kiện khác trong cùng cụm (Cửa xả hoặc Sân gia cố)
            SyncCustomParamToCluster(changedItem);
        }

        private void EnsureComponentParametersScanned(CulvertComponentItem comp)
        {
            if (comp?.SelectedSymbol?.Symbol == null || Doc == null) return;
            string cat = comp.CategoryType;
            var sym = comp.SelectedSymbol.Symbol;

            bool hasParams = ParameterMappings.Any(m => string.Equals(m.CategoryName, cat, StringComparison.OrdinalIgnoreCase));
            if (!hasParams)
            {
                var scanned = FamilyParameterScannerService.ScanParametersForFamily(Doc, sym, cat);
                foreach (var p in scanned)
                {
                    // Tham số Yes/No mặc định không tick chọn sao chép
                    if (p.IsYesNoParameter)
                    {
                        p.IsSelected = false;
                    }
                    p.PropertyChanged += OnParameterItemPropertyChanged;
                    ParameterMappings.Add(p);
                }
            }
        }

        private void SyncCustomParamToCluster(ParameterMappingItem srcItem)
        {
            if (SelectedComponentForTab02 == null) return;
            var srcComp = SelectedComponentForTab02;
            string srcGroup = srcComp.GroupType ?? string.Empty;
            string srcCat = srcComp.CategoryType ?? string.Empty;

            List<CulvertComponentItem> targetComps = new();
            if (srcGroup.Contains("Cửa") || srcCat.Contains("Cửa") || OutletComponents.Contains(srcComp))
            {
                targetComps = OutletComponents.Where(c => c != srcComp && c.IsActive && c.SelectedSymbol != null).ToList();
            }
            else if (srcGroup.Contains("Gia cố") || srcCat.Contains("Gia cố") || srcCat.Contains("Sân") || ApronComponents.Contains(srcComp))
            {
                targetComps = ApronComponents.Where(c => c != srcComp && c.IsActive && c.SelectedSymbol != null).ToList();
            }
            else
            {
                return; // Chỉ áp dụng tự động đồng bộ cho Cụm Cửa xả và Cụm Sân gia cố theo yêu cầu
            }

            if (targetComps.Count == 0) return;

            _isSyncingClusterParams = true;
            try
            {
                foreach (var tgt in targetComps)
                {
                    EnsureComponentParametersScanned(tgt);
                    string tgtCat = tgt.CategoryType;

                    var tgtItem = ParameterMappings.FirstOrDefault(m =>
                        string.Equals(m.CategoryName, tgtCat, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(m.InternalName, srcItem.InternalName, StringComparison.OrdinalIgnoreCase));

                    if (tgtItem != null && !tgtItem.IsYesNoParameter)
                    {
                        tgtItem.CustomValue = srcItem.CustomValue;
                    }
                }
            }
            finally
            {
                _isSyncingClusterParams = false;
            }
        }

        private void OnSelectedFamilyForTab02Changed()
        {
            if (SelectedComponentForTab02?.SelectedSymbol?.Symbol == null)
            {
                FilterParameterMappings();
                return;
            }

            EnsureComponentParametersScanned(SelectedComponentForTab02);
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
                // Khớp chính xác theo CategoryName của cấu kiện đang chọn (nếu có), fallback theo FamilyName
                bool match = !string.IsNullOrEmpty(m.CategoryName)
                    ? string.Equals(m.CategoryName, cat, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(m.FamilyName, sym.FamilyName, StringComparison.OrdinalIgnoreCase);

                if (match)
                {
                    // 1. Lọc theo Phân nhóm chuẩn Revit trong khung đỏ (Hình 1, 2, 3: Dimensions vs Other)
                    if (string.Equals(Tab02ParamCategoryFilter, "Dimensions", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!m.IsDimension) continue;
                    }
                    else if (string.Equals(Tab02ParamCategoryFilter, "Other", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!m.IsOther) continue;
                        // YÊU CẦU 3 (HÌNH 4): Trong mục Other chỉ hiển thị kiểu dữ liệu Length và Yes/No thôi
                        bool isLenOrYesNo = m.DataType.Contains("Length") || m.DataType.Contains("Yes/No");
                        if (!isLenOrYesNo) continue;
                    }
                    else // "All" - hiển thị tất cả các tham số thuộc khung đỏ của người dùng (Dimensions hoặc Other)
                    {
                        if (!m.IsDimension && !m.IsOther) continue;
                        if (m.IsOther && !m.DataType.Contains("Length") && !m.DataType.Contains("Yes/No")) continue;
                    }

                    // 2. Lọc theo Phân loại Type / Instance (nếu người dùng có tick chọn)
                    if (ShowTypeParams && !ShowInstanceParams && m.IsInstance) continue;
                    if (!ShowTypeParams && ShowInstanceParams && !m.IsInstance) continue;

                    // 3. Lọc theo từ khóa tìm kiếm (nếu người dùng nhập)
                    if (!string.IsNullOrWhiteSpace(ParamSearchFilter))
                    {
                        string q = ParamSearchFilter.Trim().ToLowerInvariant();
                        bool matchSearch = m.InternalName.ToLowerInvariant().Contains(q) ||
                                           m.GroupName.ToLowerInvariant().Contains(q) ||
                                           m.MappedField.ToLowerInvariant().Contains(q);
                        if (!matchSearch) continue;
                    }

                    FilteredParameterMappings.Add(m);
                }
            }

            SelectedParamForBatch = FilteredParameterMappings.FirstOrDefault();
            UpdateBatchTableForSelectedParam();
        }

        public void UpdateBatchTableForSelectedParam()
        {
            string pName = SelectedParamForBatch?.InternalName ?? string.Empty;
            string defVal = SelectedParamForBatch?.CustomValue ?? string.Empty;
            foreach (var r in AllCulvertRows)
            {
                r.ActiveParamName = pName;
                r.ActiveParamValue = r.GetParamOverride(pName, defVal);
            }
            try
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(FilteredCulvertRows);
                view?.Refresh();
            }
            catch { }
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

            _eventHandler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var scanned = FamilyParameterScannerService.ScanParametersForFamily(doc, sym, cat);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Xóa tham số cũ của cấu kiện này để quét mới nhất
                    var toRemove = ParameterMappings.Where(m => string.Equals(m.CategoryName, cat, StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var item in toRemove) ParameterMappings.Remove(item);

                    foreach (var p in scanned)
                    {
                        if (p.IsYesNoParameter)
                        {
                            p.IsSelected = false;
                        }
                        p.PropertyChanged += OnParameterItemPropertyChanged;
                        ParameterMappings.Add(p);
                    }

                    FilterParameterMappings();

                    int typeCount = scanned.Count(p => !p.IsInstance);
                    int instCount = scanned.Count(p => p.IsInstance);
                    int dimCount = scanned.Count(p => p.IsDimension);
                    int visCount = scanned.Count(p => p.IsVisibility);
                    int otherCount = scanned.Count(p => p.IsOther);
                    MessageBox.Show($"Đã quét thành công Family '{sym.FamilyName}' ({cat}):\n- Tổng số: {scanned.Count} tham số\n- Tham số Loại (Type): {typeCount} tham số\n- Tham số Biến thể (Instance): {instCount} tham số\n- Nhóm Kích thước (Dimensions): {dimCount} tham số\n- Nhóm Ẩn hiện (Visibility): {visCount} tham số\n- Nhóm Khác (Other): {otherCount} tham số.", "Quét hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });

            _externalEvent.Raise();
        }

        private void ApplyBatchValue()
        {
            if (SelectedParamForBatch == null || string.IsNullOrEmpty(BatchApplyValue))
            {
                MessageBox.Show("Vui lòng chọn tham số và nhập giá trị cần gán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedParamForBatch.CustomValue = BatchApplyValue;
            string pName = SelectedParamForBatch.InternalName;

            int count = 0;
            foreach (var r in AllCulvertRows)
            {
                if (r.STT >= BatchFromSTT && r.STT <= BatchToSTT)
                {
                    r.SetParamOverride(pName, BatchApplyValue);
                    count++;
                }
            }

            UpdateBatchTableForSelectedParam();

            MessageBox.Show($"Đã gán giá trị '{BatchApplyValue}' cho tham số '{pName}' của Family '{SelectedParamForBatch.FamilyName}' (Phạm vi cống STT {BatchFromSTT} - {BatchToSTT}, {count} cống).", "Hoàn thành", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ApplyAllCustomParamsToStationRange()
        {
            var paramsToApply = FilteredParameterMappings
                .Where(p => p.IsSelected && !string.IsNullOrWhiteSpace(p.CustomValue))
                .ToList();

            if (paramsToApply.Count == 0)
            {
                MessageBox.Show("Không có tham số nào có giá trị tùy chỉnh để gán cho các lý trình.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int count = 0;
            foreach (var r in AllCulvertRows)
            {
                if (r.STT >= BatchFromSTT && r.STT <= BatchToSTT)
                {
                    foreach (var p in paramsToApply)
                    {
                        r.SetParamOverride(p.InternalName, p.CustomValue);
                    }
                    count++;
                }
            }

            UpdateBatchTableForSelectedParam();
            MessageBox.Show($"Đã gán thành công {paramsToApply.Count} tham số tùy biến cho {count} lý trình (Phạm vi STT {BatchFromSTT} đến {BatchToSTT}).", "Hoàn tất gán tham số", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void SelectAllParamsForCopy()
        {
            foreach (var m in FilteredParameterMappings)
            {
                // Chỉ chọn các tham số hình học/kích thước, tuyệt đối không chọn Yes/No
                if (!m.IsYesNoParameter)
                {
                    m.IsSelected = true;
                }
                else
                {
                    m.IsSelected = false;
                }
            }
        }

        public void UnselectAllParamsForCopy()
        {
            foreach (var m in FilteredParameterMappings)
            {
                m.IsSelected = false;
            }
        }

        public void CopyCustomParamsToOtherComponents()
        {
            if (SelectedComponentForTab02?.SelectedSymbol?.Symbol == null)
            {
                MessageBox.Show("Vui lòng chọn một cấu kiện nguồn trước khi thực hiện sao chép.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var srcComp = SelectedComponentForTab02;
            string srcGroup = srcComp.GroupType ?? string.Empty;
            string srcCat = srcComp.CategoryType ?? string.Empty;

            // Xác định danh sách các cấu kiện mục tiêu trong cùng cụm (Cửa xả, Sân gia cố, Cống đúc sẵn, Cống đổ tại chỗ, v.v.)
            List<CulvertComponentItem> targetComps = new();
            if (srcGroup.Contains("Cửa") || srcCat.Contains("Cửa") || OutletComponents.Contains(srcComp))
            {
                targetComps = OutletComponents.Where(c => c != srcComp && c.IsActive && c.SelectedSymbol != null).ToList();
                srcGroup = "Cửa xả";
            }
            else if (srcGroup.Contains("Gia cố") || srcCat.Contains("Gia cố") || srcCat.Contains("Sân") || ApronComponents.Contains(srcComp))
            {
                targetComps = ApronComponents.Where(c => c != srcComp && c.IsActive && c.SelectedSymbol != null).ToList();
                srcGroup = "Sân gia cố";
            }
            else if (PrecastBarrelComponents.Contains(srcComp))
            {
                targetComps = PrecastBarrelComponents.Where(c => c != srcComp && c.IsActive && c.SelectedSymbol != null).ToList();
                srcGroup = "Cống đúc sẵn";
            }
            else if (CastInPlaceBarrelComponents.Contains(srcComp))
            {
                targetComps = CastInPlaceBarrelComponents.Where(c => c != srcComp && c.IsActive && c.SelectedSymbol != null).ToList();
                srcGroup = "Cống đổ tại chỗ";
            }
            else
            {
                targetComps = ActiveAssignedFamiliesForTab02.Where(c => c != srcComp && c.GroupType == srcGroup && c.IsActive && c.SelectedSymbol != null).ToList();
            }

            if (targetComps.Count == 0)
            {
                MessageBox.Show($"Không tìm thấy cấu kiện khác trong cùng cụm '{srcGroup}' để sao chép tham số.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Lấy danh sách tham số nguồn: CHỈ LẤY THAM SỐ HÌNH HỌC/KÍCH THƯỚC, TUYỆT ĐỐI LOẠI TRỪ YES/NO
            var candidateItems = FilteredParameterMappings
                .Where(m => !m.IsYesNoParameter && !string.IsNullOrWhiteSpace(m.CustomValue))
                .ToList();

            var itemsToCopy = candidateItems.Where(m => m.IsSelected).ToList();
            if (itemsToCopy.Count == 0)
            {
                itemsToCopy = candidateItems;
            }

            if (itemsToCopy.Count == 0)
            {
                MessageBox.Show("Không có tham số hình học nào có giá trị tùy biến để sao chép.\n(Lưu ý: Các tham số kiểu Yes/No như CX_BE TONG LOT, CX_DA DAM DEM... được tự động loại trừ để bảo toàn mô hình).", "Chưa có thông số hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isSyncingClusterParams = true;
            int copiedParams = 0;
            try
            {
                foreach (var tgt in targetComps)
                {
                    EnsureComponentParametersScanned(tgt);
                    string tgtCat = tgt.CategoryType;

                    foreach (var srcItem in itemsToCopy)
                    {
                        var tgtItem = ParameterMappings.FirstOrDefault(m =>
                            string.Equals(m.CategoryName, tgtCat, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(m.InternalName, srcItem.InternalName, StringComparison.OrdinalIgnoreCase));

                        if (tgtItem != null && !tgtItem.IsYesNoParameter)
                        {
                            tgtItem.CustomValue = srcItem.CustomValue;
                            tgtItem.IsSelected = true;
                            copiedParams++;
                        }
                    }
                }
            }
            finally
            {
                _isSyncingClusterParams = false;
            }

            FilterParameterMappings();
            MessageBox.Show($"Đã sao chép thành công {itemsToCopy.Count} tham số hình học sang {targetComps.Count} cấu kiện khác trong cụm '{srcGroup}'!\n(Đã tự động loại trừ toàn bộ tham số kiểu Yes/No để bảo toàn hiển thị và cấu trúc Family).\n\nDanh sách các cấu kiện đã cập nhật:\n• {string.Join("\n• ", targetComps.Select(t => t.CategoryType))}", "Sao chép hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
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

            bool hasValidBarrel = PrecastBarrelComponents.Any(c => c.IsActive && c.SelectedSymbol != null) ||
                                  CastInPlaceBarrelComponents.Any(c => c.IsActive && c.SelectedSymbol != null);
            if (!hasValidBarrel)
            {
                MessageBox.Show("Vui lòng kích hoạt và chọn Family cho Cống đúc sẵn hoặc Cống đổ tại chỗ ở Khung 2.", "Chưa chọn Family", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            var modePrecast = (SelectedArrayModeIndex_Precast == 0) ? CulvertArrayMode.CenterOut : CulvertArrayMode.OneWay;
            var modeCastInPlace = (SelectedArrayModeIndex_CastInPlace == 0) ? CulvertArrayMode.CenterOut : CulvertArrayMode.OneWay;
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
                    PrecastBarrelComponents.ToList(),
                    CastInPlaceBarrelComponents.ToList(),
                    OutletComponents.ToList(),
                    ApronComponents.ToList(),
                    ManholeComponents.ToList(),
                    L_Std_Precast,
                    Khe_Ho_Precast,
                    modePrecast,
                    L_Std_CastInPlace,
                    Khe_Ho_CastInPlace,
                    modeCastInPlace,
                    B_Box,
                    KhoangCachTimDefault,
                    useSurveyPoint,
                    ComponentMaterials);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ExecutionStatus = $"Hoàn tất: Tạo thành công {result.SuccessCount}/{rowsToBuild.Count} cống.";
                    if (result.ErrorCount > 0)
                    {
                        string errSummary = string.Join("\n", result.Logs.Where(l => l.Contains("❌")));
                        MessageBox.Show($"Quá trình dựng cống hoàn thành!\n- Thành công: {result.SuccessCount}\n- Thất bại: {result.ErrorCount}\n\nChi tiết lỗi:\n{errSummary}\n\n(Xem thêm log đầy đủ tại: culvert_build_error.log)", "Kết quả thực hiện", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        MessageBox.Show($"Quá trình dựng cống hoàn thành!\n- Thành công: {result.SuccessCount}\n- Thất bại: {result.ErrorCount}", "Kết quả thực hiện", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    // Tự động làm mới Preview 3D để trích xuất hình học thực từ các đối tượng vừa tạo trong Revit
                    try { UpdatePreviewGeometry(); } catch { }
                });
            });

            _externalEvent.Raise();
        }
        #endregion
    }

    /// <summary>
    /// Xử lý ghi đè Family khi nạp tự động vào Revit Document
    /// </summary>
    public class CustomFamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
}
