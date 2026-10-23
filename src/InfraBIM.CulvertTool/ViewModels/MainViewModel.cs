using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using InfraBIM.CulvertTool.Models;
using InfraBIM.CulvertTool.Services;
using InfraBIM.CulvertTool.Views;
using Microsoft.Win32;

namespace InfraBIM.CulvertTool.ViewModels;

public class MainViewModel : ViewModelBase
{
	private readonly UIApplication _uiApp;

	private readonly ExternalEvent _externalEvent;

	private readonly RevitExternalEventHandler _eventHandler;

	private string _selectedTypeFilter = "TẤT CẢ";

	private string _excelFilePath = string.Empty;

	private string _selectedSheetName = string.Empty;

	private int _selectedCoordSysIndex;

	private int _selectedDuplicateOptionIndex;

	private string _familyFolderPath = "C:\\Users\\ADMIN\\Desktop\\TEST TOOL\\FAMLY REVIT_HTKT";

	private string _familyLoadStatus = string.Empty;

	private CulvertTypeProfile? _selectedCastInPlaceProfile;

	private bool _isTechnicalCulvertMode;

	private CulvertComponentItem? _selectedAssemblyComponent;

	private string _selectedCulvertTemplateType = "CỐNG HỘP ĐÚC SẴN";

	private double _lStdPrecast = 1.0;

	private string _lStdPrecastText = "1.00";

	private double _kheHoPrecast = 0.01;

	private string _kheHoPrecastText = "0.01";

	private int _selectedArrayModeIndexPrecast = 1;

	private double _lStdCastInPlace = 4.0;

	private string _lStdCastInPlaceText = "4.00";

	private double _kheHoCastInPlace = 0.02;

	private string _kheHoCastInPlaceText = "0.02";

	private int _selectedArrayModeIndexCastInPlace = 1;

	private double _lStdTechnical = 1.0;

	private string _lStdTechnicalText = "1.00";

	private double _kheHoTechnical = 0.01;

	private string _kheHoTechnicalText = "0.01";

	private int _selectedArrayModeIndexTechnical;

	private double _lStd = 1.0;

	private string _lStdText = "1.00";

	private double _kheHo = 0.01;

	private string _kheHoText = "0.01";

	private double _khoangCachTimDefault = 2.0;

	private string _khoangCachTimText = "2.00";

	private double _bBox = 1.5;

	private string _bBoxText = "1.50";

	private bool _isDoubleCulvertMode;

	private bool _isCastInPlaceMode;

	private int _selectedArrayModeIndex;

	private string _selectedTab02GroupFilter = "Barrel";

	private CulvertTypeProfile? _selectedCastInPlaceProfileForTab02;

	private CulvertComponentItem? _selectedComponentForTab02;

	private string _tab02ParamCategoryFilter = "All";

	private bool _showDimensions = true;

	private bool _showVisibility = true;

	private bool _showOther = true;

	private bool _showAllGroups = true;

	private bool _showTypeParams = true;

	private bool _showInstanceParams = true;

	private string _paramSearchFilter = string.Empty;

	private CulvertRowData? _selectedCulvertRowForInspector;

	private CustomBimParameterItem? _selectedCustomBimParameter;

	private string _selectedCulvertTypeForTab04 = "TẤT CẢ";

	private CulvertTypeProfile? _selectedCastInPlaceProfileForTab04;

	private readonly Dictionary<int, List<(MeshGeometry3D Mesh, System.Windows.Media.Brush Brush)>> _cachedRealMeshes = new Dictionary<int, List<(MeshGeometry3D, System.Windows.Media.Brush)>>();

	private CulvertRowData? _selectedCulvertRowForPreview;

	private CulvertPreviewGeometry? _currentPreviewGeometry;

	private PreviewViewMode _previewMode = PreviewViewMode.Isometric3D;

	private string _previewSearchText = string.Empty;

	private ParameterMappingItem? _selectedParamForBatch;

	private string _batchApplyValue = string.Empty;

	private int _batchFromSTT = 1;

	private int _batchToSTT = 999;

	private string _statusConclusion = "CHƯA KIỂM TRA";

	private int _totalErrors;

	private int _totalWarnings;

	private int _totalInfos;

	private string _executionStatus = "Sẵn sàng";

	private string _selectedCulvertTypeForTab03 = "TẤT CẢ";

	private CulvertTypeProfile? _selectedCastInPlaceProfileForTab03;

	private string _selectedMaterialGroupFilter = "All";

	private CulvertMaterialItem? _selectedMaterialItem;

	private string _selectedItemsSummaryText = "";

	private string _applyToSelectedGroupButtonText = "⚡ ÁP DỤNG VẬT LIỆU & MÀU CHO CÁC DÒNG ĐANG CHỌN";

	private Culvert3DPreviewWindow? _activeReviewWindow;

	private bool _isUpdatingInspector;

	private bool _isInitializing = true;

	private bool _isFilteringTab02;

	public Document? Doc => _uiApp?.ActiveUIDocument?.Document;

	public string SelectedTypeFilter
	{
		get
		{
			return _selectedTypeFilter;
		}
		set
		{
			if (SetProperty(ref _selectedTypeFilter, value, "SelectedTypeFilter"))
			{
				FilterCulvertRows();
			}
		}
	}

	public string ExcelFilePath
	{
		get
		{
			return _excelFilePath;
		}
		set
		{
			SetProperty(ref _excelFilePath, value, "ExcelFilePath");
		}
	}

	public ObservableCollection<string> SheetNames { get; } = new ObservableCollection<string>();

	public string SelectedSheetName
	{
		get
		{
			return _selectedSheetName;
		}
		set
		{
			if (SetProperty(ref _selectedSheetName, value, "SelectedSheetName") && !string.IsNullOrEmpty(value))
			{
				SaveCurrentSheetOverrides();
				UpdateModeFromSheetName(value);
				LoadExcelData();
				RestoreCurrentSheetOverrides();
				UpdateInspectorCulvertRows();
				RefreshInspectorParameterMappings();
			}
		}
	}

	public ObservableCollection<string> CoordinateSystems { get; } = new ObservableCollection<string> { "VN2000 sang Revit (Survey Point + True North)", "Tọa độ cục bộ Revit (Local - Không bù trừ)" };

	public int SelectedCoordSysIndex
	{
		get
		{
			return _selectedCoordSysIndex;
		}
		set
		{
			SetProperty(ref _selectedCoordSysIndex, value, "SelectedCoordSysIndex");
		}
	}

	public ObservableCollection<string> DuplicateOptions { get; } = new ObservableCollection<string> { "Cập nhật theo Lý trình", "Bỏ qua dòng trùng" };

	public int SelectedDuplicateOptionIndex
	{
		get
		{
			return _selectedDuplicateOptionIndex;
		}
		set
		{
			SetProperty(ref _selectedDuplicateOptionIndex, value, "SelectedDuplicateOptionIndex");
		}
	}

	public string FamilyFolderPath
	{
		get
		{
			return _familyFolderPath;
		}
		set
		{
			SetProperty(ref _familyFolderPath, value, "FamilyFolderPath");
		}
	}

	public string FamilyLoadStatus
	{
		get
		{
			return _familyLoadStatus;
		}
		set
		{
			SetProperty(ref _familyLoadStatus, value, "FamilyLoadStatus");
		}
	}

	public ObservableCollection<FamilySymbolWrapper> AllAvailableFamilies { get; } = new ObservableCollection<FamilySymbolWrapper>();

	public ObservableCollection<CulvertComponentItem> BarrelComponents { get; } = new ObservableCollection<CulvertComponentItem>();

	public ObservableCollection<CulvertComponentItem> PrecastBarrelComponents { get; } = new ObservableCollection<CulvertComponentItem>();

	public ObservableCollection<CulvertComponentItem> CastInPlaceBarrelComponents { get; } = new ObservableCollection<CulvertComponentItem>();

	public ObservableCollection<CulvertTypeProfile> CastInPlaceProfiles { get; } = new ObservableCollection<CulvertTypeProfile>();

	public CulvertTypeProfile? SelectedCastInPlaceProfile
	{
		get
		{
			return _selectedCastInPlaceProfile;
		}
		set
		{
			if (_selectedCastInPlaceProfile == value)
			{
				return;
			}
			if (_selectedCastInPlaceProfile != null)
			{
				_selectedCastInPlaceProfile.PropertyChanged -= OnCastInPlaceProfilePropertyChanged;
			}
			if (SetProperty(ref _selectedCastInPlaceProfile, value, "SelectedCastInPlaceProfile"))
			{
				if (_selectedCastInPlaceProfile != null)
				{
					_selectedCastInPlaceProfile.PropertyChanged += OnCastInPlaceProfilePropertyChanged;
					_lStdCastInPlace = _selectedCastInPlaceProfile.L_Std;
					_lStdCastInPlaceText = _selectedCastInPlaceProfile.L_Std_Text;
					_kheHoCastInPlace = _selectedCastInPlaceProfile.Khe_Ho;
					_kheHoCastInPlaceText = _selectedCastInPlaceProfile.Khe_Ho_Text;
					_selectedArrayModeIndexCastInPlace = _selectedCastInPlaceProfile.SelectedArrayModeIndex;
					OnPropertyChanged("L_Std_CastInPlace");
					OnPropertyChanged("L_Std_CastInPlace_Text");
					OnPropertyChanged("Khe_Ho_CastInPlace");
					OnPropertyChanged("Khe_Ho_CastInPlace_Text");
				}
				SyncCastInPlaceBarrelComponents();
				if (!_isInitializing)
				{
					UpdatePreviewGeometry();
					RefreshActiveAssignedFamiliesForTab02();
					ApplyTab02FamilyFilter();
				}
			}
		}
	}

	public ObservableCollection<CulvertComponentItem> TechnicalBarrelComponents { get; } = new ObservableCollection<CulvertComponentItem>();

	public ObservableCollection<CulvertComponentItem> OutletComponents { get; } = new ObservableCollection<CulvertComponentItem>();

	public ObservableCollection<CulvertComponentItem> ApronComponents { get; } = new ObservableCollection<CulvertComponentItem>();

	public ObservableCollection<CulvertComponentItem> ManholeComponents { get; } = new ObservableCollection<CulvertComponentItem>();

	public ObservableCollection<CulvertComponentItem> TechnicalManholeComponents { get; } = new ObservableCollection<CulvertComponentItem>();

	public bool IsTechnicalCulvertMode
	{
		get
		{
			return _isTechnicalCulvertMode;
		}
		set
		{
			if (SetProperty(ref _isTechnicalCulvertMode, value, "IsTechnicalCulvertMode"))
			{
				OnPropertyChanged("IsNormalCulvertMode");
			}
		}
	}

	public bool IsNormalCulvertMode => !IsTechnicalCulvertMode;

	public ObservableCollection<CulvertComponentItem> AssemblyComponents { get; } = new ObservableCollection<CulvertComponentItem>();

	public CulvertComponentItem? SelectedAssemblyComponent
	{
		get
		{
			return _selectedAssemblyComponent;
		}
		set
		{
			SetProperty(ref _selectedAssemblyComponent, value, "SelectedAssemblyComponent");
		}
	}

	public string SelectedCulvertTemplateType
	{
		get
		{
			return _selectedCulvertTemplateType;
		}
		set
		{
			SetProperty(ref _selectedCulvertTemplateType, value, "SelectedCulvertTemplateType");
		}
	}

	public double L_Std_Precast
	{
		get
		{
			return _lStdPrecast;
		}
		set
		{
			if (SetProperty(ref _lStdPrecast, value, "L_Std_Precast"))
			{
				_lStdPrecastText = value.ToString("0.##", CultureInfo.InvariantCulture);
				OnPropertyChanged("L_Std_Precast_Text");
				_lStd = value;
				_lStdText = _lStdPrecastText;
				OnPropertyChanged("L_Std");
				OnPropertyChanged("L_Std_Text");
				UpdatePreviewGeometry();
			}
		}
	}

	public string L_Std_Precast_Text
	{
		get
		{
			return _lStdPrecastText;
		}
		set
		{
			if (SetProperty(ref _lStdPrecastText, value, "L_Std_Precast_Text") && TryParseFlexible(value, out var value2) && value2 > 0.0)
			{
				_lStdPrecast = value2;
				_lStd = value2;
				UpdatePreviewGeometry();
			}
		}
	}

	public double Khe_Ho_Precast
	{
		get
		{
			return _kheHoPrecast;
		}
		set
		{
			if (SetProperty(ref _kheHoPrecast, value, "Khe_Ho_Precast"))
			{
				_kheHoPrecastText = value.ToString("0.###", CultureInfo.InvariantCulture);
				OnPropertyChanged("Khe_Ho_Precast_Text");
				_kheHo = value;
				_kheHoText = _kheHoPrecastText;
				OnPropertyChanged("Khe_Ho");
				OnPropertyChanged("Khe_Ho_Text");
				UpdatePreviewGeometry();
			}
		}
	}

	public string Khe_Ho_Precast_Text
	{
		get
		{
			return _kheHoPrecastText;
		}
		set
		{
			if (SetProperty(ref _kheHoPrecastText, value, "Khe_Ho_Precast_Text") && TryParseFlexible(value, out var value2) && value2 >= 0.0)
			{
				_kheHoPrecast = value2;
				_kheHo = value2;
				UpdatePreviewGeometry();
			}
		}
	}

	public int SelectedArrayModeIndex_Precast
	{
		get
		{
			return _selectedArrayModeIndexPrecast;
		}
		set
		{
			if (SetProperty(ref _selectedArrayModeIndexPrecast, value, "SelectedArrayModeIndex_Precast"))
			{
				UpdatePreviewGeometry();
			}
		}
	}

	public double L_Std_CastInPlace
	{
		get
		{
			return _lStdCastInPlace;
		}
		set
		{
			if (SetProperty(ref _lStdCastInPlace, value, "L_Std_CastInPlace"))
			{
				_lStdCastInPlaceText = value.ToString("0.##", CultureInfo.InvariantCulture);
				OnPropertyChanged("L_Std_CastInPlace_Text");
				UpdatePreviewGeometry();
			}
		}
	}

	public string L_Std_CastInPlace_Text
	{
		get
		{
			return _lStdCastInPlaceText;
		}
		set
		{
			if (SetProperty(ref _lStdCastInPlaceText, value, "L_Std_CastInPlace_Text") && TryParseFlexible(value, out var value2) && value2 > 0.0)
			{
				_lStdCastInPlace = value2;
				UpdatePreviewGeometry();
			}
		}
	}

	public double Khe_Ho_CastInPlace
	{
		get
		{
			return _kheHoCastInPlace;
		}
		set
		{
			if (SetProperty(ref _kheHoCastInPlace, value, "Khe_Ho_CastInPlace"))
			{
				_kheHoCastInPlaceText = value.ToString("0.###", CultureInfo.InvariantCulture);
				OnPropertyChanged("Khe_Ho_CastInPlace_Text");
				UpdatePreviewGeometry();
			}
		}
	}

	public string Khe_Ho_CastInPlace_Text
	{
		get
		{
			return _kheHoCastInPlaceText;
		}
		set
		{
			if (SetProperty(ref _kheHoCastInPlaceText, value, "Khe_Ho_CastInPlace_Text") && TryParseFlexible(value, out var value2) && value2 >= 0.0)
			{
				_kheHoCastInPlace = value2;
				UpdatePreviewGeometry();
			}
		}
	}

	public int SelectedArrayModeIndex_CastInPlace
	{
		get
		{
			return _selectedArrayModeIndexCastInPlace;
		}
		set
		{
			if (SetProperty(ref _selectedArrayModeIndexCastInPlace, value, "SelectedArrayModeIndex_CastInPlace"))
			{
				UpdatePreviewGeometry();
			}
		}
	}

	public double L_Std_Technical
	{
		get
		{
			return _lStdTechnical;
		}
		set
		{
			if (SetProperty(ref _lStdTechnical, value, "L_Std_Technical"))
			{
				_lStdTechnicalText = value.ToString("0.##", CultureInfo.InvariantCulture);
				OnPropertyChanged("L_Std_Technical_Text");
				UpdatePreviewGeometry();
			}
		}
	}

	public string L_Std_Technical_Text
	{
		get
		{
			return _lStdTechnicalText;
		}
		set
		{
			if (SetProperty(ref _lStdTechnicalText, value, "L_Std_Technical_Text") && TryParseFlexible(value, out var value2) && value2 > 0.0)
			{
				_lStdTechnical = value2;
				UpdatePreviewGeometry();
			}
		}
	}

	public double Khe_Ho_Technical
	{
		get
		{
			return _kheHoTechnical;
		}
		set
		{
			if (SetProperty(ref _kheHoTechnical, value, "Khe_Ho_Technical"))
			{
				_kheHoTechnicalText = value.ToString("0.###", CultureInfo.InvariantCulture);
				OnPropertyChanged("Khe_Ho_Technical_Text");
				UpdatePreviewGeometry();
			}
		}
	}

	public string Khe_Ho_Technical_Text
	{
		get
		{
			return _kheHoTechnicalText;
		}
		set
		{
			if (SetProperty(ref _kheHoTechnicalText, value, "Khe_Ho_Technical_Text") && TryParseFlexible(value, out var value2) && value2 >= 0.0)
			{
				_kheHoTechnical = value2;
				UpdatePreviewGeometry();
			}
		}
	}

	public int SelectedArrayModeIndex_Technical
	{
		get
		{
			return _selectedArrayModeIndexTechnical;
		}
		set
		{
			if (SetProperty(ref _selectedArrayModeIndexTechnical, value, "SelectedArrayModeIndex_Technical"))
			{
				UpdatePreviewGeometry();
			}
		}
	}

	public double L_Std
	{
		get
		{
			return _lStd;
		}
		set
		{
			if (SetProperty(ref _lStd, value, "L_Std"))
			{
				_lStdText = value.ToString("0.##", CultureInfo.InvariantCulture);
				OnPropertyChanged("L_Std_Text");
				UpdatePreviewGeometry();
			}
		}
	}

	public string L_Std_Text
	{
		get
		{
			return _lStdText;
		}
		set
		{
			if (SetProperty(ref _lStdText, value, "L_Std_Text") && TryParseFlexible(value, out var value2) && value2 > 0.0)
			{
				_lStd = value2;
				UpdatePreviewGeometry();
			}
		}
	}

	public double Khe_Ho
	{
		get
		{
			return _kheHo;
		}
		set
		{
			if (SetProperty(ref _kheHo, value, "Khe_Ho"))
			{
				_kheHoText = value.ToString("0.###", CultureInfo.InvariantCulture);
				OnPropertyChanged("Khe_Ho_Text");
				UpdatePreviewGeometry();
			}
		}
	}

	public string Khe_Ho_Text
	{
		get
		{
			return _kheHoText;
		}
		set
		{
			if (SetProperty(ref _kheHoText, value, "Khe_Ho_Text") && TryParseFlexible(value, out var value2) && value2 >= 0.0)
			{
				_kheHo = value2;
				UpdatePreviewGeometry();
			}
		}
	}

	public double KhoangCachTimDefault
	{
		get
		{
			return _khoangCachTimDefault;
		}
		set
		{
			if (SetProperty(ref _khoangCachTimDefault, value, "KhoangCachTimDefault"))
			{
				_khoangCachTimText = value.ToString("0.##", CultureInfo.InvariantCulture);
				OnPropertyChanged("KhoangCachTimDefault_Text");
				UpdatePreviewGeometry();
			}
		}
	}

	public string KhoangCachTimDefault_Text
	{
		get
		{
			return _khoangCachTimText;
		}
		set
		{
			if (SetProperty(ref _khoangCachTimText, value, "KhoangCachTimDefault_Text") && TryParseFlexible(value, out var value2) && value2 > 0.0)
			{
				_khoangCachTimDefault = value2;
				UpdatePreviewGeometry();
			}
		}
	}

	public double B_Box
	{
		get
		{
			return _bBox;
		}
		set
		{
			if (SetProperty(ref _bBox, value, "B_Box"))
			{
				_bBoxText = value.ToString("0.##", CultureInfo.InvariantCulture);
				OnPropertyChanged("B_Box_Text");
				UpdatePreviewGeometry();
			}
		}
	}

	public string B_Box_Text
	{
		get
		{
			return _bBoxText;
		}
		set
		{
			if (SetProperty(ref _bBoxText, value, "B_Box_Text") && TryParseFlexible(value, out var value2) && value2 > 0.0)
			{
				_bBox = value2;
				UpdatePreviewGeometry();
			}
		}
	}

	public bool IsDoubleCulvertMode
	{
		get
		{
			return _isDoubleCulvertMode;
		}
		set
		{
			SetProperty(ref _isDoubleCulvertMode, value, "IsDoubleCulvertMode");
		}
	}

	public bool IsCastInPlaceMode
	{
		get
		{
			return _isCastInPlaceMode;
		}
		set
		{
			SetProperty(ref _isCastInPlaceMode, value, "IsCastInPlaceMode");
		}
	}

	public ObservableCollection<string> ArrayModes { get; } = new ObservableCollection<string> { "Rải từ giữa ra 2 bên (2 đầu co giãn đối xứng - Chịu lực tối ưu)", "Rải một chiều (Đốt cuối co giãn - Theo hướng dốc thoát nước)" };

	public int SelectedArrayModeIndex
	{
		get
		{
			return _selectedArrayModeIndex;
		}
		set
		{
			if (SetProperty(ref _selectedArrayModeIndex, value, "SelectedArrayModeIndex"))
			{
				UpdatePreviewGeometry();
			}
		}
	}

	public ObservableCollection<CulvertComponentItem> ActiveAssignedFamiliesForTab02 { get; } = new ObservableCollection<CulvertComponentItem>();

	public ObservableCollection<CulvertComponentItem> FilteredAssignedFamiliesForTab02 { get; } = new ObservableCollection<CulvertComponentItem>();

	public string SelectedTab02GroupFilter
	{
		get
		{
			return _selectedTab02GroupFilter;
		}
		set
		{
			if (SetProperty(ref _selectedTab02GroupFilter, value, "SelectedTab02GroupFilter"))
			{
				OnPropertyChanged("SelectedTab02GroupName");
				OnPropertyChanged("IsCastInPlaceTab02Active");
			}
		}
	}

	public bool IsCastInPlaceTab02Active
	{
		get
		{
			if (!(_selectedTab02GroupFilter == "CastInPlace"))
			{
				return CastInPlaceProfiles.Any((CulvertTypeProfile p) => p.TypeName == _selectedTab02GroupFilter);
			}
			return true;
		}
	}

	public CulvertTypeProfile? SelectedCastInPlaceProfileForTab02
	{
		get
		{
			if (_selectedCastInPlaceProfileForTab02 == null || !CastInPlaceProfiles.Contains(_selectedCastInPlaceProfileForTab02))
			{
				_selectedCastInPlaceProfileForTab02 = CastInPlaceProfiles.FirstOrDefault();
			}
			return _selectedCastInPlaceProfileForTab02;
		}
		set
		{
			if (SetProperty(ref _selectedCastInPlaceProfileForTab02, value, "SelectedCastInPlaceProfileForTab02") && value != null)
			{
				_selectedTab02GroupFilter = value.TypeName;
				OnPropertyChanged("SelectedTab02GroupFilter");
				OnPropertyChanged("SelectedTab02GroupName");
				OnPropertyChanged("IsCastInPlaceTab02Active");
				if (!_isInitializing)
				{
					ApplyTab02FamilyFilter();
				}
			}
		}
	}

	public string SelectedTab02GroupName => _selectedTab02GroupFilter switch
	{
		"Precast" => "\ud83e\uddf1 Cống đúc sẵn", 
		"CastInPlace" => "\ud83c\udfd7\ufe0f Tất cả Cống đổ tại chỗ", 
		"Barrel" => "\ud83e\uddf1 Thân cống", 
		"Outlet" => "\ud83c\udf0a Cửa xả", 
		"Apron" => "\ud83d\udee1\ufe0f Sân gia cố", 
		"Manhole" => "\ud83d\udd73\ufe0f Hố ga (Hộp nối)", 
		"TechManhole" => "\ud83d\udd73\ufe0f Hố kỹ thuật", 
		"JunctionBox" => "\ud83d\udce6 Hộp nối cống", 
		"All" => "\ud83d\udcc1 Tất cả", 
		null => "\ud83d\udcc1 Tất cả", 
		_ => CastInPlaceProfiles.FirstOrDefault((CulvertTypeProfile p) => p.TypeName == _selectedTab02GroupFilter)?.TypeName ?? _selectedTab02GroupFilter, 
	};

	public RelayCommand<string> FilterTab02GroupCommand { get; }

	public CulvertComponentItem? SelectedComponentForTab02
	{
		get
		{
			return _selectedComponentForTab02;
		}
		set
		{
			if (_isFilteringTab02)
			{
				_selectedComponentForTab02 = value;
				OnPropertyChanged("SelectedComponentForTab02");
				return;
			}
			if (SetProperty(ref _selectedComponentForTab02, value, "SelectedComponentForTab02"))
			{
				if (!_isInitializing)
				{
					OnSelectedFamilyForTab02Changed();
				}
			}
		}
	}

	public string Tab02ParamCategoryFilter
	{
		get
		{
			return _tab02ParamCategoryFilter;
		}
		set
		{
			if (SetProperty(ref _tab02ParamCategoryFilter, value, "Tab02ParamCategoryFilter"))
			{
				FilterParameterMappings();
			}
		}
	}

	public bool ShowDimensions
	{
		get
		{
			return _showDimensions;
		}
		set
		{
			if (SetProperty(ref _showDimensions, value, "ShowDimensions"))
			{
				FilterParameterMappings();
			}
		}
	}

	public bool ShowVisibility
	{
		get
		{
			return _showVisibility;
		}
		set
		{
			if (SetProperty(ref _showVisibility, value, "ShowVisibility"))
			{
				FilterParameterMappings();
			}
		}
	}

	public bool ShowOther
	{
		get
		{
			return _showOther;
		}
		set
		{
			if (SetProperty(ref _showOther, value, "ShowOther"))
			{
				FilterParameterMappings();
			}
		}
	}

	public bool ShowAllGroups
	{
		get
		{
			return _showAllGroups;
		}
		set
		{
			if (SetProperty(ref _showAllGroups, value, "ShowAllGroups"))
			{
				FilterParameterMappings();
			}
		}
	}

	public bool ShowTypeParams
	{
		get
		{
			return _showTypeParams;
		}
		set
		{
			if (SetProperty(ref _showTypeParams, value, "ShowTypeParams"))
			{
				FilterParameterMappings();
			}
		}
	}

	public bool ShowInstanceParams
	{
		get
		{
			return _showInstanceParams;
		}
		set
		{
			if (SetProperty(ref _showInstanceParams, value, "ShowInstanceParams"))
			{
				FilterParameterMappings();
			}
		}
	}

	public string ParamSearchFilter
	{
		get
		{
			return _paramSearchFilter;
		}
		set
		{
			if (SetProperty(ref _paramSearchFilter, value, "ParamSearchFilter"))
			{
				FilterParameterMappings();
			}
		}
	}

	public ObservableCollection<ParameterMappingItem> ParameterMappings { get; } = new ObservableCollection<ParameterMappingItem>();

	public ObservableCollection<ParameterMappingItem> FilteredParameterMappings { get; } = new ObservableCollection<ParameterMappingItem>();

	public CulvertRowData? SelectedCulvertRowForInspector
	{
		get
		{
			return _selectedCulvertRowForInspector;
		}
		set
		{
			if (SetProperty(ref _selectedCulvertRowForInspector, value, "SelectedCulvertRowForInspector"))
			{
				RefreshInspectorParameterMappings();
			}
		}
	}

	public ObservableCollection<ParameterMappingItem> InspectorParameterMappings { get; } = new ObservableCollection<ParameterMappingItem>();

	public ObservableCollection<CulvertRowData> InspectorCulvertRows { get; } = new ObservableCollection<CulvertRowData>();

	public ObservableCollection<CulvertRowData> AllCulvertRows { get; } = new ObservableCollection<CulvertRowData>();

	public ObservableCollection<CulvertRowData> FilteredCulvertRows { get; } = new ObservableCollection<CulvertRowData>();

	public ObservableCollection<ValidationResultItem> ValidationResults { get; } = new ObservableCollection<ValidationResultItem>();

	public BimInfoConfig BimConfig { get; } = new BimInfoConfig();

	private readonly Dictionary<string, List<CulvertRowOverrideSaveDto>> _overridesBySheet = new(StringComparer.OrdinalIgnoreCase);

	public ObservableCollection<CustomBimParameterItem> CustomBimParameters { get; } = new ObservableCollection<CustomBimParameterItem>();

	public ObservableCollection<CustomBimParameterItem> DisplayedCustomBimParameters { get; } = new ObservableCollection<CustomBimParameterItem>();

	private string _selectedTargetScopeForTab04 = "Tất cả cấu kiện";
	public string SelectedTargetScopeForTab04
	{
		get => _selectedTargetScopeForTab04;
		set
		{
			if (SetProperty(ref _selectedTargetScopeForTab04, value, nameof(SelectedTargetScopeForTab04)))
			{
				RefreshDisplayedCustomBimParameters();
			}
		}
	}

	public CustomBimParameterItem? SelectedCustomBimParameter
	{
		get
		{
			return _selectedCustomBimParameter;
		}
		set
		{
			SetProperty(ref _selectedCustomBimParameter, value, "SelectedCustomBimParameter");
		}
	}

	public string SelectedCulvertTypeForTab04
	{
		get
		{
			return _selectedCulvertTypeForTab04;
		}
		set
		{
			if (SetProperty(ref _selectedCulvertTypeForTab04, value, "SelectedCulvertTypeForTab04"))
			{
				OnPropertyChanged("IsCastInPlaceTab04Active");
				UpdateAvailableTargetScopesForTab04();
				UpdateFilteredCulvertRowsForTab04();
			}
		}
	}

	public bool IsCastInPlaceTab04Active
	{
		get
		{
			if (!(_selectedCulvertTypeForTab04 == "CỐNG ĐỔ TẠI CHỖ"))
			{
				return CastInPlaceProfiles.Any((CulvertTypeProfile p) => p.TypeName == _selectedCulvertTypeForTab04);
			}
			return true;
		}
	}

	public CulvertTypeProfile? SelectedCastInPlaceProfileForTab04
	{
		get
		{
			if (_selectedCastInPlaceProfileForTab04 == null || !CastInPlaceProfiles.Contains(_selectedCastInPlaceProfileForTab04))
			{
				_selectedCastInPlaceProfileForTab04 = CastInPlaceProfiles.FirstOrDefault();
			}
			return _selectedCastInPlaceProfileForTab04;
		}
		set
		{
			if (SetProperty(ref _selectedCastInPlaceProfileForTab04, value, "SelectedCastInPlaceProfileForTab04") && value != null)
			{
				SelectedCulvertTypeForTab04 = value.TypeName;
			}
		}
	}

	public ObservableCollection<string> AvailableTargetScopesForTab04 { get; } = new ObservableCollection<string>();

	public ObservableCollection<CulvertRowData> FilteredCulvertRowsForTab04 { get; } = new ObservableCollection<CulvertRowData>();

	public CulvertRowData? SelectedCulvertRowForPreview
	{
		get
		{
			return _selectedCulvertRowForPreview;
		}
		set
		{
			if (!SetProperty(ref _selectedCulvertRowForPreview, value, "SelectedCulvertRowForPreview"))
			{
				return;
			}
			UpdatePreviewGeometry();
			if (_selectedCulvertRowForPreview != null)
			{
				bool flag = false;
				lock (_cachedRealMeshes)
				{
					flag = _cachedRealMeshes.ContainsKey(_selectedCulvertRowForPreview.STT);
				}
				if (!flag || (_activeReviewWindow != null && _activeReviewWindow.IsLoaded))
				{
					ExtractRealFamilyGeometryForPreview(_selectedCulvertRowForPreview);
				}
			}
		}
	}

	public CulvertPreviewGeometry? CurrentPreviewGeometry
	{
		get
		{
			return _currentPreviewGeometry;
		}
		set
		{
			SetProperty(ref _currentPreviewGeometry, value, "CurrentPreviewGeometry");
		}
	}

	public PreviewViewMode PreviewMode
	{
		get
		{
			return _previewMode;
		}
		set
		{
			SetProperty(ref _previewMode, value, "PreviewMode");
		}
	}

	public string PreviewSearchText
	{
		get
		{
			return _previewSearchText;
		}
		set
		{
			if (SetProperty(ref _previewSearchText, value, "PreviewSearchText"))
			{
				FilterPreviewCulvertRows();
			}
		}
	}

	public ObservableCollection<CulvertRowData> FilteredPreviewCulvertRows { get; } = new ObservableCollection<CulvertRowData>();

	public Action<string>? RequestPreviewAction { get; set; }

	public ParameterMappingItem? SelectedParamForBatch
	{
		get
		{
			return _selectedParamForBatch;
		}
		set
		{
			if (SetProperty(ref _selectedParamForBatch, value, "SelectedParamForBatch"))
			{
				UpdateBatchTableForSelectedParam();
			}
		}
	}

	public string BatchApplyValue
	{
		get
		{
			return _batchApplyValue;
		}
		set
		{
			SetProperty(ref _batchApplyValue, value, "BatchApplyValue");
		}
	}

	public int BatchFromSTT
	{
		get
		{
			return _batchFromSTT;
		}
		set
		{
			if (SetProperty(ref _batchFromSTT, value, "BatchFromSTT"))
			{
				UpdateInspectorCulvertRows();
			}
		}
	}

	public int BatchToSTT
	{
		get
		{
			return _batchToSTT;
		}
		set
		{
			if (SetProperty(ref _batchToSTT, value, "BatchToSTT"))
			{
				UpdateInspectorCulvertRows();
			}
		}
	}

	public string StatusConclusion
	{
		get
		{
			return _statusConclusion;
		}
		set
		{
			SetProperty(ref _statusConclusion, value, "StatusConclusion");
		}
	}

	public int TotalErrors
	{
		get
		{
			return _totalErrors;
		}
		set
		{
			SetProperty(ref _totalErrors, value, "TotalErrors");
		}
	}

	public int TotalWarnings
	{
		get
		{
			return _totalWarnings;
		}
		set
		{
			SetProperty(ref _totalWarnings, value, "TotalWarnings");
		}
	}

	public int TotalInfos
	{
		get
		{
			return _totalInfos;
		}
		set
		{
			SetProperty(ref _totalInfos, value, "TotalInfos");
		}
	}

	public string ExecutionStatus
	{
		get
		{
			return _executionStatus;
		}
		set
		{
			SetProperty(ref _executionStatus, value, "ExecutionStatus");
		}
	}

	public ObservableCollection<CulvertMaterialItem> ComponentMaterials { get; } = new ObservableCollection<CulvertMaterialItem>();

	public ObservableCollection<CulvertMaterialItem> FilteredComponentMaterials { get; } = new ObservableCollection<CulvertMaterialItem>();

	public string SelectedCulvertTypeForTab03
	{
		get
		{
			return _selectedCulvertTypeForTab03;
		}
		set
		{
			if (SetProperty(ref _selectedCulvertTypeForTab03, value, "SelectedCulvertTypeForTab03"))
			{
				OnPropertyChanged("IsCastInPlaceTab03Active");
				ApplyMaterialGroupFilter();
			}
		}
	}

	public bool IsCastInPlaceTab03Active
	{
		get
		{
			if (!(_selectedCulvertTypeForTab03 == "CỐNG ĐỔ TẠI CHỖ"))
			{
				return CastInPlaceProfiles.Any((CulvertTypeProfile p) => p.TypeName == _selectedCulvertTypeForTab03);
			}
			return true;
		}
	}

	public CulvertTypeProfile? SelectedCastInPlaceProfileForTab03
	{
		get
		{
			if (_selectedCastInPlaceProfileForTab03 == null || !CastInPlaceProfiles.Contains(_selectedCastInPlaceProfileForTab03))
			{
				_selectedCastInPlaceProfileForTab03 = CastInPlaceProfiles.FirstOrDefault();
			}
			return _selectedCastInPlaceProfileForTab03;
		}
		set
		{
			if (SetProperty(ref _selectedCastInPlaceProfileForTab03, value, "SelectedCastInPlaceProfileForTab03") && value != null)
			{
				SelectedCulvertTypeForTab03 = value.TypeName;
			}
		}
	}

	public string SelectedMaterialGroupFilter
	{
		get
		{
			return _selectedMaterialGroupFilter;
		}
		set
		{
			if (SetProperty(ref _selectedMaterialGroupFilter, value, "SelectedMaterialGroupFilter"))
			{
				OnPropertyChanged("SelectedMaterialGroupFilterName");
				ApplyMaterialGroupFilter();
			}
		}
	}

	public string SelectedMaterialGroupFilterName => _selectedMaterialGroupFilter switch
	{
		"Barrel" => "\ud83e\uddf1 Thân cống", 
		"Outlet" => "\ud83c\udf0a Cửa xả", 
		"Apron" => "\ud83d\udee1\ufe0f Sân gia cố", 
		"Manhole" => "\ud83d\udd73\ufe0f Hố ga (Hộp nối)", 
		_ => "\ud83d\udcc1 Tất cả các cụm", 
	};

	public ObservableCollection<string> AvailableRevitMaterials { get; } = new ObservableCollection<string>();

	public CulvertMaterialItem? SelectedMaterialItem
	{
		get
		{
			return _selectedMaterialItem;
		}
		set
		{
			SetProperty(ref _selectedMaterialItem, value, "SelectedMaterialItem");
		}
	}

	public ObservableCollection<CulvertMaterialItem> SelectedMaterialItems { get; } = new ObservableCollection<CulvertMaterialItem>();

	public string SelectedItemsSummaryText
	{
		get
		{
			return _selectedItemsSummaryText;
		}
		set
		{
			SetProperty(ref _selectedItemsSummaryText, value, "SelectedItemsSummaryText");
		}
	}

	public string ApplyToSelectedGroupButtonText
	{
		get
		{
			return _applyToSelectedGroupButtonText;
		}
		set
		{
			SetProperty(ref _applyToSelectedGroupButtonText, value, "ApplyToSelectedGroupButtonText");
		}
	}

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

	public RelayCommand UpdateBimAndMaterialsCommand { get; }

	public RelayCommand CloseCommand { get; }

	public RelayCommand AddComponentCommand { get; }

	public RelayCommand RemoveComponentCommand { get; }

	public RelayCommand<string> ApplyAssemblyTemplateCommand { get; }

	public RelayCommand AddCastInPlaceProfileCommand { get; }

	public RelayCommand RemoveCastInPlaceProfileCommand { get; }

	public RelayCommand AssignProfileToSelectedCulvertsCommand { get; }

	public RelayCommand<object> FilterTab02ByProfileCommand { get; }

	public RelayCommand Open3DReviewCommand { get; }

	public RelayCommand<string> SetTab02ParamCategoryFilterCommand { get; }

	public RelayCommand<string> SetPreviewModeCommand { get; }

	public RelayCommand FitPreviewCommand { get; }

	public RelayCommand ZoomInPreviewCommand { get; }

	public RelayCommand ZoomOutPreviewCommand { get; }

	public RelayCommand RefreshPreviewCommand { get; }

	public RelayCommand<string> SetCulvertTypeForTab03Command { get; }

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

	public RelayCommand ApplyMaterialToSelectedGroupCommand { get; }

	public RelayCommand ApplyMaterialToCurrentGroupTypeCommand { get; }

	public RelayCommand<string> SetCulvertTypeForTab04Command { get; }

	public RelayCommand AddCustomBimParameterCommand { get; }

	public RelayCommand RemoveCustomBimParameterCommand { get; }

	public RelayCommand ResetDefaultBimParametersCommand { get; }

	public RelayCommand UpdateBimParametersOnlyCommand { get; }

	public RelayCommand CopyBimParametersToScopeCommand { get; }

	// Persistence Commands (LÆ°u & KhÃ´i phá»¥c cáº¥u hÃ¬nh dá»± Ã¡n)
	public RelayCommand SaveProjectConfigCommand { get; }

	public RelayCommand ExportPresetCommand { get; }

	public RelayCommand ImportPresetCommand { get; }

	public RelayCommand RestoreConfigCommand { get; }

	public Action? RequestClose { get; set; }

	public RelayCommand CopyKhung1ToInspectorRowCommand => new RelayCommand((object? _) =>
	{
		CulvertRowData selectedCulvertRowForInspector = SelectedCulvertRowForInspector;
		if (selectedCulvertRowForInspector == null)
		{
			System.Windows.MessageBox.Show("Vui lòng chọn một lý trình ở Khung 2 để sao chép.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
		else
		{
			string categoryName = SelectedComponentForTab02?.CategoryType ?? string.Empty;
			string profileName = SelectedComponentForTab02?.ProfileName ?? string.Empty;
			int num = 0;
			foreach (ParameterMappingItem filteredParameterMapping in FilteredParameterMappings)
			{
				if (!string.IsNullOrWhiteSpace(filteredParameterMapping.CustomValue))
				{
					selectedCulvertRowForInspector.SetParamOverride(profileName, categoryName, filteredParameterMapping.InternalName, filteredParameterMapping.CustomValue);
					num++;
				}
			}
			RefreshInspectorParameterMappings();
			UpdatePreviewGeometry();
			System.Windows.MessageBox.Show($"Đã sao chép {num} thông số từ Khung 1 vào Lý trình {selectedCulvertRowForInspector.LyTrinh} (STT {selectedCulvertRowForInspector.STT}).", "Thành công", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	});

	public RelayCommand RefreshInspectorParamsCommand => new RelayCommand((object? _) =>
	{
		RefreshInspectorParameterMappings();
	});

	private void UpdateModeFromSheetName(string sheetName)
	{
		string text = sheetName.Trim().ToLowerInvariant();
		if (IsTechnicalCulvertMode = text.Contains("kythuat") || text.Contains("kỹ thuật") || text.Contains("ky_thuat") || text.Contains("ckt"))
		{
			ApplyAssemblyTemplate("CỐNG KỸ THUẬT");
			SelectedCulvertTypeForTab03 = "CỐNG KỸ THUẬT";
			SelectedCulvertTypeForTab04 = "CỐNG KỸ THUẬT";
		}
		else if (SelectedCulvertTemplateType == "CỐNG KỸ THUẬT")
		{
			ApplyAssemblyTemplate("CỐNG HỘP ĐÚC SẴN");
			SelectedCulvertTypeForTab03 = "CỐNG ĐÚC SẴN";
			SelectedCulvertTypeForTab04 = "CỐNG ĐÚC SẴN";
		}
		ApplyMaterialGroupFilter();
		UpdateAvailableTargetScopesForTab04();
		UpdateFilteredCulvertRowsForTab04();
	}

	private void OnCastInPlaceProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_isInitializing) return;
		if (sender is CulvertTypeProfile culvertTypeProfile && (e.PropertyName == "L_Std" || e.PropertyName == "L_Std_Text" || e.PropertyName == "Khe_Ho" || e.PropertyName == "Khe_Ho_Text" || e.PropertyName == "SelectedArrayModeIndex"))
		{
			_lStdCastInPlace = culvertTypeProfile.L_Std;
			_kheHoCastInPlace = culvertTypeProfile.Khe_Ho;
			OnPropertyChanged("L_Std_CastInPlace");
			OnPropertyChanged("L_Std_CastInPlace_Text");
			OnPropertyChanged("Khe_Ho_CastInPlace");
			OnPropertyChanged("Khe_Ho_CastInPlace_Text");
			UpdatePreviewGeometry();
		}
	}

	private static bool TryParseFlexible(string? text, out double value)
	{
		value = 0.0;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		return double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
	}

	public void UpdateInspectorCulvertRows()
	{
		InspectorCulvertRows.Clear();
		foreach (CulvertRowData item in AllCulvertRows.Where((CulvertRowData r) => r.STT >= BatchFromSTT && r.STT <= BatchToSTT).ToList())
		{
			InspectorCulvertRows.Add(item);
		}
		if (SelectedCulvertRowForInspector == null || !InspectorCulvertRows.Contains(SelectedCulvertRowForInspector))
		{
			SelectedCulvertRowForInspector = InspectorCulvertRows.FirstOrDefault();
		}
	}

	private void SaveCurrentSheetOverrides()
	{
		if (string.IsNullOrEmpty(SelectedSheetName) || AllCulvertRows == null || AllCulvertRows.Count == 0) return;
		var currentOverrides = AllCulvertRows
			.Where(r => r.ParameterOverrides != null && r.ParameterOverrides.Count > 0)
			.Select(r => new CulvertRowOverrideSaveDto
			{
				SheetName = SelectedSheetName,
				LyTrinh = r.LyTrinh ?? string.Empty,
				STT = r.STT,
				Overrides = new Dictionary<string, string>(r.ParameterOverrides, StringComparer.OrdinalIgnoreCase)
			}).ToList();
		_overridesBySheet[SelectedSheetName] = currentOverrides;
	}

	private void RestoreCurrentSheetOverrides()
	{
		if (string.IsNullOrEmpty(SelectedSheetName) || !_overridesBySheet.TryGetValue(SelectedSheetName, out var savedList) || savedList == null) return;
		foreach (var ov in savedList)
		{
			var row = AllCulvertRows.FirstOrDefault(r =>
				(!string.IsNullOrEmpty(ov.LyTrinh) && string.Equals(r.LyTrinh?.Trim(), ov.LyTrinh.Trim(), StringComparison.OrdinalIgnoreCase))
				|| r.STT == ov.STT);
			if (row != null && ov.Overrides != null)
			{
				foreach (var kvp in ov.Overrides)
				{
					row.ParameterOverrides[kvp.Key] = kvp.Value;
				}
			}
		}
	}

	public void UpdateAvailableTargetScopesForTab04()
	{
		AvailableTargetScopesForTab04.Clear();
		string type = SelectedCulvertTypeForTab04 ?? "TẤT CẢ";
		if (type == "CỐNG ĐỔ TẠI CHỖ" || CastInPlaceProfiles.Any((CulvertTypeProfile p) => p.TypeName == type))
		{
			AvailableTargetScopesForTab04.Add("Tất cả cấu kiện");
			AvailableTargetScopesForTab04.Add("Cống hộp liên trong");
			AvailableTargetScopesForTab04.Add("Cống hộp liên ngoài trái");
			AvailableTargetScopesForTab04.Add("Cống hộp liên ngoài phải");
			AvailableTargetScopesForTab04.Add("Bê tông lót");
			AvailableTargetScopesForTab04.Add("Đá dăm đệm");
		}
		else if (type == "CỐNG ĐÚC SẴN")
		{
			AvailableTargetScopesForTab04.Add("Tất cả cấu kiện");
			AvailableTargetScopesForTab04.Add("Đốt cống đúc sẵn");
			AvailableTargetScopesForTab04.Add("Bê tông lót");
			AvailableTargetScopesForTab04.Add("Đá dăm đệm");
		}
		else if (type == "CỬA XẢ")
		{
			AvailableTargetScopesForTab04.Add("Tất cả cấu kiện cửa xả");
			AvailableTargetScopesForTab04.Add("Tường đầu");
			AvailableTargetScopesForTab04.Add("Tường cánh");
			AvailableTargetScopesForTab04.Add("Sân cống");
			AvailableTargetScopesForTab04.Add("Bê tông lót");
			AvailableTargetScopesForTab04.Add("Đá dăm đệm");
		}
		else if (type == "SÂN GIA CỐ")
		{
			AvailableTargetScopesForTab04.Add("Tất cả cấu kiện sân gia cố");
			AvailableTargetScopesForTab04.Add("Sân gia cố");
			AvailableTargetScopesForTab04.Add("Bê tông lót");
		}
		else if (type == "HỐ GA (HỘP NỐI)")
		{
			AvailableTargetScopesForTab04.Add("Tất cả cấu kiện hố ga");
			AvailableTargetScopesForTab04.Add("Thân hố ga");
			AvailableTargetScopesForTab04.Add("Cổ giếng");
			AvailableTargetScopesForTab04.Add("Khuôn hầm");
			AvailableTargetScopesForTab04.Add("Nắp đan");
			AvailableTargetScopesForTab04.Add("Bê tông lót");
		}
		else if (type == "CỐNG KỸ THUẬT")
		{
			AvailableTargetScopesForTab04.Add("Tất cả cấu kiện cống kỹ thuật");
			AvailableTargetScopesForTab04.Add("Thân cống kỹ thuật");
			AvailableTargetScopesForTab04.Add("Hố kỹ thuật (Hộp nối)");
			AvailableTargetScopesForTab04.Add("Bê tông lót");
			AvailableTargetScopesForTab04.Add("Đá dăm đệm");
		}
		else
		{
			AvailableTargetScopesForTab04.Add("Tất cả cấu kiện");
			AvailableTargetScopesForTab04.Add("Đốt cống đúc sẵn");
			AvailableTargetScopesForTab04.Add("Cống hộp liên trong");
			AvailableTargetScopesForTab04.Add("Cống hộp liên ngoài trái");
			AvailableTargetScopesForTab04.Add("Cống hộp liên ngoài phải");
			AvailableTargetScopesForTab04.Add("Cửa xả");
			AvailableTargetScopesForTab04.Add("Sân gia cố");
			AvailableTargetScopesForTab04.Add("Hộp nối");
			AvailableTargetScopesForTab04.Add("Bê tông lót");
			AvailableTargetScopesForTab04.Add("Đá dăm đệm");
		}

		string defaultScope = AvailableTargetScopesForTab04.FirstOrDefault() ?? "Tất cả cấu kiện";
		if (string.IsNullOrEmpty(_selectedTargetScopeForTab04) || !AvailableTargetScopesForTab04.Contains(_selectedTargetScopeForTab04))
		{
			SelectedTargetScopeForTab04 = defaultScope;
		}
		else
		{
			RefreshDisplayedCustomBimParameters();
		}
	}

	public void RefreshDisplayedCustomBimParameters()
	{
		DisplayedCustomBimParameters.Clear();
		string currentScope = SelectedTargetScopeForTab04 ?? "Tất cả cấu kiện";

		EnsureDefaultParametersForScope(currentScope);

		var matched = CustomBimParameters.Where(p =>
			string.Equals(p.TargetScope, currentScope, StringComparison.OrdinalIgnoreCase)
		).ToList();

		foreach (var item in matched)
		{
			DisplayedCustomBimParameters.Add(item);
		}

		if (SelectedCustomBimParameter == null || !DisplayedCustomBimParameters.Contains(SelectedCustomBimParameter))
		{
			SelectedCustomBimParameter = DisplayedCustomBimParameters.FirstOrDefault();
		}
		UpdateCustomBimParameterPreviews();
	}

	private void EnsureDefaultParametersForScope(string scope)
	{
		if (string.IsNullOrWhiteSpace(scope)) return;
		if (CustomBimParameters.Any(p => string.Equals(p.TargetScope, scope, StringComparison.OrdinalIgnoreCase))) return;

		if (scope.StartsWith("Tất cả", StringComparison.OrdinalIgnoreCase))
		{
			AddDefaultBimParam(scope, "Loại công trình", "Constraints", "Giao thông", "Loại công trình hạ tầng / giao thông");
			AddDefaultBimParam(scope, "Tên công trình", "Constraints", "Đường ven biển Đắk Lắk", "Tên dự án công trình");
			AddDefaultBimParam(scope, "Hạng mục", "Constraints", "cụm Lý trình Km", "Hạng mục cống thoát nước");
			AddDefaultBimParam(scope, "Lý trình", "Constraints", "{LyTrinh}", "Lý trình thiết kế cống");
			AddDefaultBimParam(scope, "Phân nhóm cấu kiện", "Constraints", "{LoaiCong} {KhauDo}", "Phân nhóm chủng loại và khẩu độ");
			AddDefaultBimParam(scope, "Tên cấu kiện", "Constraints", "TCDS.{STT_DOT}", "Tên mã hiệu cấu kiện");
			AddDefaultBimParam(scope, "Mô tả", "Constraints", "Đốt cống {STT_DOT}", "Mô tả chi tiết cấu kiện");
			AddDefaultBimParam(scope, "Vật liệu", "Constraints", "{VatLieu}", "Vật liệu đồng bộ từ Tab 03");
		}
		else if (scope == "Tường đầu")
		{
			AddDefaultBimParam(scope, "Tên cấu kiện", "Constraints", "Tường đầu cống {LyTrinh}", "Tên mã hiệu tường đầu");
			AddDefaultBimParam(scope, "Mô tả", "Constraints", "Tường đầu {LoaiCong} ({KhauDo})", "Mô tả tường đầu");
			AddDefaultBimParam(scope, "Vật liệu", "Constraints", "{VatLieu}", "Vật liệu đồng bộ từ Tab 03");
		}
		else if (scope == "Tường cánh")
		{
			AddDefaultBimParam(scope, "Tên cấu kiện", "Constraints", "Tường cánh cống {LyTrinh}", "Tên mã hiệu tường cánh");
			AddDefaultBimParam(scope, "Mô tả", "Constraints", "Tường cánh {LoaiCong} ({KhauDo})", "Mô tả tường cánh");
			AddDefaultBimParam(scope, "Vật liệu", "Constraints", "{VatLieu}", "Vật liệu đồng bộ từ Tab 03");
		}
		else if (scope == "Sân cống")
		{
			AddDefaultBimParam(scope, "Tên cấu kiện", "Constraints", "Sân cống {LyTrinh}", "Tên mã hiệu sân cống");
			AddDefaultBimParam(scope, "Mô tả", "Constraints", "Sân cống {LoaiCong} ({KhauDo})", "Mô tả sân cống");
			AddDefaultBimParam(scope, "Vật liệu", "Constraints", "{VatLieu}", "Vật liệu đồng bộ từ Tab 03");
		}
		else if (scope == "Sân gia cố")
		{
			AddDefaultBimParam(scope, "Tên cấu kiện", "Constraints", "Sân gia cố {LyTrinh}", "Tên mã hiệu sân gia cố");
			AddDefaultBimParam(scope, "Mô tả", "Constraints", "Sân gia cố {LoaiCong} ({KhauDo})", "Mô tả sân gia cố");
			AddDefaultBimParam(scope, "Vật liệu", "Constraints", "{VatLieu}", "Vật liệu đồng bộ từ Tab 03");
		}
		else if (scope == "Bê tông lót")
		{
			AddDefaultBimParam(scope, "Tên cấu kiện", "Constraints", "BTL cống {LyTrinh}", "Bê tông lót móng cống");
			AddDefaultBimParam(scope, "Vật liệu", "Constraints", "{VatLieu}", "Vật liệu bê tông lót");
		}
		else if (scope == "Đá dăm đệm")
		{
			AddDefaultBimParam(scope, "Tên cấu kiện", "Constraints", "Đá dăm cống {LyTrinh}", "Lớp đá dăm đệm móng");
			AddDefaultBimParam(scope, "Vật liệu", "Constraints", "{VatLieu}", "Vật liệu đá dăm");
		}
		else if (scope.IndexOf("hố ga", StringComparison.OrdinalIgnoreCase) >= 0 || scope.IndexOf("Hố", StringComparison.OrdinalIgnoreCase) >= 0 || scope.IndexOf("Hộp", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			AddDefaultBimParam(scope, "Tên cấu kiện", "Constraints", scope + " cống {LyTrinh}", "Tên mã hiệu " + scope);
			AddDefaultBimParam(scope, "Vật liệu", "Constraints", "{VatLieu}", "Vật liệu đồng bộ từ Tab 03");
		}
		else
		{
			AddDefaultBimParam(scope, "Tên cấu kiện", "Constraints", scope + ".{STT_DOT}", "Tên mã hiệu " + scope);
			AddDefaultBimParam(scope, "Vật liệu", "Constraints", "{VatLieu}", "Vật liệu cấu kiện");
		}
	}

	private void AddDefaultBimParam(string scope, string name, string group, string valTemplate, string desc, bool isActive = true)
	{
		var item = new CustomBimParameterItem
		{
			IsActive = isActive,
			ParamName = name,
			RevitGroup = group,
			TargetScope = scope,
			ValueTemplate = valTemplate,
			Description = desc
		};
		item.PropertyChanged += OnCustomBimParameterPropertyChanged;
		CustomBimParameters.Add(item);
	}

	public void UpdateFilteredCulvertRowsForTab04()
	{
		FilteredCulvertRowsForTab04.Clear();
		string type = SelectedCulvertTypeForTab04 ?? "TẤT CẢ";
		foreach (CulvertRowData filteredCulvertRow in FilteredCulvertRows)
		{
			int num;
			if (!(filteredCulvertRow.DetermineCulvertType() == "CỐNG KỸ THUẬT"))
			{
				string loaiCong = filteredCulvertRow.LoaiCong;
				if (loaiCong == null || loaiCong.IndexOf("KỸ THUẬT", StringComparison.OrdinalIgnoreCase) < 0)
				{
					string loaiCong2 = filteredCulvertRow.LoaiCong;
					if (loaiCong2 == null || loaiCong2.IndexOf("KY THUAT", StringComparison.OrdinalIgnoreCase) < 0)
					{
						string ghiChu = filteredCulvertRow.GhiChu;
						if (ghiChu == null || ghiChu.IndexOf("KỸ THUẬT", StringComparison.OrdinalIgnoreCase) < 0)
						{
							string ghiChu2 = filteredCulvertRow.GhiChu;
							num = ((ghiChu2 != null && ghiChu2.IndexOf("KY THUAT", StringComparison.OrdinalIgnoreCase) >= 0) ? 1 : 0);
							goto IL_02ce;
						}
					}
				}
			}
			num = 1;
			goto IL_02ce;
			IL_00d1:
			int num2;
			bool flag = (byte)num2 != 0;
			bool flag2;
			if (type == "TẤT CẢ")
			{
				FilteredCulvertRowsForTab04.Add(filteredCulvertRow);
			}
			else if ((type == "CỐNG KỸ THUẬT") & flag2)
			{
				FilteredCulvertRowsForTab04.Add(filteredCulvertRow);
			}
			else if (((type == "CỐNG ĐỔ TẠI CHỖ") & flag) && !flag2)
			{
				FilteredCulvertRowsForTab04.Add(filteredCulvertRow);
			}
			else if (CastInPlaceProfiles.Any((CulvertTypeProfile p) => p.TypeName == type))
			{
				if (string.Equals(filteredCulvertRow.AssignedProfileName, type, StringComparison.OrdinalIgnoreCase) || string.Equals(filteredCulvertRow.KhauDo, type, StringComparison.OrdinalIgnoreCase))
				{
					FilteredCulvertRowsForTab04.Add(filteredCulvertRow);
				}
				else if (flag && string.IsNullOrEmpty(filteredCulvertRow.AssignedProfileName) && CastInPlaceProfiles.FirstOrDefault()?.TypeName == type)
				{
					FilteredCulvertRowsForTab04.Add(filteredCulvertRow);
				}
			}
			else if (type == "CỐNG ĐÚC SẴN" && !flag && !flag2)
			{
				FilteredCulvertRowsForTab04.Add(filteredCulvertRow);
			}
			else if (type == "CỬA XẢ" && !flag2)
			{
				FilteredCulvertRowsForTab04.Add(filteredCulvertRow);
			}
			else if (type == "SÂN GIA CỐ" && (filteredCulvertRow.L_SGC_TL > 0.0 || filteredCulvertRow.L_SGC_HL > 0.0))
			{
				FilteredCulvertRowsForTab04.Add(filteredCulvertRow);
			}
			else if (type == "HỐ GA (HỘP NỐI)" && filteredCulvertRow.SoHopNoi > 0)
			{
				FilteredCulvertRowsForTab04.Add(filteredCulvertRow);
			}
			continue;
			IL_02ce:
			flag2 = (byte)num != 0;
			if (filteredCulvertRow.SoCua <= 1)
			{
				string cauKien = filteredCulvertRow.CauKien;
				if (cauKien == null || cauKien.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) < 0)
				{
					string cauKien2 = filteredCulvertRow.CauKien;
					if (cauKien2 == null || cauKien2.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) < 0)
					{
						string loaiCong3 = filteredCulvertRow.LoaiCong;
						if (loaiCong3 == null || loaiCong3.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) < 0)
						{
							string loaiCong4 = filteredCulvertRow.LoaiCong;
							if (loaiCong4 == null || loaiCong4.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) < 0)
							{
								string ghiChu3 = filteredCulvertRow.GhiChu;
								if (ghiChu3 == null || ghiChu3.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) < 0)
								{
									string ghiChu4 = filteredCulvertRow.GhiChu;
									num2 = ((ghiChu4 != null && ghiChu4.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) >= 0) ? 1 : 0);
									goto IL_00d1;
								}
							}
						}
					}
				}
			}
			num2 = 1;
			goto IL_00d1;
		}
	}

	public MainViewModel()
		: this(null, null, null)
	{
	}

	public MainViewModel(UIApplication uiApp, ExternalEvent externalEvent, RevitExternalEventHandler eventHandler)
	{
		StartupLogger.Log("[VM] MainViewModel constructor START");
		_isInitializing = true;
		_uiApp = uiApp;
		_externalEvent = externalEvent;
		_eventHandler = eventHandler;
		StartupLogger.Log("[VM] Basic fields assigned, initializing commands...");
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
		UpdateBimAndMaterialsCommand = new RelayCommand(ExecuteUpdateBimAndMaterials);
		CloseCommand = new RelayCommand(() =>
		{
			try
			{
				ProjectConfigStorageService.SaveToLastSession(ExportCurrentState());
			}
			catch { }
			RequestClose?.Invoke();
		});
		AddComponentCommand = new RelayCommand(AddNewComponent);
		RemoveComponentCommand = new RelayCommand(RemoveSelectedComponent);
		ApplyAssemblyTemplateCommand = new RelayCommand<string>(ApplyAssemblyTemplate);
		AddCastInPlaceProfileCommand = new RelayCommand(AddCastInPlaceProfile);
		RemoveCastInPlaceProfileCommand = new RelayCommand(RemoveCastInPlaceProfile);
		AssignProfileToSelectedCulvertsCommand = new RelayCommand(AssignProfileToSelectedCulverts);
		FilterTab02ByProfileCommand = new RelayCommand<object>((object? param) =>
		{
			if (param is CulvertTypeProfile culvertTypeProfile)
			{
				SelectedTab02GroupFilter = culvertTypeProfile.TypeName;
				ApplyTab02FamilyFilter();
			}
			else if (param is string selectedTab02GroupFilter)
			{
				SelectedTab02GroupFilter = selectedTab02GroupFilter;
				ApplyTab02FamilyFilter();
			}
		});
		Open3DReviewCommand = new RelayCommand(Open3DReview);
		SetTab02ParamCategoryFilterCommand = new RelayCommand<string>((string? filter) =>
		{
			if (!string.IsNullOrEmpty(filter))
			{
				Tab02ParamCategoryFilter = filter;
			}
		});
		SetPreviewModeCommand = new RelayCommand<string>(SetPreviewMode);
		FitPreviewCommand = new RelayCommand(() =>
		{
			RequestPreviewAction?.Invoke("Fit");
		});
		ZoomInPreviewCommand = new RelayCommand(() =>
		{
			RequestPreviewAction?.Invoke("ZoomIn");
		});
		ZoomOutPreviewCommand = new RelayCommand(() =>
		{
			RequestPreviewAction?.Invoke("ZoomOut");
		});
		RefreshPreviewCommand = new RelayCommand(() =>
		{
			CulvertRowData culvertRowData = SelectedCulvertRowForPreview ?? FilteredPreviewCulvertRows.FirstOrDefault();
			if (culvertRowData != null)
			{
				lock (_cachedRealMeshes)
				{
					_cachedRealMeshes.Remove(culvertRowData.STT);
				}
				ExtractRealFamilyGeometryForPreview(culvertRowData);
			}
			UpdatePreviewGeometry();
		});
		SetCulvertTypeForTab03Command = new RelayCommand<string>((string? type) =>
		{
			if (type == "CỐNG ĐỔ TẠI CHỖ")
			{
				SelectedCulvertTypeForTab03 = (SelectedCastInPlaceProfileForTab03 ?? CastInPlaceProfiles.FirstOrDefault())?.TypeName ?? "CỐNG ĐỔ TẠI CHỖ";
			}
			else
			{
				SelectedCulvertTypeForTab03 = type ?? "TẤT CẢ";
			}
		});
		ApplyStandardMaterialPresetCommand = new RelayCommand(ApplyStandardMaterialPreset);
		ApplyColorPresetCommand = new RelayCommand<string>(ApplyColorPreset);
		ScanRevitMaterialsCommand = new RelayCommand(ScanRevitMaterials);
		CreateSelectedMaterialInRevitCommand = new RelayCommand(CreateSelectedMaterialInRevit);
		SyncMaterialsCommand = new RelayCommand(SyncMaterialsFromAssemblyComponents);
		FilterMaterialGroupCommand = new RelayCommand<string>((string? group) =>
		{
			SelectedMaterialGroupFilter = group ?? "All";
		});
		AddMaterialItemCommand = new RelayCommand(AddNewMaterialItem);
		RemoveMaterialItemCommand = new RelayCommand(RemoveSelectedMaterialItem);
		OpenColorPickerCommand = new RelayCommand(OpenColorPicker);
		OpenColorPickerForItemCommand = new RelayCommand<CulvertMaterialItem>(OpenColorPickerForItem);
		ApplyMaterialToSelectedGroupCommand = new RelayCommand(ApplyMaterialToSelectedGroup);
		ApplyMaterialToCurrentGroupTypeCommand = new RelayCommand(ApplyMaterialToCurrentGroupType);
		SetCulvertTypeForTab04Command = new RelayCommand<string>((string? type) =>
		{
			if (type == "CỐNG ĐỔ TẠI CHỖ")
			{
				SelectedCulvertTypeForTab04 = (SelectedCastInPlaceProfileForTab04 ?? CastInPlaceProfiles.FirstOrDefault())?.TypeName ?? "CỐNG ĐỔ TẠI CHỖ";
			}
			else
			{
				SelectedCulvertTypeForTab04 = type ?? "TẤT CẢ";
			}
		});
		AddCustomBimParameterCommand = new RelayCommand(AddNewCustomBimParameter);
		RemoveCustomBimParameterCommand = new RelayCommand(RemoveSelectedCustomBimParameter);
		ResetDefaultBimParametersCommand = new RelayCommand(ResetDefaultCustomBimParameters);
		UpdateBimParametersOnlyCommand = new RelayCommand(ExecuteUpdateBimParametersOnly);
		CopyBimParametersToScopeCommand = new RelayCommand(ExecuteCopyBimParametersToScope);
		StartupLogger.Log("[VM] All commands created. Starting initialization try block...");
		try
		{
			StartupLogger.Log("[VM] Step 1: LoadAvailableFamilies START");
			LoadAvailableFamilies();
			StartupLogger.Log("[VM] Step 1: LoadAvailableFamilies DONE");

			StartupLogger.Log("[VM] Step 2: ApplyAssemblyTemplate START");
			ApplyAssemblyTemplate("CỐNG HỘP ĐƠN");
			StartupLogger.Log("[VM] Step 2: ApplyAssemblyTemplate DONE");

			StartupLogger.Log("[VM] Step 3: ScanRevitMaterials START");
			ScanRevitMaterials();
			StartupLogger.Log("[VM] Step 3: ScanRevitMaterials DONE");

			StartupLogger.Log("[VM] Step 4: SyncMaterialsFromAssemblyComponents START");
			SyncMaterialsFromAssemblyComponents();
			StartupLogger.Log("[VM] Step 4: SyncMaterialsFromAssemblyComponents DONE");

			StartupLogger.Log("[VM] Step 5: InitializeAllDefaultBimParameters START");
			InitializeAllDefaultBimParameters();
			StartupLogger.Log("[VM] Step 5: InitializeAllDefaultBimParameters DONE");

			StartupLogger.Log("[VM] Step 6: UpdateAvailableTargetScopesForTab04 START");
			UpdateAvailableTargetScopesForTab04();
			RefreshDisplayedCustomBimParameters();
			StartupLogger.Log("[VM] Step 6: UpdateAvailableTargetScopesForTab04 DONE");

			StartupLogger.Log("[VM] Step 7: UpdateFilteredCulvertRowsForTab04 START");
			UpdateFilteredCulvertRowsForTab04();
			StartupLogger.Log("[VM] Step 7: UpdateFilteredCulvertRowsForTab04 DONE");

			StartupLogger.Log("[VM] Step 8: UpdateInspectorCulvertRows START");
			UpdateInspectorCulvertRows();
			StartupLogger.Log("[VM] Step 8: UpdateInspectorCulvertRows DONE");

			// Persistence Commands
			SaveProjectConfigCommand = new RelayCommand(ExecuteSaveProjectConfig);
			ExportPresetCommand = new RelayCommand(ExecuteExportPreset);
			ImportPresetCommand = new RelayCommand(ExecuteImportPreset);
			RestoreConfigCommand = new RelayCommand(TryAutoLoadConfiguration);

			StartupLogger.Log("[VM] Step 9: TryAutoLoadConfiguration START");
			TryAutoLoadConfiguration();
			StartupLogger.Log("[VM] Step 9: TryAutoLoadConfiguration DONE");
		}
		catch (Exception ex)
		{
			StartupLogger.Log($"[VM] Constructor EXCEPTION: {ex}");
		}
		finally
		{
			_isInitializing = false;
		}

		StartupLogger.Log("[VM] Post-init: RefreshActiveAssignedFamiliesForTab02 START");
		RefreshActiveAssignedFamiliesForTab02();
		StartupLogger.Log("[VM] Post-init: ApplyTab02FamilyFilter START");
		ApplyTab02FamilyFilter();
		StartupLogger.Log("[VM] Post-init: ApplyMaterialGroupFilter START");
		ApplyMaterialGroupFilter();
		StartupLogger.Log("[VM] Post-init: UpdatePreviewGeometry START");
		UpdatePreviewGeometry();
		StartupLogger.Log("[VM] MainViewModel constructor COMPLETED SUCCESSFULLY!");
	}

	private void BrowseExcel()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "Tất cả bảng tính (*.xlsx;*.xlsm;*.csv)|*.xlsx;*.xlsm;*.csv|Excel Files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
			Title = "Chọn bảng dữ liệu cống ngang Excel hoặc CSV"
		};
		if (openFileDialog.ShowDialog() != true)
		{
			return;
		}
		ExcelFilePath = openFileDialog.FileName;
		SheetNames.Clear();
		foreach (string sheetName in ExcelReaderService.GetSheetNames(ExcelFilePath))
		{
			SheetNames.Add(sheetName);
		}
		if (SheetNames.Count > 0)
		{
			SelectedSheetName = SheetNames[0];
		}
	}

	private void BrowseFamilyFolder()
	{
		FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			Description = "Chọn thư mục chứa Family Revit (.rfa) để nạp tự động vào Revit",
			SelectedPath = (Directory.Exists(FamilyFolderPath) ? FamilyFolderPath : "C:\\Users\\ADMIN\\Desktop\\TEST TOOL\\FAMLY REVIT_HTKT"),
			ShowNewFolderButton = false
		};
		try
		{
			if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
			{
				FamilyFolderPath = folderBrowserDialog.SelectedPath;
				ExecuteLoadFamiliesFromFolder();
			}
		}
		finally
		{
			((IDisposable)(object)folderBrowserDialog)?.Dispose();
		}
	}

	public void ExecuteLoadFamiliesFromFolder()
	{
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrWhiteSpace(FamilyFolderPath) || !Directory.Exists(FamilyFolderPath))
		{
			System.Windows.MessageBox.Show("Thư mục Family không tồn tại hoặc chưa được chọn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		string[] rfaFiles = Directory.GetFiles(FamilyFolderPath, "*.rfa", SearchOption.AllDirectories);
		if (rfaFiles.Length == 0)
		{
			System.Windows.MessageBox.Show("Không tìm thấy file .rfa nào trong thư mục đã chọn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		FamilyLoadStatus = $"Đang nạp {rfaFiles.Length} Family vào Revit...";
		_eventHandler.SetAction((UIApplication app) =>
		{
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Expected Obj, but got Unknown
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Expected Obj, but got Unknown
			UIDocument activeUIDocument = app.ActiveUIDocument;
			Document val = ((activeUIDocument != null) ? activeUIDocument.Document : null);
			if (val != null)
			{
				int loadedCount = 0;
				int num = 0;
				Transaction val2 = new Transaction(val, "Load Culvert Families");
				try
				{
					val2.Start();
					CustomFamilyLoadOptions customFamilyLoadOptions = new CustomFamilyLoadOptions();
					string[] array = rfaFiles;
					Family val3 = null;
					string[] array2 = array;
					foreach (string text in array2)
					{
						try
						{
							if (val.LoadFamily(text, (IFamilyLoadOptions)(object)customFamilyLoadOptions, out val3))
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
							num++;
						}
					}
					val2.Commit();
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
				((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)(() =>
				{
					LoadAvailableFamilies();
					FamilyLoadStatus = $"Đã nạp {loadedCount}/{rfaFiles.Length} Family thành công!";
				}));
				try
				{
					CulvertRowData curTarget = SelectedCulvertRowForPreview ?? FilteredPreviewCulvertRows.FirstOrDefault() ?? FilteredCulvertRows.FirstOrDefault() ?? AllCulvertRows.FirstOrDefault();
					if (curTarget == null)
					{
						curTarget = CreateDefaultPreviewCulvertRow();
						((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)(() =>
						{
							SelectedCulvertRowForPreview = curTarget;
						}));
					}
					if (curTarget != null && RevitCulvertMeshExtractor.TryExtractPreviewGeometry(val, curTarget, this, out List<(MeshGeometry3D, System.Windows.Media.Brush)> result) && result.Count > 0)
					{
						lock (_cachedRealMeshes)
						{
							_cachedRealMeshes[curTarget.STT] = result;
						}
					}
					((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)(() =>
					{
						UpdatePreviewGeometry();
						OnPropertyChanged("CurrentPreviewGeometry");
					}));
				}
				catch
				{
				}
				((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)(() =>
				{
					System.Windows.MessageBox.Show($"Đã tự động nạp thành công {loadedCount}/{rfaFiles.Length} Family vào Revit!\nĐã trích xuất và nạp sẵn toàn bộ hình dạng hình học 3D từ Family cho Review 3D.", "Nạp Family Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}));
			}
		});
		_externalEvent.Raise();
	}

	private void CreateSampleExcel()
	{
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
		{
			Filter = "Excel Workbook (*.xlsx)|*.xlsx",
			FileName = "Mau_Du_Lieu_Cong_Ngang.xlsx",
			Title = "Lưu file Excel mẫu 19 cột chuẩn"
		};
		if (saveFileDialog.ShowDialog() != true)
		{
			return;
		}
		ExcelReaderService.CreateSampleExcelTemplate(saveFileDialog.FileName);
		ExcelFilePath = saveFileDialog.FileName;
		SheetNames.Clear();
		foreach (string sheetName in ExcelReaderService.GetSheetNames(ExcelFilePath))
		{
			SheetNames.Add(sheetName);
		}
		if (SheetNames.Count > 0)
		{
			SelectedSheetName = SheetNames[0];
		}
		System.Windows.MessageBox.Show("Đã tạo thành công file Excel mẫu tại:\n" + saveFileDialog.FileName, "Thành công", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	private void ReloadExcelData()
	{
		if (string.IsNullOrEmpty(ExcelFilePath) || !File.Exists(ExcelFilePath))
		{
			return;
		}
		try
		{
			string selectedSheetName = SelectedSheetName;
			List<string> sheetNames = ExcelReaderService.GetSheetNames(ExcelFilePath);
			SheetNames.Clear();
			foreach (string item in sheetNames)
			{
				SheetNames.Add(item);
			}
			if (!string.IsNullOrEmpty(selectedSheetName) && SheetNames.Contains(selectedSheetName))
			{
				SelectedSheetName = selectedSheetName;
			}
			else if (SheetNames.Count > 0)
			{
				SelectedSheetName = SheetNames[0];
			}
			LoadExcelData();
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("Lỗi đọc lại file Excel: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void LoadExcelData()
	{
		if (string.IsNullOrEmpty(ExcelFilePath) || !File.Exists(ExcelFilePath))
		{
			return;
		}
		try
		{
			List<CulvertRowData> list = ExcelReaderService.ReadCulvertRows(ExcelFilePath, SelectedSheetName);
			AllCulvertRows.Clear();
			foreach (CulvertRowData item in list)
			{
				AllCulvertRows.Add(item);
			}
			RestoreCurrentSheetOverrides();
			if (AllCulvertRows.Count > 0)
			{
				BatchFromSTT = AllCulvertRows.Min((CulvertRowData r) => r.STT);
				BatchToSTT = AllCulvertRows.Max((CulvertRowData r) => r.STT);
			}
			FilterCulvertRows();
			RunValidation();
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("Lỗi đọc file Excel: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void FilterCulvertRows()
	{
		FilteredCulvertRows.Clear();
		foreach (CulvertRowData allCulvertRow in AllCulvertRows)
		{
			string text = (allCulvertRow.LoaiCong ?? "").ToUpperInvariant();
			string text2 = (allCulvertRow.ResolvedCulvertType ?? "").ToUpperInvariant();
			if (SelectedTypeFilter == "TẤT CẢ")
			{
				FilteredCulvertRows.Add(allCulvertRow);
			}
			else if (SelectedTypeFilter == "CỐNG HỘP" && (text.Contains("HOP") || text.Contains("HỘP") || text2.Contains("HỘP")))
			{
				FilteredCulvertRows.Add(allCulvertRow);
			}
			else if (SelectedTypeFilter == "CỐNG TRÒN" && (text.Contains("TRON") || text.Contains("TRÒN") || text2.Contains("TRÒN")))
			{
				FilteredCulvertRows.Add(allCulvertRow);
			}
			else if (SelectedTypeFilter == "CỐNG ĐÔI" && (allCulvertRow.SoCua >= 2 || text2.Contains("ĐÔI")))
			{
				FilteredCulvertRows.Add(allCulvertRow);
			}
		}
		FilterPreviewCulvertRows();
		UpdateFilteredCulvertRowsForTab04();
		UpdateInspectorCulvertRows();
	}

	private FamilySymbolWrapper? FindSymbol(string familyKeyword, string? typeKeyword = null)
	{
		List<FamilySymbolWrapper> list = AllAvailableFamilies.Where((FamilySymbolWrapper f) => string.Equals(f.FamilyName, familyKeyword, StringComparison.OrdinalIgnoreCase)).ToList();
		if (list.Count > 0)
		{
			if (string.IsNullOrEmpty(typeKeyword))
			{
				return list.FirstOrDefault();
			}
			FamilySymbolWrapper familySymbolWrapper = list.FirstOrDefault((FamilySymbolWrapper f) => f.TypeName.IndexOf(typeKeyword, StringComparison.OrdinalIgnoreCase) >= 0);
			if (familySymbolWrapper != null)
			{
				return familySymbolWrapper;
			}
		}
		List<FamilySymbolWrapper> source = AllAvailableFamilies.Where((FamilySymbolWrapper f) => f.FamilyName.IndexOf(familyKeyword, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
		if (!source.Any())
		{
			source = AllAvailableFamilies.Where((FamilySymbolWrapper f) => f.TypeName.IndexOf(familyKeyword, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
		}
		if (!source.Any())
		{
			return list.FirstOrDefault();
		}
		if (!string.IsNullOrEmpty(typeKeyword))
		{
			FamilySymbolWrapper familySymbolWrapper2 = source.FirstOrDefault((FamilySymbolWrapper f) => f.TypeName.IndexOf(typeKeyword, StringComparison.OrdinalIgnoreCase) >= 0);
			if (familySymbolWrapper2 != null)
			{
				return familySymbolWrapper2;
			}
		}
		return list.FirstOrDefault() ?? source.FirstOrDefault();
	}

	private void LoadAvailableFamilies()
	{
		StartupLogger.Log("[VM] LoadAvailableFamilies: START");
		AllAvailableFamilies.Clear();
		StartupLogger.Log("[VM] LoadAvailableFamilies: Collector START");
		foreach (FamilySymbol item2 in ((IEnumerable)new FilteredElementCollector(Doc).OfClass(typeof(FamilySymbol))).Cast<FamilySymbol>())
		{
			if (item2 != null && ((Element)item2).IsValidObject)
			{
				FamilySymbolWrapper item = new FamilySymbolWrapper(item2);
				AllAvailableFamilies.Add(item);
			}
		}
		StartupLogger.Log($"[VM] LoadAvailableFamilies: Collector DONE. Found {AllAvailableFamilies.Count} symbols.");
		StartupLogger.Log("[VM] LoadAvailableFamilies: InitPrecastBarrelComponents...");
		InitPrecastBarrelComponents(forceRefresh: true);
		StartupLogger.Log("[VM] LoadAvailableFamilies: InitCastInPlaceProfiles...");
		InitCastInPlaceProfiles(forceRefresh: true);
		StartupLogger.Log("[VM] LoadAvailableFamilies: InitTechnicalBarrelComponents...");
		InitTechnicalBarrelComponents(forceRefresh: true);
		StartupLogger.Log("[VM] LoadAvailableFamilies: InitOutletComponents...");
		InitOutletComponents(forceRefresh: true);
		StartupLogger.Log("[VM] LoadAvailableFamilies: InitApronComponents...");
		InitApronComponents(forceRefresh: true);
		StartupLogger.Log("[VM] LoadAvailableFamilies: InitManholeComponents...");
		InitManholeComponents(forceRefresh: true);
		StartupLogger.Log("[VM] LoadAvailableFamilies: InitTechnicalManholeComponents...");
		InitTechnicalManholeComponents(forceRefresh: true);
		StartupLogger.Log("[VM] LoadAvailableFamilies: calling ApplyAssemblyTemplate...");
		ApplyAssemblyTemplate(SelectedCulvertTemplateType ?? "CỐNG HỘP ĐÚC SẴN");
		StartupLogger.Log("[VM] LoadAvailableFamilies: END");
	}

	private void InitPrecastBarrelComponents(bool forceRefresh = false)
	{
		if (!forceRefresh && PrecastBarrelComponents.Count > 0)
		{
			return;
		}
		foreach (CulvertComponentItem precastBarrelComponent in PrecastBarrelComponents)
		{
			precastBarrelComponent.PropertyChanged -= OnAssemblyComponentPropertyChanged;
		}
		PrecastBarrelComponents.Clear();
		PrecastBarrelComponents.Add(new CulvertComponentItem
		{
			IsActive = true,
			GroupType = "Cống đúc sẵn",
			ProfileName = "Cống đúc sẵn",
			CategoryType = "Thân cống hộp đúc sẵn",
			SelectedSymbol = (FindSymbol("TNN_CH_THAN CONG", "1.5x1.5") ?? FindSymbol("TNN_CH_THAN CONG")),
			OffsetZ = 0.0,
			Note = "Đốt thân cống hộp đúc sẵn"
		});
		PrecastBarrelComponents.Add(new CulvertComponentItem
		{
			IsActive = true,
			GroupType = "Cống đúc sẵn",
			ProfileName = "Cống đúc sẵn",
			CategoryType = "Bê tông lót thân cống",
			SelectedSymbol = (FindSymbol("TNN_CH_BE TONG LOT") ?? FindSymbol("TNN_CH_DEM CONG")),
			OffsetZ = -0.1,
			Note = "Lớp bê tông lót thân cống đúc sẵn"
		});
		PrecastBarrelComponents.Add(new CulvertComponentItem
		{
			IsActive = true,
			GroupType = "Cống đúc sẵn",
			ProfileName = "Cống đúc sẵn",
			CategoryType = "Đá dăm đệm thân cống",
			SelectedSymbol = FindSymbol("TNN_CH_DA DAM DEM"),
			OffsetZ = -0.2,
			Note = "Lớp đá dăm đệm thân cống đúc sẵn"
		});
		foreach (CulvertComponentItem precastBarrelComponent2 in PrecastBarrelComponents)
		{
			precastBarrelComponent2.PropertyChanged += OnAssemblyComponentPropertyChanged;
		}
	}

	private void InitCastInPlaceProfiles(bool forceRefresh = false)
	{
		if (!forceRefresh && CastInPlaceProfiles.Count > 0)
		{
			return;
		}
		foreach (CulvertTypeProfile castInPlaceProfile in CastInPlaceProfiles)
		{
			foreach (CulvertComponentItem component in castInPlaceProfile.Components)
			{
				component.PropertyChanged -= OnAssemblyComponentPropertyChanged;
			}
		}
		CastInPlaceProfiles.Clear();
		CulvertTypeProfile culvertTypeProfile = CreateNewCastInPlaceProfile("Cống đổ tại chỗ 1");
		CastInPlaceProfiles.Add(culvertTypeProfile);
		SelectedCastInPlaceProfile = culvertTypeProfile;
	}

	private void InitCastInPlaceBarrelComponents(bool forceRefresh = false)
	{
		InitCastInPlaceProfiles(forceRefresh);
	}

	public CulvertTypeProfile CreateNewCastInPlaceProfile(string profileName, CulvertTypeProfile? template = null)
	{
		CulvertTypeProfile culvertTypeProfile = new CulvertTypeProfile
		{
			TypeName = profileName,
			CulvertKind = "CastInPlace",
			L_Std = (template?.L_Std ?? 4.0),
			Khe_Ho = (template?.Khe_Ho ?? 0.02),
			SelectedArrayModeIndex = (template?.SelectedArrayModeIndex ?? 0)
		};
		culvertTypeProfile.Components.Add(new CulvertComponentItem
		{
			IsActive = true,
			GroupType = profileName,
			ProfileName = profileName,
			CategoryType = "Cống hộp (liên ngoài trái)",
			SelectedSymbol = (template?.Components.ElementAtOrDefault(0)?.SelectedSymbol ?? FindSymbol("TNN_CH_THAN CONG_2x3x2") ?? FindSymbol("TNN_CH_THAN CONG")),
			OffsetZ = 0.0,
			Note = "Đốt thân cống hộp đổ tại chỗ liên ngoài trái (tiếp xúc cửa xả TL)"
		});
		culvertTypeProfile.Components.Add(new CulvertComponentItem
		{
			IsActive = true,
			GroupType = profileName,
			ProfileName = profileName,
			CategoryType = "Cống hộp (liên trong)",
			SelectedSymbol = (template?.Components.ElementAtOrDefault(1)?.SelectedSymbol ?? FindSymbol("TNN_CH_THAN CONG_2x3x2") ?? FindSymbol("TNN_CH_THAN CONG")),
			OffsetZ = 0.0,
			Note = "Đốt thân cống hộp đổ tại chỗ liên trong (các đốt ở giữa)"
		});
		culvertTypeProfile.Components.Add(new CulvertComponentItem
		{
			IsActive = true,
			GroupType = profileName,
			ProfileName = profileName,
			CategoryType = "Cống hộp (liên ngoài phải)",
			SelectedSymbol = (template?.Components.ElementAtOrDefault(2)?.SelectedSymbol ?? FindSymbol("TNN_CH_THAN CONG_2x3x2") ?? FindSymbol("TNN_CH_THAN CONG")),
			OffsetZ = 0.0,
			Note = "Đốt thân cống hộp đổ tại chỗ liên ngoài phải (tiếp xúc cửa xả HL)"
		});
		culvertTypeProfile.Components.Add(new CulvertComponentItem
		{
			IsActive = true,
			GroupType = profileName,
			ProfileName = profileName,
			CategoryType = "Bê tông lót thân cống",
			SelectedSymbol = (template?.Components.ElementAtOrDefault(3)?.SelectedSymbol ?? FindSymbol("TNN_CH_BE TONG LOT_2x3x2") ?? FindSymbol("TNN_CH_DEM CONG_2x3x2") ?? FindSymbol("TNN_CH_BE TONG LOT")),
			OffsetZ = -0.1,
			Note = "Lớp đệm / bê tông lót thân cống đổ tại chỗ"
		});
		culvertTypeProfile.Components.Add(new CulvertComponentItem
		{
			IsActive = true,
			GroupType = profileName,
			ProfileName = profileName,
			CategoryType = "Đá dăm đệm thân cống",
			SelectedSymbol = (template?.Components.ElementAtOrDefault(4)?.SelectedSymbol ?? FindSymbol("TNN_CH_DA DAM DEM_2x3x2") ?? FindSymbol("TNN_CH_DA DAM DEM")),
			OffsetZ = -0.2,
			Note = "Lớp đá dăm đệm thân cống đổ tại chỗ"
		});
		foreach (CulvertComponentItem component in culvertTypeProfile.Components)
		{
			component.PropertyChanged += OnAssemblyComponentPropertyChanged;
		}
		culvertTypeProfile.PropertyChanged += OnCastInPlaceProfilePropertyChanged;
		return culvertTypeProfile;
	}

	public void SyncCastInPlaceBarrelComponents()
	{
		foreach (CulvertComponentItem castInPlaceBarrelComponent in CastInPlaceBarrelComponents)
		{
			castInPlaceBarrelComponent.PropertyChanged -= OnAssemblyComponentPropertyChanged;
		}
		CastInPlaceBarrelComponents.Clear();
		if (SelectedCastInPlaceProfile == null)
		{
			return;
		}
		foreach (CulvertComponentItem component in SelectedCastInPlaceProfile.Components)
		{
			component.PropertyChanged -= OnAssemblyComponentPropertyChanged;
			CastInPlaceBarrelComponents.Add(component);
			component.PropertyChanged += OnAssemblyComponentPropertyChanged;
		}
		_lStdCastInPlace = SelectedCastInPlaceProfile.L_Std;
		_lStdCastInPlaceText = SelectedCastInPlaceProfile.L_Std_Text;
		_kheHoCastInPlace = SelectedCastInPlaceProfile.Khe_Ho;
		_kheHoCastInPlaceText = SelectedCastInPlaceProfile.Khe_Ho_Text;
		_selectedArrayModeIndexCastInPlace = SelectedCastInPlaceProfile.SelectedArrayModeIndex;
		OnPropertyChanged("L_Std_CastInPlace");
		OnPropertyChanged("L_Std_CastInPlace_Text");
		OnPropertyChanged("Khe_Ho_CastInPlace");
		OnPropertyChanged("Khe_Ho_CastInPlace_Text");
		OnPropertyChanged("SelectedArrayModeIndex_CastInPlace");
	}

	public void AddCastInPlaceProfile()
	{
		int value = CastInPlaceProfiles.Count + 1;
		string text = $"Cống đổ tại chỗ {value}";
		CulvertTypeProfile culvertTypeProfile = CreateNewCastInPlaceProfile(text, SelectedCastInPlaceProfile);
		CastInPlaceProfiles.Add(culvertTypeProfile);
		SelectedCastInPlaceProfile = culvertTypeProfile;
		RefreshActiveAssignedFamiliesForTab02();
		ApplyTab02FamilyFilter();
		ApplyMaterialGroupFilter();
		System.Windows.MessageBox.Show("Đã thêm mới '" + text + "'. Bạn có thể chọn Family riêng và tùy chỉnh thông số độc lập cho loại cống này.", "Thêm loại cống thành công", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	public void RemoveCastInPlaceProfile()
	{
		if (CastInPlaceProfiles.Count <= 1)
		{
			System.Windows.MessageBox.Show("Dự án phải giữ lại ít nhất 1 loại cống đổ tại chỗ.", "Không thể xóa", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
		else
		{
			if (SelectedCastInPlaceProfile == null)
			{
				return;
			}
			CulvertTypeProfile selectedCastInPlaceProfile = SelectedCastInPlaceProfile;
			if (System.Windows.MessageBox.Show("Bạn có chắc chắn muốn xóa '" + selectedCastInPlaceProfile.TypeName + "'?", "Xác nhận xóa loại cống", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
			{
				CastInPlaceProfiles.Remove(selectedCastInPlaceProfile);
				if (SelectedCastInPlaceProfileForTab02 == selectedCastInPlaceProfile)
				{
					SelectedCastInPlaceProfileForTab02 = CastInPlaceProfiles.LastOrDefault();
				}
				if (SelectedCastInPlaceProfileForTab03 == selectedCastInPlaceProfile)
				{
					SelectedCastInPlaceProfileForTab03 = CastInPlaceProfiles.LastOrDefault();
				}
				if (SelectedCastInPlaceProfileForTab04 == selectedCastInPlaceProfile)
				{
					SelectedCastInPlaceProfileForTab04 = CastInPlaceProfiles.LastOrDefault();
				}
				SelectedCastInPlaceProfile = CastInPlaceProfiles.LastOrDefault();
				RefreshActiveAssignedFamiliesForTab02();
				ApplyTab02FamilyFilter();
				ApplyMaterialGroupFilter();
			}
		}
	}

	public void AssignProfileToSelectedCulverts()
	{
		if (SelectedCastInPlaceProfile == null)
		{
			System.Windows.MessageBox.Show("Vui lòng chọn loại cống đổ tại chỗ cần gán.", "Chưa chọn loại cống", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		List<CulvertRowData> list = AllCulvertRows.Where((CulvertRowData r) => r.IsSelected).ToList();
		if (list.Count == 0)
		{
			System.Windows.MessageBox.Show("Vui lòng chọn hoặc quét các cống trong bảng để gán loại cống này.", "Chưa chọn cống", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		foreach (CulvertRowData item in list)
		{
			item.AssignedProfileName = SelectedCastInPlaceProfile.TypeName;
			item.CauKien = "Đổ tại chỗ";
			item.StatusNote = "Đã gán " + SelectedCastInPlaceProfile.TypeName;
		}
		UpdateInspectorCulvertRows();
		UpdatePreviewGeometry();
		System.Windows.MessageBox.Show($"Đã gán '{SelectedCastInPlaceProfile.TypeName}' cho {list.Count} cống đã chọn thành công!", "Hoàn tất gán loại cống", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	private void InitOutletComponents(bool forceRefresh = false)
	{
		if (forceRefresh || OutletComponents.Count <= 0)
		{
			OutletComponents.Clear();
			OutletComponents.Add(new CulvertComponentItem
			{
				IsActive = true,
				GroupType = "Cửa xả",
				ProfileName = "Cửa xả",
				CategoryType = "Cửa xả - Tường đầu",
				SelectedSymbol = (FindSymbol("TNN_CX_SAN CONG", "TUONG DAU") ?? FindSymbol("TNN_CX_TUONG DAU")),
				OffsetZ = 0.0,
				Note = "Tường đầu cửa xả"
			});
			OutletComponents.Add(new CulvertComponentItem
			{
				IsActive = true,
				GroupType = "Cửa xả",
				ProfileName = "Cửa xả",
				CategoryType = "Cửa xả - Tường cánh",
				SelectedSymbol = (FindSymbol("TNN_CX_SAN CONG", "TUONG CANH") ?? FindSymbol("TNN_CX_TUONG CANH")),
				OffsetZ = 0.0,
				Note = "Tường cánh cửa xả"
			});
			OutletComponents.Add(new CulvertComponentItem
			{
				IsActive = true,
				GroupType = "Cửa xả",
				ProfileName = "Cửa xả",
				CategoryType = "Cửa xả - Sân cống",
				SelectedSymbol = (FindSymbol("TNN_CX_SAN CONG", "SAN CONG") ?? FindSymbol("TNN_CX_SAN CONG")),
				OffsetZ = 0.0,
				Note = "Sân cống cửa xả"
			});
			OutletComponents.Add(new CulvertComponentItem
			{
				IsActive = true,
				GroupType = "Cửa xả",
				ProfileName = "Cửa xả",
				CategoryType = "Cửa xả - Bê tông lót",
				SelectedSymbol = (FindSymbol("TNN_CX_SAN CONG", "BE TONG LOT") ?? FindSymbol("TNN_CX_BE TONG LOT")),
				OffsetZ = 0.0,
				Note = "Bê tông lót cửa xả"
			});
			OutletComponents.Add(new CulvertComponentItem
			{
				IsActive = true,
				GroupType = "Cửa xả",
				ProfileName = "Cửa xả",
				CategoryType = "Cửa xả - Đá dăm đệm",
				SelectedSymbol = (FindSymbol("TNN_CX_SAN CONG", "DA DAM DEM") ?? FindSymbol("TNN_CX_DA DAM DEM")),
				OffsetZ = 0.0,
				Note = "Lớp đá dăm đệm cửa xả"
			});
		}
	}

	private void InitApronComponents(bool forceRefresh = false)
	{
		if (forceRefresh || ApronComponents.Count <= 0)
		{
			ApronComponents.Clear();
			ApronComponents.Add(new CulvertComponentItem
			{
				IsActive = true,
				GroupType = "Sân gia cố",
				ProfileName = "Sân gia cố",
				CategoryType = "Sân gia cố - Tấm sân",
				SelectedSymbol = (FindSymbol("TNN_CX_SAN GIA CO", "SAN GIA CO") ?? FindSymbol("TNN_CX_SGC_SAN GIA CO") ?? FindSymbol("TNN_CX_SAN GIA CO")),
				OffsetZ = 0.0,
				Note = "Tấm sân gia cố nối dài"
			});
			ApronComponents.Add(new CulvertComponentItem
			{
				IsActive = true,
				GroupType = "Sân gia cố",
				ProfileName = "Sân gia cố",
				CategoryType = "Sân gia cố - Bê tông lót",
				SelectedSymbol = (FindSymbol("TNN_CX_SAN GIA CO", "BE TONG LOT") ?? FindSymbol("TNN_CX_SGC_BE TONG LOT")),
				OffsetZ = 0.0,
				Note = "Bê tông lót sân gia cố"
			});
		}
	}

	private void InitManholeComponents(bool forceRefresh = false)
	{
		if (forceRefresh || ManholeComponents.Count <= 0)
		{
			ManholeComponents.Clear();
			FamilySymbolWrapper familySymbolWrapper = FindSymbol("HOP NOI CONG DOC") ?? FindSymbol("HOP NOI");
			if (familySymbolWrapper != null)
			{
				ManholeComponents.Add(new CulvertComponentItem
				{
					IsActive = true,
					GroupType = "Hố ga (Hộp nối)",
					CategoryType = "Hộp nối cống dọc",
					SelectedSymbol = familySymbolWrapper,
					OffsetZ = 0.0,
					Note = "Hộp nối cống dọc thoát nước"
				});
				return;
			}
			ManholeComponents.Add(new CulvertComponentItem
			{
				IsActive = true,
				GroupType = "Hố ga (Hộp nối)",
				CategoryType = "Thân hố ga",
				SelectedSymbol = (FindSymbol("TNM_HG_DO TAI CHO") ?? FindSymbol("TNM_HG_DUC SAN") ?? FindSymbol("TNM_HG")),
				OffsetZ = 0.0,
				Note = "Thân buồng thu hố ga (hộp nối)"
			});
			ManholeComponents.Add(new CulvertComponentItem
			{
				IsActive = true,
				GroupType = "Hố ga (Hộp nối)",
				CategoryType = "Cổ giếng",
				SelectedSymbol = (FindSymbol("TNM_HG_CO GIENG") ?? FindSymbol("CO GIENG")),
				OffsetZ = 0.5,
				Note = "Cổ giếng hố ga"
			});
			ManholeComponents.Add(new CulvertComponentItem
			{
				IsActive = true,
				GroupType = "Hố ga (Hộp nối)",
				CategoryType = "Khuôn hầm",
				SelectedSymbol = (FindSymbol("TNM_HG_KHUON HAM") ?? FindSymbol("KHUON HAM")),
				OffsetZ = 0.8,
				Note = "Khuôn đỡ nắp hầm ga"
			});
			ManholeComponents.Add(new CulvertComponentItem
			{
				IsActive = true,
				GroupType = "Hố ga (Hộp nối)",
				CategoryType = "Nắp đan",
				SelectedSymbol = (FindSymbol("TNM_HG_NAP DAN") ?? FindSymbol("NAP DAN")),
				OffsetZ = 0.9,
				Note = "Tấm đan nắp đậy hố ga"
			});
			ManholeComponents.Add(new CulvertComponentItem
			{
				IsActive = true,
				GroupType = "Hố ga (Hộp nối)",
				CategoryType = "Bê tông lót hố ga",
				SelectedSymbol = (FindSymbol("TNM_HG_BE TONG LOT") ?? FindSymbol("BE TONG LOT")),
				OffsetZ = -0.2,
				Note = "Bê tông lót đáy hố ga"
			});
		}
	}

	private void InitTechnicalBarrelComponents(bool forceRefresh = false)
	{
		if (!forceRefresh && TechnicalBarrelComponents.Count > 0)
		{
			return;
		}
		foreach (CulvertComponentItem technicalBarrelComponent in TechnicalBarrelComponents)
		{
			technicalBarrelComponent.PropertyChanged -= OnAssemblyComponentPropertyChanged;
		}
		TechnicalBarrelComponents.Clear();
		TechnicalBarrelComponents.Add(new CulvertComponentItem
		{
			IsActive = true,
			GroupType = "Cống kỹ thuật",
			ProfileName = "Cống kỹ thuật",
			CategoryType = "Thân cống kỹ thuật",
			SelectedSymbol = (FindSymbol("TNN_CKT_THAN CONG") ?? FindSymbol("TNN_CH_THAN CONG")),
			OffsetZ = 0.0,
			Note = "Đốt thân cống kỹ thuật đúc sẵn (1.0m)"
		});
		TechnicalBarrelComponents.Add(new CulvertComponentItem
		{
			IsActive = true,
			GroupType = "Cống kỹ thuật",
			ProfileName = "Cống kỹ thuật",
			CategoryType = "Bê tông lót cống kỹ thuật",
			SelectedSymbol = (FindSymbol("TNN_CKT_BE TONG LOT") ?? FindSymbol("TNN_CH_BE TONG LOT")),
			OffsetZ = -0.1,
			Note = "Lớp bê tông lót cống kỹ thuật"
		});
		TechnicalBarrelComponents.Add(new CulvertComponentItem
		{
			IsActive = true,
			GroupType = "Cống kỹ thuật",
			ProfileName = "Cống kỹ thuật",
			CategoryType = "Đá dăm đệm cống kỹ thuật",
			SelectedSymbol = (FindSymbol("TNN_CKT_DA DAM DEM") ?? FindSymbol("TNN_CH_DA DAM DEM")),
			OffsetZ = -0.2,
			Note = "Lớp đá dăm đệm cống kỹ thuật"
		});
		foreach (CulvertComponentItem technicalBarrelComponent2 in TechnicalBarrelComponents)
		{
			technicalBarrelComponent2.PropertyChanged += OnAssemblyComponentPropertyChanged;
		}
	}

	private void InitTechnicalManholeComponents(bool forceRefresh = false)
	{
		if (!forceRefresh && TechnicalManholeComponents.Count > 0)
		{
			return;
		}
		foreach (CulvertComponentItem technicalManholeComponent in TechnicalManholeComponents)
		{
			technicalManholeComponent.PropertyChanged -= OnAssemblyComponentPropertyChanged;
		}
		TechnicalManholeComponents.Clear();
		TechnicalManholeComponents.Add(new CulvertComponentItem
		{
			IsActive = true,
			GroupType = "Hố kỹ thuật",
			CategoryType = "Hố kỹ thuật 1.5x1.5m",
			SelectedSymbol = (FindSymbol("TNN_CKT_HO KY THUAT 1.5x1.5M") ?? FindSymbol("TNN_CKT_HO KY THUAT") ?? FindSymbol("HO KY THUAT")),
			OffsetZ = 0.0,
			Note = "Hố kỹ thuật tại 2 đầu tuyến"
		});
		TechnicalManholeComponents.Add(new CulvertComponentItem
		{
			IsActive = true,
			GroupType = "Hộp nối cống",
			CategoryType = "Hộp nối cống dọc",
			SelectedSymbol = (FindSymbol("HOP NOI CONG DOC") ?? FindSymbol("HOP NOI") ?? FindSymbol("TNN_CKT_HO KY THUAT 1.5x1.5M") ?? FindSymbol("HO KY THUAT")),
			OffsetZ = 0.0,
			Note = "Hộp nối giữa cống kỹ thuật"
		});
		foreach (CulvertComponentItem technicalManholeComponent2 in TechnicalManholeComponents)
		{
			technicalManholeComponent2.PropertyChanged += OnAssemblyComponentPropertyChanged;
		}
	}

	private void AddNewComponent()
	{
	}

	private void RemoveSelectedComponent()
	{
	}

	public void ApplyAssemblyTemplate(string? templateType)
	{
		StartupLogger.Log($"[VM] ApplyAssemblyTemplate START: {templateType}");
		string obj = templateType?.ToUpperInvariant() ?? "CỐNG HỘP ĐÚC SẴN";
		string text = obj;
		SelectedCulvertTemplateType = obj;
		string text2 = text;
		foreach (CulvertComponentItem barrelComponent in BarrelComponents)
		{
			barrelComponent.PropertyChanged -= OnAssemblyComponentPropertyChanged;
		}
		BarrelComponents.Clear();
		if (text2.Contains("KỸ THUẬT") || text2.Contains("KY THUAT") || text2.Contains("TECHNICAL") || text2.Contains("CKT"))
		{
			IsTechnicalCulvertMode = true;
			IsCastInPlaceMode = false;
			IsDoubleCulvertMode = false;
			L_Std = 1.0;
			Khe_Ho = 0.01;
			FamilyFolderPath = "C:\\Users\\ADMIN\\Desktop\\TEST TOOL\\FAMLY REVIT_HTKT\\2. CỐNG KỸ THUẬT";
			if (TechnicalBarrelComponents.Count == 0)
			{
				InitTechnicalBarrelComponents(forceRefresh: true);
			}
			if (TechnicalManholeComponents.Count == 0)
			{
				InitTechnicalManholeComponents(forceRefresh: true);
			}
			foreach (CulvertComponentItem technicalBarrelComponent in TechnicalBarrelComponents)
			{
				BarrelComponents.Add(technicalBarrelComponent);
			}
		}
		else if (text2.Contains("ĐỔ TẠI CHỖ") || text2.Contains("DO TAI CHO"))
		{
			IsTechnicalCulvertMode = false;
			IsCastInPlaceMode = true;
			IsDoubleCulvertMode = false;
			L_Std = 4.0;
			Khe_Ho = 0.02;
			if (CastInPlaceBarrelComponents.Count == 0)
			{
				InitCastInPlaceBarrelComponents(forceRefresh: true);
			}
			foreach (CulvertComponentItem castInPlaceBarrelComponent in CastInPlaceBarrelComponents)
			{
				BarrelComponents.Add(castInPlaceBarrelComponent);
			}
		}
		else
		{
			IsTechnicalCulvertMode = false;
			IsCastInPlaceMode = false;
			IsDoubleCulvertMode = false;
			L_Std = 1.0;
			Khe_Ho = 0.01;
			if (PrecastBarrelComponents.Count == 0)
			{
				InitPrecastBarrelComponents(forceRefresh: true);
			}
			foreach (CulvertComponentItem precastBarrelComponent in PrecastBarrelComponents)
			{
				BarrelComponents.Add(precastBarrelComponent);
			}
		}
		StartupLogger.Log("[VM] ApplyAssemblyTemplate calling SyncAllAssemblyComponents...");
		SyncAllAssemblyComponents();
		StartupLogger.Log("[VM] ApplyAssemblyTemplate calling UpdatePreviewGeometry...");
		UpdatePreviewGeometry();
		StartupLogger.Log("[VM] ApplyAssemblyTemplate END");
	}

	private void SyncAllAssemblyComponents()
	{
		StartupLogger.Log("[VM] SyncAllAssemblyComponents START");
		foreach (CulvertComponentItem assemblyComponent in AssemblyComponents)
		{
			assemblyComponent.PropertyChanged -= OnAssemblyComponentPropertyChanged;
		}
		AssemblyComponents.Clear();
		if (IsTechnicalCulvertMode)
		{
			foreach (CulvertComponentItem technicalBarrelComponent in TechnicalBarrelComponents)
			{
				AssemblyComponents.Add(technicalBarrelComponent);
			}
			foreach (CulvertComponentItem technicalManholeComponent in TechnicalManholeComponents)
			{
				AssemblyComponents.Add(technicalManholeComponent);
			}
		}
		else
		{
			foreach (CulvertComponentItem barrelComponent in BarrelComponents)
			{
				AssemblyComponents.Add(barrelComponent);
			}
			foreach (CulvertComponentItem outletComponent in OutletComponents)
			{
				AssemblyComponents.Add(outletComponent);
			}
			foreach (CulvertComponentItem apronComponent in ApronComponents)
			{
				AssemblyComponents.Add(apronComponent);
			}
			foreach (CulvertComponentItem manholeComponent in ManholeComponents)
			{
				AssemblyComponents.Add(manholeComponent);
			}
		}
		foreach (CulvertComponentItem assemblyComponent2 in AssemblyComponents)
		{
			assemblyComponent2.PropertyChanged += OnAssemblyComponentPropertyChanged;
		}
		RefreshActiveAssignedFamiliesForTab02();
		SyncMaterialsFromAssemblyComponents();
	}

	private void OnAssemblyComponentPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_isInitializing) return;
		if (!(e.PropertyName == "CategoryType") && !(e.PropertyName == "SelectedSymbol") && !(e.PropertyName == "IsActive"))
		{
			return;
		}
		RefreshActiveAssignedFamiliesForTab02();
		SyncMaterialsFromAssemblyComponents();
		if (sender is CulvertComponentItem culvertComponentItem)
		{
			if (SelectedComponentForTab02 == culvertComponentItem)
			{
				EnsureComponentParametersScanned(culvertComponentItem);
				FilterParameterMappings();
			}
			else
			{
				EnsureComponentParametersScanned(culvertComponentItem);
			}
		}
	}

	public void FilterTab02Group(string? filter)
	{
		if (filter == "CastInPlace")
		{
			SelectedTab02GroupFilter = (SelectedCastInPlaceProfileForTab02 ?? CastInPlaceProfiles.FirstOrDefault())?.TypeName ?? "CastInPlace";
		}
		else
		{
			SelectedTab02GroupFilter = filter ?? "All";
		}
		OnPropertyChanged("IsCastInPlaceTab02Active");
		ApplyTab02FamilyFilter();
	}

	public void ApplyTab02FamilyFilter()
	{
		if (_isFilteringTab02) return;
		_isFilteringTab02 = true;
		try
		{
		CulvertComponentItem? selectedComponentForTab = SelectedComponentForTab02;
		FilteredAssignedFamiliesForTab02.Clear();
		string text = SelectedTab02GroupFilter ?? "All";
		foreach (CulvertComponentItem comp in ActiveAssignedFamiliesForTab02)
		{
			bool flag = comp.GroupType == "Cống đúc sẵn" || comp.CategoryType.Contains("đúc sẵn") || PrecastBarrelComponents.Contains(comp) || TechnicalBarrelComponents.Contains(comp);
			bool flag2 = comp.GroupType == "Cống đổ tại chỗ" || comp.CategoryType.Contains("đổ tại chỗ") || CastInPlaceBarrelComponents.Contains(comp) || (!string.IsNullOrEmpty(comp.ProfileName) && CastInPlaceProfiles.Any((CulvertTypeProfile p) => p.TypeName == comp.ProfileName));
			int num;
			if (!(comp.GroupType == "Cửa xả"))
			{
				string groupType = comp.GroupType;
				if (groupType == null || !groupType.Contains("Cửa"))
				{
					num = (OutletComponents.Contains(comp) ? 1 : 0);
					goto IL_015f;
				}
			}
			num = 1;
			goto IL_015f;
			IL_015f:
			bool flag3 = (byte)num != 0;
			int num2;
			if (!(comp.GroupType == "Sân gia cố"))
			{
				string groupType2 = comp.GroupType;
				if (groupType2 == null || !groupType2.Contains("Gia cố"))
				{
					string groupType3 = comp.GroupType;
					if (groupType3 == null || !groupType3.Contains("Sân"))
					{
						num2 = (ApronComponents.Contains(comp) ? 1 : 0);
						goto IL_0306;
					}
				}
			}
			num2 = 1;
			goto IL_0306;
			IL_01df:
			int num3;
			bool flag5;
			bool flag6;
			bool flag4 = (byte)((int)((uint)num3 | (flag5 ? 1u : 0u)) | (flag6 ? 1 : 0)) != 0;
			bool flag7 = false;
			bool flag8;
			if (text == "All")
			{
				flag7 = true;
			}
			else if ((text == "Precast") & flag)
			{
				flag7 = true;
			}
			else if ((text == "CastInPlace") & flag2)
			{
				flag7 = true;
			}
			else if (text == "Barrel" && (flag | flag2))
			{
				flag7 = true;
			}
			else if ((text == "Outlet") & flag3)
			{
				flag7 = true;
			}
			else if ((text == "Apron") & flag8)
			{
				flag7 = true;
			}
			else if ((text == "TechManhole") & flag5)
			{
				flag7 = true;
			}
			else if ((text == "JunctionBox") & flag6)
			{
				flag7 = true;
			}
			else if ((text == "Manhole") & flag4)
			{
				flag7 = true;
			}
			else if (!string.IsNullOrEmpty(comp.ProfileName) && string.Equals(comp.ProfileName, text, StringComparison.OrdinalIgnoreCase))
			{
				flag7 = true;
			}
			if (flag7)
			{
				FilteredAssignedFamiliesForTab02.Add(comp);
			}
			continue;
			IL_0306:
			flag8 = (byte)num2 != 0;
			flag5 = comp.GroupType == "Hố kỹ thuật" || comp.CategoryType.Contains("Hố kỹ thuật");
			flag6 = comp.GroupType == "Hộp nối cống" || comp.GroupType == "Hộp nối" || comp.CategoryType.Contains("Hộp nối");
			if (!(comp.GroupType == "Hố ga (Hộp nối)"))
			{
				string groupType4 = comp.GroupType;
				if (groupType4 == null || !groupType4.Contains("Hố"))
				{
					string groupType5 = comp.GroupType;
					if (groupType5 == null || !groupType5.Contains("Hộp"))
					{
						num3 = (ManholeComponents.Contains(comp) ? 1 : 0);
						goto IL_01df;
					}
				}
			}
			num3 = 1;
			goto IL_01df;
		}
		if (selectedComponentForTab != null && FilteredAssignedFamiliesForTab02.Contains(selectedComponentForTab))
		{
			_selectedComponentForTab02 = selectedComponentForTab;
		}
		else
		{
			_selectedComponentForTab02 = FilteredAssignedFamiliesForTab02.FirstOrDefault();
		}
		OnPropertyChanged("SelectedComponentForTab02");
		if (!_isInitializing)
		{
			OnSelectedFamilyForTab02Changed();
		}
		}
		finally
		{
			_isFilteringTab02 = false;
		}
	}

	public void RefreshActiveAssignedFamiliesForTab02()
	{
		ActiveAssignedFamiliesForTab02.Clear();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<CulvertComponentItem> list = new List<CulvertComponentItem>();
		if (IsTechnicalCulvertMode)
		{
			list.AddRange(TechnicalBarrelComponents);
			list.AddRange(TechnicalManholeComponents);
		}
		else
		{
			list.AddRange(PrecastBarrelComponents);
			foreach (CulvertTypeProfile castInPlaceProfile in CastInPlaceProfiles)
			{
				list.AddRange(castInPlaceProfile.Components);
			}
			list.AddRange(OutletComponents);
			list.AddRange(ApronComponents);
			list.AddRange(ManholeComponents);
		}
		foreach (CulvertComponentItem item2 in list)
		{
			if (item2.IsActive && item2.SelectedSymbol != null)
			{
				FamilySymbol symbol = item2.SelectedSymbol.Symbol;
				string value = ((symbol != null) ? ((ElementType)symbol).FamilyName : null) ?? item2.CategoryType;
				string value2 = item2.ProfileName ?? "";
				string item = $"{item2.GroupType}_{value2}_{value}_{item2.CategoryType}";
				if (!hashSet.Contains(item))
				{
					hashSet.Add(item);
					ActiveAssignedFamiliesForTab02.Add(item2);
				}
			}
		}
		if (!_isInitializing)
		{
			ApplyTab02FamilyFilter();
		}
	}

	public void SetPreviewMode(string? mode)
	{
		if (string.Equals(mode, "Plan", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "Plan2D", StringComparison.OrdinalIgnoreCase))
		{
			PreviewMode = PreviewViewMode.Plan2D;
		}
		else if (string.Equals(mode, "3D", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "Isometric3D", StringComparison.OrdinalIgnoreCase))
		{
			PreviewMode = PreviewViewMode.Isometric3D;
		}
		else
		{
			PreviewMode = PreviewViewMode.Profile2D;
		}
	}

	private CulvertRowData CreateDefaultPreviewCulvertRow()
	{
		if (IsTechnicalCulvertMode)
		{
			return new CulvertRowData
			{
				STT = 1,
				LyTrinh = "Km0+000",
				LoaiCong = "CỐNG KỸ THUẬT",
				KhauDo = "1.5x1.5",
				ChieuDai = 12.0,
				SoCua = 1,
				DoDoc = 2.0,
				Z1 = 10.0,
				Z2 = 9.76,
				X1 = 0.0,
				Y1 = 0.0,
				X2 = 12.0,
				Y2 = 0.0,
				SoHopNoi = 1,
				KC_HN1 = 6.0,
				B_HT1 = 1.4
			};
		}
		return new CulvertRowData
		{
			STT = 1,
			LyTrinh = "Km0+000",
			LoaiCong = "Cống hộp 1.5x1.5m",
			KhauDo = "1.5x1.5",
			ChieuDai = 12.0,
			SoCua = 1,
			DoDoc = 2.0,
			Z1 = 10.0,
			Z2 = 9.76,
			X1 = 0.0,
			Y1 = 0.0,
			X2 = 12.0,
			Y2 = 0.0
		};
	}

	public void Open3DReview()
	{
		if (SelectedCulvertRowForPreview == null)
		{
			SelectedCulvertRowForPreview = FilteredPreviewCulvertRows.FirstOrDefault() ?? FilteredCulvertRows.FirstOrDefault() ?? AllCulvertRows.FirstOrDefault();
			if (SelectedCulvertRowForPreview == null)
			{
				SelectedCulvertRowForPreview = CreateDefaultPreviewCulvertRow();
			}
		}
		PreviewMode = PreviewViewMode.Isometric3D;
		if (SelectedCulvertRowForPreview != null)
		{
			lock (_cachedRealMeshes)
			{
				_cachedRealMeshes.Remove(SelectedCulvertRowForPreview.STT);
			}
			UpdatePreviewGeometry();
			ExtractRealFamilyGeometryForPreview(SelectedCulvertRowForPreview);
		}
		if (_activeReviewWindow != null && _activeReviewWindow.IsLoaded)
		{
			_activeReviewWindow.Activate();
			return;
		}
		Culvert3DPreviewWindow reviewWindow = new Culvert3DPreviewWindow(this);
		_activeReviewWindow = reviewWindow;
		reviewWindow.Closed += (object? s, EventArgs e) =>
		{
			if (_activeReviewWindow == reviewWindow)
			{
				_activeReviewWindow = null;
			}
		};
		try
		{
			if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
			{
				reviewWindow.Owner = System.Windows.Application.Current.MainWindow;
			}
		}
		catch
		{
		}
		reviewWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		reviewWindow.Show();
	}

	public void FilterPreviewCulvertRows()
	{
		FilteredPreviewCulvertRows.Clear();
		string value = (PreviewSearchText ?? "").Trim().ToLowerInvariant();
		foreach (CulvertRowData filteredCulvertRow in FilteredCulvertRows)
		{
			if (string.IsNullOrEmpty(value) || filteredCulvertRow.STT.ToString().Contains(value) || filteredCulvertRow.LyTrinh.ToLowerInvariant().Contains(value) || filteredCulvertRow.LoaiCong.ToLowerInvariant().Contains(value) || filteredCulvertRow.KhauDo.ToLowerInvariant().Contains(value))
			{
				FilteredPreviewCulvertRows.Add(filteredCulvertRow);
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
		CulvertRowData target = SelectedCulvertRowForPreview ?? FilteredPreviewCulvertRows.FirstOrDefault() ?? FilteredCulvertRows.FirstOrDefault();
		if (target == null)
		{
			CurrentPreviewGeometry = null;
			return;
		}
		int num;
		if (!IsCastInPlaceMode && target.SoCua <= 1)
		{
			string cauKien = target.CauKien;
			if (cauKien == null || cauKien.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) < 0)
			{
				string cauKien2 = target.CauKien;
				if (cauKien2 == null || cauKien2.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) < 0)
				{
					string loaiCong = target.LoaiCong;
					if (loaiCong == null || loaiCong.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) < 0)
					{
						string loaiCong2 = target.LoaiCong;
						if (loaiCong2 == null || loaiCong2.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) < 0)
						{
							string ghiChu = target.GhiChu;
							if (ghiChu == null || ghiChu.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) < 0)
							{
								string ghiChu2 = target.GhiChu;
								num = ((ghiChu2 != null && ghiChu2.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) >= 0) ? 1 : 0);
								goto IL_0131;
							}
						}
					}
				}
			}
		}
		num = 1;
		goto IL_0131;
		IL_0131:
		bool flag = (byte)num != 0;
		ObservableCollection<CulvertComponentItem> source = ((flag && CastInPlaceBarrelComponents.Count > 0) ? CastInPlaceBarrelComponents : BarrelComponents);
		CulvertComponentItem? culvertComponentItem = source.FirstOrDefault((CulvertComponentItem c) => c.CategoryType.Contains("lót") || c.CategoryType.Contains("BTL"));
		bool hasBtlDot = culvertComponentItem?.IsActive ?? true;
		double offsetZ_BTL = culvertComponentItem?.OffsetZ ?? (-0.1);
		double offsetZ_Cat = source.FirstOrDefault((CulvertComponentItem c) => c.CategoryType.Contains("cát") || c.CategoryType.Contains("đá dăm") || c.CategoryType.Contains("đệm"))?.OffsetZ ?? (-0.2);
		CulvertTypeProfile culvertTypeProfile = null;
		if (flag && CastInPlaceProfiles.Count > 0)
		{
			if (!string.IsNullOrEmpty(target.AssignedProfileName))
			{
				culvertTypeProfile = CastInPlaceProfiles.FirstOrDefault((CulvertTypeProfile p) => string.Equals(p.TypeName, target.AssignedProfileName, StringComparison.OrdinalIgnoreCase));
			}
			if (culvertTypeProfile == null)
			{
				culvertTypeProfile = SelectedCastInPlaceProfile ?? CastInPlaceProfiles.FirstOrDefault();
			}
		}
		double lStd;
		if (flag)
		{
			lStd = culvertTypeProfile?.L_Std ?? L_Std_CastInPlace;
		}
		else
		{
			lStd = (IsTechnicalCulvertMode ? L_Std : L_Std_Precast);
		}
		double jointGapM;
		if (flag)
		{
			jointGapM = culvertTypeProfile?.Khe_Ho ?? Khe_Ho_CastInPlace;
		}
		else
		{
			jointGapM = (IsTechnicalCulvertMode ? 0.0 : Khe_Ho);
		}
		CulvertArrayMode arrayMode = ((!flag) ? ((SelectedArrayModeIndex != 0) ? CulvertArrayMode.OneWay : CulvertArrayMode.CenterOut) : (((culvertTypeProfile == null) ? (SelectedArrayModeIndex != 0) : (culvertTypeProfile.SelectedArrayModeIndex != 0)) ? CulvertArrayMode.OneWay : CulvertArrayMode.CenterOut));
		string text = (target.LoaiCong ?? "").ToUpperInvariant();
		bool flag2 = text.Contains("TRON") || text.Contains("TRÒN") || text.Contains("CT") || SelectedCulvertTemplateType.Contains("TRÒN");
		bool isDouble = IsDoubleCulvertMode || ((target.SoCua >= 2) & flag2);
		CurrentPreviewGeometry = CulvertPreviewService.ComputePreview(target, lStd, jointGapM, B_Box, arrayMode, flag, flag2, isDouble, offsetZ_BTL, offsetZ_Cat, hasBtlDot);
		if (CurrentPreviewGeometry == null)
		{
			return;
		}
		lock (_cachedRealMeshes)
		{
			if (_cachedRealMeshes.TryGetValue(target.STT, out List<(MeshGeometry3D, System.Windows.Media.Brush)> value) && value != null && value.Count > 0)
			{
				CurrentPreviewGeometry.RealRevitMeshes = value;
			}
		}
	}

	public void ExtractRealFamilyGeometryForPreview(CulvertRowData? targetRow = null)
	{
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		CulvertRowData row = targetRow ?? SelectedCulvertRowForPreview ?? FilteredPreviewCulvertRows.FirstOrDefault() ?? FilteredCulvertRows.FirstOrDefault();
		if (row == null)
		{
			return;
		}
		_eventHandler.SetAction((UIApplication app) =>
		{
			UIDocument activeUIDocument = app.ActiveUIDocument;
			Document val = ((activeUIDocument != null) ? activeUIDocument.Document : null);
			if (val != null && RevitCulvertMeshExtractor.TryExtractPreviewGeometry(val, row, this, out List<(MeshGeometry3D Mesh, System.Windows.Media.Brush Brush)> realMeshes) && realMeshes.Count > 0)
			{
				lock (_cachedRealMeshes)
				{
					_cachedRealMeshes[row.STT] = realMeshes;
				}
				((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)(() =>
				{
					if ((SelectedCulvertRowForPreview == row || SelectedCulvertRowForPreview?.STT == row.STT) && CurrentPreviewGeometry != null)
					{
						CurrentPreviewGeometry.RealRevitMeshes = realMeshes;
						OnPropertyChanged("CurrentPreviewGeometry");
					}
				}));
			}
		});
		_externalEvent.Raise();
	}

	public void ApplyMaterialGroupFilter()
	{
		FilteredComponentMaterials.Clear();
		string text = _selectedMaterialGroupFilter ?? "All";
		string culvertType = _selectedCulvertTypeForTab03 ?? "TẤT CẢ";
		foreach (CulvertMaterialItem item in ComponentMaterials)
		{
			if (!(culvertType == "TẤT CẢ"))
			{
				bool num;
				if (!(culvertType == "CỐNG ĐỔ TẠI CHỖ"))
				{
					if (!CastInPlaceProfiles.Any((CulvertTypeProfile p) => p.TypeName == culvertType))
					{
						if (!(culvertType == "CỐNG ĐÚC SẴN"))
						{
							if (!(culvertType == "CỬA XẢ"))
							{
								if (!(culvertType == "SÂN GIA CỐ"))
								{
									if (!(culvertType == "HỐ GA (HỘP NỐI)"))
									{
										num = ((!(culvertType == "CỐNG KỸ THUẬT")) ? (item.CulvertType == culvertType) : (item.CulvertType == "CỐNG KỸ THUẬT"));
									}
									else
									{
										if (item.CulvertType == "HỐ GA (HỘP NỐI)" || item.GroupType == "Hố ga (Hộp nối)")
										{
											goto IL_0267;
										}
										num = item.GroupType == "Hộp nối";
									}
								}
								else
								{
									if (item.CulvertType == "SÂN GIA CỐ")
									{
										goto IL_0267;
									}
									num = item.GroupType == "Sân gia cố";
								}
							}
							else
							{
								if (item.CulvertType == "CỬA XẢ")
								{
									goto IL_0267;
								}
								num = item.GroupType == "Cửa xả";
							}
						}
						else
						{
							num = item.CulvertType == "CỐNG ĐÚC SẴN";
						}
					}
					else
					{
						num = item.CulvertType == culvertType;
					}
				}
				else
				{
					if (item.CulvertType == "CỐNG ĐỔ TẠI CHỖ")
					{
						goto IL_0267;
					}
					num = CastInPlaceProfiles.Any((CulvertTypeProfile p) => p.TypeName == item.CulvertType);
				}
				if (!num)
				{
					continue;
				}
			}
			goto IL_0267;
			IL_0267:
			if (text == "All")
			{
				FilteredComponentMaterials.Add(item);
				continue;
			}
			if (text == "Barrel")
			{
				if (!(item.GroupType == "Thân cống"))
				{
					string groupType = item.GroupType;
					if (groupType == null || !groupType.Contains("Thân"))
					{
						goto IL_02e3;
					}
				}
				FilteredComponentMaterials.Add(item);
				continue;
			}
			goto IL_02e3;
			IL_02e3:
			if (text == "Outlet")
			{
				if (!(item.GroupType == "Cửa xả"))
				{
					string groupType2 = item.GroupType;
					if (groupType2 == null || !groupType2.Contains("Cửa"))
					{
						goto IL_033c;
					}
				}
				FilteredComponentMaterials.Add(item);
				continue;
			}
			goto IL_033c;
			IL_033c:
			if (text == "Apron")
			{
				if (!(item.GroupType == "Sân gia cố"))
				{
					string groupType3 = item.GroupType;
					if (groupType3 == null || !groupType3.Contains("Gia cố"))
					{
						string groupType4 = item.GroupType;
						if (groupType4 == null || !groupType4.Contains("Sân"))
						{
							goto IL_03b1;
						}
					}
				}
				FilteredComponentMaterials.Add(item);
				continue;
			}
			goto IL_03b1;
			IL_03b1:
			if (!(text == "Manhole"))
			{
				continue;
			}
			if (!(item.GroupType == "Hố ga (Hộp nối)"))
			{
				string groupType5 = item.GroupType;
				if (groupType5 == null || !groupType5.Contains("Hố"))
				{
					string groupType6 = item.GroupType;
					if (groupType6 == null || !groupType6.Contains("Hộp"))
					{
						continue;
					}
				}
			}
			FilteredComponentMaterials.Add(item);
		}
		if (SelectedMaterialItem == null || !FilteredComponentMaterials.Contains(SelectedMaterialItem))
		{
			SelectedMaterialItem = FilteredComponentMaterials.FirstOrDefault();
		}
	}

	public void SyncMaterialsFromAssemblyComponents()
	{
		if (CastInPlaceBarrelComponents.Count == 0)
		{
			InitCastInPlaceBarrelComponents();
		}
		if (PrecastBarrelComponents.Count == 0)
		{
			InitPrecastBarrelComponents();
		}
		if (TechnicalBarrelComponents.Count == 0)
		{
			InitTechnicalBarrelComponents();
		}
		if (OutletComponents.Count == 0)
		{
			InitOutletComponents();
		}
		if (ApronComponents.Count == 0)
		{
			InitApronComponents();
		}
		if (ManholeComponents.Count == 0)
		{
			InitManholeComponents();
		}
		if (TechnicalManholeComponents.Count == 0)
		{
			InitTechnicalManholeComponents();
		}
		Dictionary<string, CulvertMaterialItem> dictionary = new Dictionary<string, CulvertMaterialItem>(StringComparer.OrdinalIgnoreCase);
		foreach (CulvertMaterialItem m in ComponentMaterials)
		{
			string key = m.CulvertType + "::" + m.CategoryType;
			if (!dictionary.ContainsKey(key)) dictionary[key] = m;
		}
		ComponentMaterials.Clear();
		List<(CulvertComponentItem, string)> list = new List<(CulvertComponentItem, string)>();
		if (CastInPlaceProfiles.Count > 0)
		{
			foreach (CulvertTypeProfile castInPlaceProfile in CastInPlaceProfiles)
			{
				foreach (CulvertComponentItem component in castInPlaceProfile.Components)
				{
					list.Add((component, castInPlaceProfile.TypeName));
				}
			}
		}
		else
		{
			foreach (CulvertComponentItem castInPlaceBarrelComponent in CastInPlaceBarrelComponents)
			{
				list.Add((castInPlaceBarrelComponent, "CỐNG ĐỔ TẠI CHỖ"));
			}
		}
		foreach (CulvertComponentItem precastBarrelComponent in PrecastBarrelComponents)
		{
			list.Add((precastBarrelComponent, "CỐNG ĐÚC SẴN"));
		}
		foreach (CulvertComponentItem outletComponent in OutletComponents)
		{
			list.Add((outletComponent, "CỬA XẢ"));
		}
		foreach (CulvertComponentItem apronComponent in ApronComponents)
		{
			list.Add((apronComponent, "SÂN GIA CỐ"));
		}
		foreach (CulvertComponentItem manholeComponent in ManholeComponents)
		{
			list.Add((manholeComponent, "HỐ GA (HỘP NỐI)"));
		}
		foreach (CulvertComponentItem technicalBarrelComponent in TechnicalBarrelComponents)
		{
			list.Add((technicalBarrelComponent, "CỐNG KỸ THUẬT"));
		}
		foreach (CulvertComponentItem technicalManholeComponent in TechnicalManholeComponents)
		{
			list.Add((technicalManholeComponent, "CỐNG KỸ THUẬT"));
		}
		foreach (var (culvertComponentItem, text) in list)
		{
			if (!string.IsNullOrWhiteSpace(culvertComponentItem.CategoryType))
			{
				string key = text + "::" + culvertComponentItem.CategoryType;
				if (dictionary.TryGetValue(key, out var value))
				{
					value.CulvertType = text;
					value.GroupType = culvertComponentItem.GroupType;
					value.FamilyDisplayName = culvertComponentItem.SelectedSymbol?.DisplayName ?? "(Chưa chọn Family)";
					value.IsActive = culvertComponentItem.IsActive;
					ComponentMaterials.Add(value);
				}
				else
				{
					CulvertMaterialItem culvertMaterialItem = CreateDefaultMaterialItem(culvertComponentItem.CategoryType, culvertComponentItem.SelectedSymbol?.DisplayName ?? "(Chưa chọn Family)", culvertComponentItem.IsActive);
					culvertMaterialItem.CulvertType = text;
					culvertMaterialItem.GroupType = culvertComponentItem.GroupType;
					ComponentMaterials.Add(culvertMaterialItem);
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
		CulvertMaterialItem item = new CulvertMaterialItem
		{
			CategoryType = category,
			FamilyDisplayName = familyName,
			IsActive = isActive,
			Transparency = 0.0,
			MaterialParamName = "Material"
		};
		string text = category.ToUpperInvariant();
		if (text.Contains("CHUẨN") || text.Contains("CHỨA") || text.Contains("ĐỐT"))
		{
			item.MaterialName = "BTCT_M300_Cống đúc sẵn";
			item.SetColorRgb(189, 195, 199);
		}
		else if (text.Contains("BÙ") || text.Contains("CO GIÃN"))
		{
			item.MaterialName = "BTCT_M300_Cống đúc sẵn";
			item.SetColorRgb(189, 195, 199);
		}
		else if (text.Contains("SÂN") || text.Contains("CỬA XẢ") || text.Contains("THƯỢNG LƯU") || text.Contains("HẠ LƯU"))
		{
			item.MaterialName = "BT_M250_Đổ tại chỗ";
			item.SetColorRgb(149, 165, 166);
		}
		else if (text.Contains("HỘP NỐI") || text.Contains("HỐ THU") || text.Contains("HỐ GA"))
		{
			item.MaterialName = "BT_M250_Hố ga";
			item.SetColorRgb(127, 140, 141);
		}
		else if (text.Contains("LÓT") || text.Contains("BTL"))
		{
			item.MaterialName = "BT_M100_Lót móng";
			item.SetColorRgb(74, 85, 104);
		}
		else if (text.Contains("CÁT") || text.Contains("ĐỆM CÁT"))
		{
			item.MaterialName = "Cát đầm chặt K95";
			item.SetColorRgb(236, 201, 75);
		}
		else if (text.Contains("ĐÁ DĂM") || text.Contains("GỐI"))
		{
			item.MaterialName = "Đá dăm 1x2 đệm";
			item.SetColorRgb(86, 101, 115);
		}
		else
		{
			item.MaterialName = "Vật liệu hạ tầng kỹ thuật";
			item.SetColorRgb(160, 174, 192);
		}
		item.PropertyChanged += (object? s, PropertyChangedEventArgs e) =>
		{
			if (e.PropertyName == "MaterialName")
			{
				UpdateMaterialStatusNote(item);
			}
		};
		UpdateMaterialStatusNote(item);
		return item;
	}

	public void ApplyStandardMaterialPreset()
	{
		foreach (CulvertMaterialItem componentMaterial in ComponentMaterials)
		{
			string text = componentMaterial.CategoryType.ToUpperInvariant();
			if (text.Contains("CHUẨN") || text.Contains("ĐỐT"))
			{
				componentMaterial.MaterialName = "BTCT_M300_Cống đúc sẵn";
				componentMaterial.SetColorRgb(189, 195, 199);
			}
			else if (text.Contains("BÙ") || text.Contains("CO GIÃN"))
			{
				componentMaterial.MaterialName = "BTCT_M300_Cống đúc sẵn";
				componentMaterial.SetColorRgb(189, 195, 199);
			}
			else if (text.Contains("SÂN") || text.Contains("THƯỢNG LƯU") || text.Contains("HẠ LƯU"))
			{
				componentMaterial.MaterialName = "BT_M250_Đổ tại chỗ";
				componentMaterial.SetColorRgb(149, 165, 166);
			}
			else if (text.Contains("HỘP NỐI") || text.Contains("HỐ THU") || text.Contains("HỐ GA"))
			{
				componentMaterial.MaterialName = "BT_M250_Hố ga";
				componentMaterial.SetColorRgb(127, 140, 141);
			}
			else if (text.Contains("LÓT") || text.Contains("BTL"))
			{
				componentMaterial.MaterialName = "BT_M100_Lót móng";
				componentMaterial.SetColorRgb(74, 85, 104);
			}
			else if (text.Contains("CÁT") || text.Contains("ĐỆM CÁT"))
			{
				componentMaterial.MaterialName = "Cát đầm chặt K95";
				componentMaterial.SetColorRgb(236, 201, 75);
			}
			else if (text.Contains("ĐÁ DĂM") || text.Contains("GỐI"))
			{
				componentMaterial.MaterialName = "Đá dăm 1x2 đệm";
				componentMaterial.SetColorRgb(86, 101, 115);
			}
			UpdateMaterialStatusNote(componentMaterial);
		}
		System.Windows.MessageBox.Show("Đã áp dụng mẫu vật liệu & màu sắc TCVN chuẩn cho toàn bộ cấu kiện.", "Áp dụng mẫu TCVN", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	public void UpdateSelectedMaterialItems(IList<CulvertMaterialItem>? items)
	{
		SelectedMaterialItems.Clear();
		if (items != null)
		{
			foreach (CulvertMaterialItem item in items)
			{
				SelectedMaterialItems.Add(item);
			}
		}
		if (SelectedMaterialItems.Count > 1)
		{
			SelectedItemsSummaryText = $"[ĐANG QUÉT {SelectedMaterialItems.Count} CẤU KIỆN]";
			ApplyToSelectedGroupButtonText = $"⚡ ÁP DỤNG VẬT LIỆU & MÀU CHO {SelectedMaterialItems.Count} CẤU KIỆN ĐANG QUÉT";
		}
		else if (SelectedMaterialItems.Count == 1)
		{
			SelectedItemsSummaryText = "";
			ApplyToSelectedGroupButtonText = "⚡ ÁP DỤNG VẬT LIỆU & MÀU CHO CẤU KIỆN ĐANG CHỌN";
		}
		else
		{
			SelectedItemsSummaryText = "";
			ApplyToSelectedGroupButtonText = "⚡ ÁP DỤNG VẬT LIỆU & MÀU CHO CÁC DÒNG ĐANG CHỌN";
		}
	}

	public void ApplyMaterialToSelectedGroup()
	{
		if (SelectedMaterialItem == null)
		{
			System.Windows.MessageBox.Show("Vui lòng chọn cấu kiện làm mẫu trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		List<CulvertMaterialItem> list = ((SelectedMaterialItems.Count > 0) ? SelectedMaterialItems.ToList() : new List<CulvertMaterialItem> { SelectedMaterialItem });
		string materialName = SelectedMaterialItem.MaterialName;
		byte colorR = SelectedMaterialItem.ColorR;
		byte colorG = SelectedMaterialItem.ColorG;
		byte colorB = SelectedMaterialItem.ColorB;
		double transparency = SelectedMaterialItem.Transparency;
		string materialParamName = SelectedMaterialItem.MaterialParamName;
		foreach (CulvertMaterialItem item in list)
		{
			item.MaterialName = materialName;
			item.SetColorRgb(colorR, colorG, colorB);
			item.Transparency = transparency;
			if (!string.IsNullOrWhiteSpace(materialParamName))
			{
				item.MaterialParamName = materialParamName;
			}
			UpdateMaterialStatusNote(item);
		}
		System.Windows.MessageBox.Show($"Đã áp dụng đồng bộ tên vật liệu '{materialName}' và màu sắc ({SelectedMaterialItem.ColorHex}) cho {list.Count} cấu kiện đang chọn/quét thành công!", "Áp dụng thành công", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	public void ApplyMaterialToCurrentGroupType()
	{
		if (SelectedMaterialItem == null)
		{
			return;
		}
		string group = SelectedMaterialItem.GroupType;
		string materialName = SelectedMaterialItem.MaterialName;
		byte colorR = SelectedMaterialItem.ColorR;
		byte colorG = SelectedMaterialItem.ColorG;
		byte colorB = SelectedMaterialItem.ColorB;
		double transparency = SelectedMaterialItem.Transparency;
		string materialParamName = SelectedMaterialItem.MaterialParamName;
		List<CulvertMaterialItem> list = FilteredComponentMaterials.Where((CulvertMaterialItem m) => string.Equals(m.GroupType, group, StringComparison.OrdinalIgnoreCase)).ToList();
		foreach (CulvertMaterialItem item in list)
		{
			item.MaterialName = materialName;
			item.SetColorRgb(colorR, colorG, colorB);
			item.Transparency = transparency;
			if (!string.IsNullOrWhiteSpace(materialParamName))
			{
				item.MaterialParamName = materialParamName;
			}
			UpdateMaterialStatusNote(item);
		}
		System.Windows.MessageBox.Show($"Đã áp dụng vật liệu '{materialName}' cho toàn bộ {list.Count} cấu kiện trong cụm '{group}' thành công!", "Áp dụng thành công", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	public void ApplyColorPreset(string? hex)
	{
		if (string.IsNullOrWhiteSpace(hex))
		{
			return;
		}
		List<CulvertMaterialItem> list;
		if (SelectedMaterialItems.Count <= 0)
		{
			list = ((SelectedMaterialItem != null) ? new List<CulvertMaterialItem> { SelectedMaterialItem } : new List<CulvertMaterialItem>());
		}
		else
		{
			list = SelectedMaterialItems.ToList();
		}
		foreach (CulvertMaterialItem item in list)
		{
			item.ColorHex = hex;
			UpdateMaterialStatusNote(item);
		}
	}

	public void ScanRevitMaterials()
	{
		AvailableRevitMaterials.Clear();
		foreach (string allMaterialName in BimMaterialService.GetAllMaterialNames(Doc))
		{
			AvailableRevitMaterials.Add(allMaterialName);
		}
		foreach (CulvertMaterialItem componentMaterial in ComponentMaterials)
		{
			UpdateMaterialStatusNote(componentMaterial);
		}
	}

	private void UpdateMaterialStatusNote(CulvertMaterialItem item)
	{
		if (string.IsNullOrWhiteSpace(item.MaterialName))
		{
			item.StatusNote = "Chưa đặt tên";
			return;
		}
		bool flag = AvailableRevitMaterials.Any((string m) => string.Equals(m, item.MaterialName.Trim(), StringComparison.OrdinalIgnoreCase));
		item.StatusNote = (flag ? "Đã có trong Revit" : "Sẽ tạo mới");
	}

	public void CreateSelectedMaterialInRevit()
	{
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		List<CulvertMaterialItem> source;
		if (SelectedMaterialItems.Count <= 0)
		{
			source = ((SelectedMaterialItem != null) ? new List<CulvertMaterialItem> { SelectedMaterialItem } : new List<CulvertMaterialItem>());
		}
		else
		{
			source = SelectedMaterialItems.ToList();
		}
		List<CulvertMaterialItem> validTargets = source.Where((CulvertMaterialItem t) => !string.IsNullOrWhiteSpace(t.MaterialName)).ToList();
		if (validTargets.Count == 0)
		{
			System.Windows.MessageBox.Show("Vui lòng chọn cấu kiện và nhập tên vật liệu cần tạo.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		_eventHandler.SetAction((UIApplication app) =>
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Expected Obj, but got Unknown
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			Document document = app.ActiveUIDocument.Document;
			Transaction val = new Transaction(document, "Tạo vật liệu cấu kiện");
			try
			{
				val.Start();
				foreach (CulvertMaterialItem item in validTargets)
				{
					BimMaterialService.GetOrCreateMaterial(document, item.MaterialName, item.ColorR, item.ColorG, item.ColorB, item.Transparency);
				}
				val.Commit();
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)(() =>
			{
				ScanRevitMaterials();
				System.Windows.MessageBox.Show($"Đã tạo/cập nhật thành công {validTargets.Count} vật liệu trong dự án Revit.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}));
		});
		_externalEvent.Raise();
	}

	private void AddNewMaterialItem()
	{
		CulvertMaterialItem culvertMaterialItem = new CulvertMaterialItem();
		culvertMaterialItem.IsActive = true;
		CulvertMaterialItem culvertMaterialItem2 = culvertMaterialItem;
		culvertMaterialItem2.GroupType = _selectedMaterialGroupFilter switch
		{
			"Barrel" => "Thân cống", 
			"Outlet" => "Cửa xả", 
			"Apron" => "Sân gia cố", 
			"Manhole" => "Hố ga (Hộp nối)", 
			_ => "Thân cống", 
		};
		culvertMaterialItem.CategoryType = "Cấu kiện tùy chọn";
		culvertMaterialItem.FamilyDisplayName = "(Tùy biến)";
		culvertMaterialItem.MaterialName = "BTCT_M300_Mới";
		culvertMaterialItem.Transparency = 0.0;
		culvertMaterialItem.MaterialParamName = "Material";
		CulvertMaterialItem newItem = culvertMaterialItem;
		newItem.SetColorRgb(189, 195, 199);
		newItem.PropertyChanged += (object? s, PropertyChangedEventArgs e) =>
		{
			if (e.PropertyName == "MaterialName")
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
			CulvertMaterialItem selectedMaterialItem = SelectedMaterialItem;
			ComponentMaterials.Remove(selectedMaterialItem);
			ApplyMaterialGroupFilter();
			SelectedMaterialItem = FilteredComponentMaterials.FirstOrDefault();
		}
	}

	public void OpenColorPicker()
	{
		if (SelectedMaterialItem == null)
		{
			System.Windows.MessageBox.Show("Vui lòng chọn một cấu kiện trong bảng trước khi chọn màu.", "Chưa chọn cấu kiện", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		ColorDialog colorDialog = new ColorDialog
		{
			AllowFullOpen = true,
			AnyColor = true,
			FullOpen = true,
			Color = System.Drawing.Color.FromArgb(SelectedMaterialItem.ColorR, SelectedMaterialItem.ColorG, SelectedMaterialItem.ColorB)
		};
		try
		{
			if (colorDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}
			foreach (CulvertMaterialItem item in (SelectedMaterialItems.Count > 0) ? SelectedMaterialItems.ToList() : new List<CulvertMaterialItem> { SelectedMaterialItem })
			{
				item.SetColorRgb(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
			}
		}
		finally
		{
			((IDisposable)colorDialog)?.Dispose();
		}
	}

	public void OpenColorPickerForItem(CulvertMaterialItem? item)
	{
		if (item != null)
		{
			SelectedMaterialItem = item;
			OpenColorPicker();
		}
	}

	private void EnsureComponentParametersScanned(CulvertComponentItem comp)
	{
		FamilySymbol val = comp?.SelectedSymbol?.GetFreshSymbol(Doc);
		if (val == null || Doc == null)
		{
			return;
		}
		string cat = comp.CategoryType;
		string famName = ((ElementType)val).FamilyName;
		string prof = comp.ProfileName ?? "";
		if (ParameterMappings.Any((ParameterMappingItem m) => string.Equals(m.CategoryName, cat, StringComparison.OrdinalIgnoreCase) && string.Equals(m.ProfileName, prof, StringComparison.OrdinalIgnoreCase) && string.Equals(m.FamilyName, famName, StringComparison.OrdinalIgnoreCase)))
		{
			return;
		}
		var existingList = ParameterMappings.Where((ParameterMappingItem m) => string.Equals(m.CategoryName, cat, StringComparison.OrdinalIgnoreCase) && string.Equals(m.ProfileName, prof, StringComparison.OrdinalIgnoreCase)).ToList();
		foreach (ParameterMappingItem item in existingList)
		{
			item.PropertyChanged -= OnParameterMappingItemPropertyChanged;
			ParameterMappings.Remove(item);
		}
		foreach (ParameterMappingItem item2 in FamilyParameterScannerService.ScanParametersForFamily(Doc, val, cat, prof))
		{
			var prev = existingList.FirstOrDefault(p => string.Equals(p.InternalName, item2.InternalName, StringComparison.OrdinalIgnoreCase));
			if (prev != null)
			{
				if (!string.IsNullOrEmpty(prev.CustomValue)) item2.CustomValue = prev.CustomValue;
				if (!string.IsNullOrEmpty(prev.MappedField)) item2.MappedField = prev.MappedField;
				item2.IsSelected = prev.IsSelected;
			}
			else
			{
				item2.IsSelected = true;
			}
			item2.PropertyChanged += OnParameterMappingItemPropertyChanged;
			ParameterMappings.Add(item2);
		}
	}

	private void OnParameterMappingItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "CustomValue" || e.PropertyName == "IsSelected" || e.PropertyName == "MappedField")
		{
			lock (_cachedRealMeshes)
			{
				_cachedRealMeshes.Clear();
			}
			UpdatePreviewGeometry();
			if (SelectedCulvertRowForPreview != null && _activeReviewWindow != null && _activeReviewWindow.IsLoaded)
			{
				ExtractRealFamilyGeometryForPreview(SelectedCulvertRowForPreview);
			}
		}
	}

	private void OnSelectedFamilyForTab02Changed()
	{
		if (SelectedComponentForTab02?.SelectedSymbol?.GetFreshSymbol(Doc) == null)
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
		FamilySymbol val = SelectedComponentForTab02?.SelectedSymbol?.GetFreshSymbol(Doc);
		if (val == null)
		{
			return;
		}
		string categoryType = SelectedComponentForTab02.CategoryType;
		string familyName = ((ElementType)val).FamilyName;
		string b = SelectedComponentForTab02.ProfileName ?? "";
		foreach (ParameterMappingItem parameterMapping in ParameterMappings)
		{
			bool flag = string.Equals(parameterMapping.CategoryName, categoryType, StringComparison.OrdinalIgnoreCase) && string.Equals(parameterMapping.ProfileName, b, StringComparison.OrdinalIgnoreCase) && string.Equals(parameterMapping.FamilyName, familyName, StringComparison.OrdinalIgnoreCase);
			if (!flag && string.IsNullOrEmpty(parameterMapping.CategoryName) && string.Equals(parameterMapping.ProfileName, b, StringComparison.OrdinalIgnoreCase) && string.Equals(parameterMapping.FamilyName, familyName, StringComparison.OrdinalIgnoreCase))
			{
				flag = true;
			}
			if (!flag)
			{
				continue;
			}
			if (string.Equals(Tab02ParamCategoryFilter, "Dimensions", StringComparison.OrdinalIgnoreCase))
			{
				if (!parameterMapping.IsDimension)
				{
					continue;
				}
			}
			else if (string.Equals(Tab02ParamCategoryFilter, "Other", StringComparison.OrdinalIgnoreCase))
			{
				if (!parameterMapping.IsOther || (!parameterMapping.DataType.Contains("Length") && !parameterMapping.DataType.Contains("Yes/No")))
				{
					continue;
				}
			}
			else if ((!parameterMapping.IsDimension && !parameterMapping.IsOther) || (parameterMapping.IsOther && !parameterMapping.DataType.Contains("Length") && !parameterMapping.DataType.Contains("Yes/No")))
			{
				continue;
			}
			if ((ShowTypeParams && !ShowInstanceParams && parameterMapping.IsInstance) || (!ShowTypeParams && ShowInstanceParams && !parameterMapping.IsInstance))
			{
				continue;
			}
			if (!string.IsNullOrWhiteSpace(ParamSearchFilter))
			{
				string value = ParamSearchFilter.Trim().ToLowerInvariant();
				if (!parameterMapping.InternalName.ToLowerInvariant().Contains(value) && !parameterMapping.GroupName.ToLowerInvariant().Contains(value) && !parameterMapping.MappedField.ToLowerInvariant().Contains(value))
				{
					continue;
				}
			}
			FilteredParameterMappings.Add(parameterMapping);
		}
		SelectedParamForBatch = FilteredParameterMappings.FirstOrDefault();
		UpdateBatchTableForSelectedParam();
		RefreshInspectorParameterMappings();
	}

	public void UpdateBatchTableForSelectedParam()
	{
		string text = SelectedParamForBatch?.InternalName ?? string.Empty;
		string defaultValue = SelectedParamForBatch?.DefaultValue ?? string.Empty;
		string categoryName = SelectedComponentForTab02?.CategoryType ?? string.Empty;
		string profileName = SelectedComponentForTab02?.ProfileName ?? string.Empty;
		foreach (CulvertRowData allCulvertRow in AllCulvertRows)
		{
			allCulvertRow.ActiveParamName = text;
			allCulvertRow.ActiveParamValue = allCulvertRow.GetParamOverride(profileName, categoryName, text, defaultValue);
		}
		try
		{
			ICollectionView defaultView = CollectionViewSource.GetDefaultView(FilteredCulvertRows);
			if (defaultView != null)
			{
				defaultView.Refresh();
			}
		}
		catch
		{
		}
	}

	public void RefreshInspectorParameterMappings()
	{
		if (_isUpdatingInspector)
		{
			return;
		}
		_isUpdatingInspector = true;
		try
		{
			foreach (ParameterMappingItem inspectorParameterMapping in InspectorParameterMappings)
			{
				inspectorParameterMapping.PropertyChanged -= OnInspectorItemPropertyChanged;
			}
			InspectorParameterMappings.Clear();
			CulvertRowData culvertRowData = SelectedCulvertRowForInspector ?? InspectorCulvertRows.FirstOrDefault() ?? FilteredCulvertRows.FirstOrDefault() ?? AllCulvertRows.FirstOrDefault();
			if (_selectedCulvertRowForInspector == null && culvertRowData != null)
			{
				_selectedCulvertRowForInspector = culvertRowData;
				OnPropertyChanged("SelectedCulvertRowForInspector");
			}
			string categoryName = SelectedComponentForTab02?.CategoryType ?? string.Empty;
			string profileName = SelectedComponentForTab02?.ProfileName ?? string.Empty;
			foreach (ParameterMappingItem filteredParameterMapping in FilteredParameterMappings)
			{
				string text;
				if (culvertRowData != null && culvertRowData.HasParamOverride(profileName, categoryName, filteredParameterMapping.InternalName))
				{
					text = culvertRowData.GetParamOverride(profileName, categoryName, filteredParameterMapping.InternalName, "");
				}
				else
				{
					text = filteredParameterMapping.DefaultValue;
				}
				if (string.IsNullOrEmpty(text))
				{
					text = filteredParameterMapping.DefaultValue;
				}
				if (filteredParameterMapping.IsYesNoParameter)
				{
					switch ((text ?? "").Trim().ToLowerInvariant())
					{
					case "1":
					case "true":
					case "có":
					case "co":
					case "yes":
					case "bật":
						text = "Có";
						break;
					default:
						text = "Không";
						break;
					}
				}
				ParameterMappingItem parameterMappingItem = new ParameterMappingItem
				{
					STT = filteredParameterMapping.STT,
					IsSelected = filteredParameterMapping.IsSelected,
					ProfileName = filteredParameterMapping.ProfileName,
					CategoryName = filteredParameterMapping.CategoryName,
					FamilyName = filteredParameterMapping.FamilyName,
					InternalName = filteredParameterMapping.InternalName,
					DisplayName = filteredParameterMapping.DisplayName,
					GroupName = filteredParameterMapping.GroupName,
					IsDimension = filteredParameterMapping.IsDimension,
					IsOther = filteredParameterMapping.IsOther,
					IsVisibility = filteredParameterMapping.IsVisibility,
					DataType = filteredParameterMapping.DataType,
					DefaultValue = filteredParameterMapping.DefaultValue,
					CustomValue = text,
					MappedField = filteredParameterMapping.MappedField,
					IsInstance = filteredParameterMapping.IsInstance
				};
				parameterMappingItem.PropertyChanged += OnInspectorItemPropertyChanged;
				InspectorParameterMappings.Add(parameterMappingItem);
			}
		}
		finally
		{
			_isUpdatingInspector = false;
			OnPropertyChanged("InspectorParameterMappings");
		}
	}

	private void OnInspectorItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (!_isUpdatingInspector && e.PropertyName == "CustomValue" && sender is ParameterMappingItem parameterMappingItem && SelectedCulvertRowForInspector != null)
		{
			string categoryName = SelectedComponentForTab02?.CategoryType ?? parameterMappingItem.CategoryName;
			string profileName = SelectedComponentForTab02?.ProfileName ?? parameterMappingItem.ProfileName;
			SelectedCulvertRowForInspector.SetParamOverride(profileName, categoryName, parameterMappingItem.InternalName, parameterMappingItem.CustomValue ?? string.Empty);
			UpdatePreviewGeometry();
		}
	}

	private void ScanCurrentSelectedFamilyParameters()
	{
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		FamilySymbol sym = SelectedComponentForTab02?.SelectedSymbol?.GetFreshSymbol(Doc);
		if (sym == null)
		{
			System.Windows.MessageBox.Show("Vui lòng chọn một Family trong danh sách để quét tham số.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		string cat = SelectedComponentForTab02.CategoryType;
		string prof = SelectedComponentForTab02?.ProfileName ?? string.Empty;
		_eventHandler.SetAction((UIApplication app) =>
		{
			Document document = app.ActiveUIDocument.Document;
			List<ParameterMappingItem> scanned = FamilyParameterScannerService.ScanParametersForFamily(document, sym, cat, prof);
			((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)(() =>
			{
				var existingList = ParameterMappings.Where((ParameterMappingItem m) => string.Equals(m.CategoryName, cat, StringComparison.OrdinalIgnoreCase) && string.Equals(m.ProfileName, prof, StringComparison.OrdinalIgnoreCase)).ToList();
				foreach (ParameterMappingItem item in existingList)
				{
					item.PropertyChanged -= OnParameterMappingItemPropertyChanged;
					ParameterMappings.Remove(item);
				}
				foreach (ParameterMappingItem item2 in scanned)
				{
					var prev = existingList.FirstOrDefault(p => string.Equals(p.InternalName, item2.InternalName, StringComparison.OrdinalIgnoreCase));
					if (prev != null)
					{
						if (!string.IsNullOrEmpty(prev.CustomValue)) item2.CustomValue = prev.CustomValue;
						if (!string.IsNullOrEmpty(prev.MappedField)) item2.MappedField = prev.MappedField;
						item2.IsSelected = prev.IsSelected;
					}
					else
					{
						item2.IsSelected = true;
					}
					item2.PropertyChanged += OnParameterMappingItemPropertyChanged;
					ParameterMappings.Add(item2);
				}
				FilterParameterMappings();
				int value = scanned.Count((ParameterMappingItem p) => !p.IsInstance);
				int value2 = scanned.Count((ParameterMappingItem p) => p.IsInstance);
				int value3 = scanned.Count((ParameterMappingItem p) => p.IsDimension);
				int value4 = scanned.Count((ParameterMappingItem p) => p.IsVisibility);
				int value5 = scanned.Count((ParameterMappingItem p) => p.IsOther);
				System.Windows.MessageBox.Show($"Đã quét thành công Family '{((ElementType)sym).FamilyName}' ({cat}):\n- Tổng số: {scanned.Count} tham số\n- Tham số Loại (Type): {value} tham số\n- Tham số Biến thể (Instance): {value2} tham số\n- Nhóm Kích thước (Dimensions): {value3} tham số\n- Nhóm Ẩn hiện (Visibility): {value4} tham số\n- Nhóm Khác (Other): {value5} tham số.", "Quét hoàn tất", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}));
		});
		_externalEvent.Raise();
	}

	private void ApplyBatchValue()
	{
		if (SelectedParamForBatch == null || string.IsNullOrEmpty(BatchApplyValue))
		{
			System.Windows.MessageBox.Show("Vui lòng chọn tham số và nhập giá trị cần gán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		string text = BatchApplyValue;
		if (SelectedParamForBatch.IsYesNoParameter)
		{
			switch ((text ?? "").Trim().ToLowerInvariant())
			{
			case "0":
			case "no":
			case "false":
			case "không":
			case "khong":
			case "tắt":
				text = "Không";
				break;
			default:
				text = "Có";
				break;
			}
		}
		SelectedParamForBatch.CustomValue = text;
		string internalName = SelectedParamForBatch.InternalName;
		CulvertComponentItem? selectedComponentForTab = SelectedComponentForTab02;
		string categoryName = selectedComponentForTab?.CategoryType ?? string.Empty;
		string text2 = selectedComponentForTab?.ProfileName ?? string.Empty;

		bool isTechnicalComp = selectedComponentForTab != null && (
			TechnicalBarrelComponents.Contains(selectedComponentForTab) ||
			TechnicalManholeComponents.Contains(selectedComponentForTab) ||
			string.Equals(selectedComponentForTab.GroupType, "Cống kỹ thuật", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(selectedComponentForTab.GroupType, "Hố kỹ thuật", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(selectedComponentForTab.GroupType, "Hộp nối cống", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(selectedComponentForTab.ProfileName, "Cống kỹ thuật", StringComparison.OrdinalIgnoreCase) ||
			IsTechnicalCulvertMode
		);

		bool isPrecastComp = !isTechnicalComp && selectedComponentForTab != null && (
			PrecastBarrelComponents.Contains(selectedComponentForTab) ||
			string.Equals(selectedComponentForTab.GroupType, "Cống đúc sẵn", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(selectedComponentForTab.ProfileName, "Cống đúc sẵn", StringComparison.OrdinalIgnoreCase) ||
			(selectedComponentForTab.CategoryType != null && selectedComponentForTab.CategoryType.Contains("đúc sẵn"))
		);

		bool isCastInPlaceComp = !isTechnicalComp && !isPrecastComp && selectedComponentForTab != null && (
			CastInPlaceBarrelComponents.Contains(selectedComponentForTab) ||
			string.Equals(selectedComponentForTab.GroupType, "Cống đổ tại chỗ", StringComparison.OrdinalIgnoreCase) ||
			(selectedComponentForTab.GroupType != null && selectedComponentForTab.GroupType.Contains("đổ tại chỗ")) ||
			CastInPlaceProfiles.Any((CulvertTypeProfile p) => p.Components.Contains(selectedComponentForTab) || (!string.IsNullOrEmpty(selectedComponentForTab.ProfileName) && string.Equals(p.TypeName, selectedComponentForTab.ProfileName, StringComparison.OrdinalIgnoreCase)))
		);

		int num = 0;
		foreach (CulvertRowData allCulvertRow in AllCulvertRows)
		{
			if (allCulvertRow.STT < BatchFromSTT || allCulvertRow.STT > BatchToSTT)
			{
				continue;
			}
			if (isTechnicalComp && !allCulvertRow.IsTechnical)
			{
				continue;
			}
			if (!isTechnicalComp && allCulvertRow.IsTechnical)
			{
				continue;
			}
			if (isPrecastComp && (allCulvertRow.IsCastInPlace || allCulvertRow.IsTechnical))
			{
				continue;
			}
			if (isCastInPlaceComp)
			{
				if (!allCulvertRow.IsCastInPlace || allCulvertRow.IsTechnical)
				{
					continue;
				}
				if (!string.IsNullOrEmpty(text2) && 
				    !string.IsNullOrEmpty(allCulvertRow.AssignedProfileName) && 
				    !string.Equals(text2, "Cống đổ tại chỗ", StringComparison.OrdinalIgnoreCase) && 
				    !string.Equals(text2, allCulvertRow.AssignedProfileName, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
			}

			allCulvertRow.SetParamOverride(text2, categoryName, internalName, text);
			num++;
		}
		UpdateBatchTableForSelectedParam();
		lock (_cachedRealMeshes)
		{
			_cachedRealMeshes.Clear();
		}
		if (SelectedCulvertRowForPreview != null)
		{
			ExtractRealFamilyGeometryForPreview(SelectedCulvertRowForPreview);
		}
		UpdatePreviewGeometry();
		UpdateInspectorCulvertRows();
		RefreshInspectorParameterMappings();
		System.Windows.MessageBox.Show($"Đã gán giá trị '{text}' cho tham số '{internalName}' của Family '{SelectedParamForBatch.FamilyName}' (Phạm vi cống STT {BatchFromSTT} - {BatchToSTT}, {num} cống).", "Hoàn thành", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	public void ApplyAllCustomParamsToStationRange()
	{
		List<(string, string, string)> list = new List<(string, string, string)>();
		foreach (ParameterMappingItem filteredParameterMapping in FilteredParameterMappings)
		{
			if (!filteredParameterMapping.IsSelected)
			{
				continue;
			}
			if (filteredParameterMapping.IsYesNoParameter)
			{
				switch ((filteredParameterMapping.CustomValue ?? "").Trim().ToLowerInvariant())
				{
				case "0":
				case "no":
				case "false":
				case "không":
				case "khong":
				case "tắt":
					list.Add((filteredParameterMapping.CategoryName, filteredParameterMapping.InternalName, "Không"));
					break;
				default:
					list.Add((filteredParameterMapping.CategoryName, filteredParameterMapping.InternalName, "Có"));
					break;
				}
			}
			else if (!string.IsNullOrWhiteSpace(filteredParameterMapping.CustomValue))
			{
				list.Add((filteredParameterMapping.CategoryName, filteredParameterMapping.InternalName, filteredParameterMapping.CustomValue));
			}
		}
		if (list.Count == 0)
		{
			System.Windows.MessageBox.Show("Không có tham số nào có giá trị tùy chỉnh để gán cho các lý trình.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		CulvertComponentItem? selectedComponentForTab = SelectedComponentForTab02;
		string text = selectedComponentForTab?.ProfileName ?? string.Empty;

		bool isTechnicalComp = selectedComponentForTab != null && (
			TechnicalBarrelComponents.Contains(selectedComponentForTab) ||
			TechnicalManholeComponents.Contains(selectedComponentForTab) ||
			string.Equals(selectedComponentForTab.GroupType, "Cống kỹ thuật", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(selectedComponentForTab.GroupType, "Hố kỹ thuật", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(selectedComponentForTab.GroupType, "Hộp nối cống", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(selectedComponentForTab.ProfileName, "Cống kỹ thuật", StringComparison.OrdinalIgnoreCase) ||
			IsTechnicalCulvertMode
		);

		bool isPrecastComp = !isTechnicalComp && selectedComponentForTab != null && (
			PrecastBarrelComponents.Contains(selectedComponentForTab) ||
			string.Equals(selectedComponentForTab.GroupType, "Cống đúc sẵn", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(selectedComponentForTab.ProfileName, "Cống đúc sẵn", StringComparison.OrdinalIgnoreCase) ||
			(selectedComponentForTab.CategoryType != null && selectedComponentForTab.CategoryType.Contains("đúc sẵn"))
		);

		bool isCastInPlaceComp = !isTechnicalComp && !isPrecastComp && selectedComponentForTab != null && (
			CastInPlaceBarrelComponents.Contains(selectedComponentForTab) ||
			string.Equals(selectedComponentForTab.GroupType, "Cống đổ tại chỗ", StringComparison.OrdinalIgnoreCase) ||
			(selectedComponentForTab.GroupType != null && selectedComponentForTab.GroupType.Contains("đổ tại chỗ")) ||
			CastInPlaceProfiles.Any((CulvertTypeProfile p) => p.Components.Contains(selectedComponentForTab) || (!string.IsNullOrEmpty(selectedComponentForTab.ProfileName) && string.Equals(p.TypeName, selectedComponentForTab.ProfileName, StringComparison.OrdinalIgnoreCase)))
		);

		int num = 0;
		foreach (CulvertRowData allCulvertRow in AllCulvertRows)
		{
			if (allCulvertRow.STT < BatchFromSTT || allCulvertRow.STT > BatchToSTT)
			{
				continue;
			}
			if (isTechnicalComp && !allCulvertRow.IsTechnical)
			{
				continue;
			}
			if (!isTechnicalComp && allCulvertRow.IsTechnical)
			{
				continue;
			}
			if (isPrecastComp && (allCulvertRow.IsCastInPlace || allCulvertRow.IsTechnical))
			{
				continue;
			}
			if (isCastInPlaceComp)
			{
				if (!allCulvertRow.IsCastInPlace || allCulvertRow.IsTechnical)
				{
					continue;
				}
				if (!string.IsNullOrEmpty(text) && 
				    !string.IsNullOrEmpty(allCulvertRow.AssignedProfileName) && 
				    !string.Equals(text, "Cống đổ tại chỗ", StringComparison.OrdinalIgnoreCase) && 
				    !string.Equals(text, allCulvertRow.AssignedProfileName, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
			}

			foreach (var (categoryName, paramName, val) in list)
			{
				allCulvertRow.SetParamOverride(text, categoryName, paramName, val);
			}
			num++;
		}
		UpdateBatchTableForSelectedParam();
		lock (_cachedRealMeshes)
		{
			_cachedRealMeshes.Clear();
		}
		if (SelectedCulvertRowForPreview != null)
		{
			ExtractRealFamilyGeometryForPreview(SelectedCulvertRowForPreview);
		}
		UpdatePreviewGeometry();
		UpdateInspectorCulvertRows();
		RefreshInspectorParameterMappings();
		System.Windows.MessageBox.Show($"Đã gán thành công {list.Count} tham số tùy biến cho {num} lý trình (Phạm vi STT {BatchFromSTT} đến {BatchToSTT}).", "Hoàn tất gán tham số", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	public void SelectAllParamsForCopy()
	{
		foreach (ParameterMappingItem filteredParameterMapping in FilteredParameterMappings)
		{
			if (!filteredParameterMapping.IsYesNoParameter && !string.IsNullOrWhiteSpace(filteredParameterMapping.CustomValue))
			{
				filteredParameterMapping.IsSelected = true;
			}
			else
			{
				filteredParameterMapping.IsSelected = false;
			}
		}
	}

	public void UnselectAllParamsForCopy()
	{
		foreach (ParameterMappingItem filteredParameterMapping in FilteredParameterMappings)
		{
			filteredParameterMapping.IsSelected = false;
		}
	}

	public void CopyCustomParamsToOtherComponents()
	{
		if (SelectedComponentForTab02?.SelectedSymbol?.GetFreshSymbol(Doc) == null)
		{
			System.Windows.MessageBox.Show("Vui lòng chọn một cấu kiện nguồn trước khi thực hiện sao chép.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		CulvertComponentItem srcComp = SelectedComponentForTab02;
		string srcGroup = srcComp.GroupType ?? string.Empty;
		if (srcComp.CategoryType == null)
		{
			_ = string.Empty;
		}
		List<CulvertComponentItem> list = new List<CulvertComponentItem>();
		if (OutletComponents.Contains(srcComp) || (srcGroup == "Cửa xả" && !ApronComponents.Contains(srcComp)))
		{
			list = OutletComponents.Where((CulvertComponentItem c) => c != srcComp && c.IsActive && c.SelectedSymbol != null).ToList();
			srcGroup = "Cửa xả";
		}
		else if (ApronComponents.Contains(srcComp) || srcGroup == "Sân gia cố")
		{
			list = ApronComponents.Where((CulvertComponentItem c) => c != srcComp && c.IsActive && c.SelectedSymbol != null).ToList();
			srcGroup = "Sân gia cố";
		}
		else if (PrecastBarrelComponents.Contains(srcComp) || 
		         string.Equals(srcGroup, "Cống đúc sẵn", StringComparison.OrdinalIgnoreCase) || 
		         string.Equals(srcComp.ProfileName, "Cống đúc sẵn", StringComparison.OrdinalIgnoreCase))
		{
			list = PrecastBarrelComponents.Where((CulvertComponentItem c) => c != srcComp && c.IsActive && c.SelectedSymbol != null).ToList();
			srcGroup = "Cống đúc sẵn";
		}
		else if (CastInPlaceBarrelComponents.Contains(srcComp) || 
		         string.Equals(srcGroup, "Cống đổ tại chỗ", StringComparison.OrdinalIgnoreCase) || 
		         (srcGroup != null && srcGroup.Contains("đổ tại chỗ")) ||
		         CastInPlaceProfiles.Any(p => p.Components.Contains(srcComp) || (!string.IsNullOrEmpty(srcComp.ProfileName) && string.Equals(p.TypeName, srcComp.ProfileName, StringComparison.OrdinalIgnoreCase))))
		{
			CulvertTypeProfile culvertTypeProfile = CastInPlaceProfiles.FirstOrDefault((CulvertTypeProfile p) => p.Components.Contains(srcComp) || (!string.IsNullOrEmpty(srcComp.ProfileName) && p.TypeName == srcComp.ProfileName));
			if (culvertTypeProfile != null)
			{
				list = culvertTypeProfile.Components.Where((CulvertComponentItem c) => c != srcComp && c.IsActive && c.SelectedSymbol != null).ToList();
				srcGroup = culvertTypeProfile.TypeName;
			}
			else
			{
				list = CastInPlaceBarrelComponents.Where((CulvertComponentItem c) => c != srcComp && c.IsActive && c.SelectedSymbol != null).ToList();
				srcGroup = "Cống đổ tại chỗ";
			}
		}
		else if (TechnicalBarrelComponents.Contains(srcComp) || srcGroup == "Cống kỹ thuật")
		{
			list = TechnicalBarrelComponents.Where((CulvertComponentItem c) => c != srcComp && c.IsActive && c.SelectedSymbol != null).ToList();
			srcGroup = "Cống kỹ thuật";
		}
		else
		{
			list = ActiveAssignedFamiliesForTab02.Where((CulvertComponentItem c) => c != srcComp && c.GroupType == srcGroup && c.IsActive && c.SelectedSymbol != null).ToList();
		}
		if (list.Count == 0)
		{
			System.Windows.MessageBox.Show("Không tìm thấy cấu kiện khác trong cùng cụm '" + srcGroup + "' để sao chép giá trị.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		List<ParameterMappingItem> list2 = FilteredParameterMappings.Where((ParameterMappingItem m) => !m.IsYesNoParameter && !string.IsNullOrWhiteSpace(m.CustomValue)).ToList();
		List<ParameterMappingItem> list3 = list2.Where((ParameterMappingItem m) => m.IsSelected).ToList();
		if (list3.Count == 0)
		{
			list3 = list2;
		}
		if (list3.Count == 0)
		{
			System.Windows.MessageBox.Show("Không có giá trị số liệu tùy biến nào được nhập để sao chép.\n(Lưu ý: Các tham số kiểu Yes/No được giữ nguyên theo từng cấu kiện, không sao chép).", "Chưa có số liệu", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		int num = 0;
		foreach (CulvertComponentItem item in list)
		{
			EnsureComponentParametersScanned(item);
			string tgtCat = item.CategoryType;
			string tgtProf = item.ProfileName ?? "";
			foreach (ParameterMappingItem srcItem in list3)
			{
				ParameterMappingItem parameterMappingItem = ParameterMappings.FirstOrDefault((ParameterMappingItem m) => string.Equals(m.CategoryName, tgtCat, StringComparison.OrdinalIgnoreCase) && string.Equals(m.InternalName, srcItem.InternalName, StringComparison.OrdinalIgnoreCase) && string.Equals(m.ProfileName ?? "", tgtProf, StringComparison.OrdinalIgnoreCase));
				if (parameterMappingItem != null && !parameterMappingItem.IsYesNoParameter)
				{
					parameterMappingItem.CustomValue = srcItem.CustomValue;
					parameterMappingItem.IsSelected = true;
					num++;
				}
			}
		}
		FilterParameterMappings();
		lock (_cachedRealMeshes)
		{
			_cachedRealMeshes.Clear();
		}
		if (SelectedCulvertRowForPreview != null)
		{
			ExtractRealFamilyGeometryForPreview(SelectedCulvertRowForPreview);
		}
		UpdatePreviewGeometry();
		System.Windows.MessageBox.Show($"Đã sao chép thành công giá trị của {list3.Count} thông số bạn đã nhập sang {list.Count} cấu kiện khác trong cụm '{srcGroup}'!\n(Chỉ sao chép giá trị số liệu tùy biến đã nhập, không sao chép tham số và giữ nguyên toàn bộ biến Yes/No theo từng cấu kiện).\n\nDanh sách cấu kiện đã nhận giá trị:\n• {string.Join("\n• ", list.Select((CulvertComponentItem t) => t.CategoryType))}", "Sao chép giá trị hoàn tất", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	private void AddNewCustomBimParameter()
	{
		string text = SelectedTargetScopeForTab04 ?? "Tất cả cấu kiện";
		if (text == "TẤT CẢ")
		{
			text = "Tất cả cấu kiện";
		}
		CustomBimParameterItem customBimParameterItem = new CustomBimParameterItem
		{
			IsActive = true,
			ParamName = "BIM_ThamSoMoi",
			RevitGroup = "Constraints",
			TargetScope = text,
			ValueTemplate = "{LyTrinh}",
			Description = "Thuộc tính tùy biến dự án"
		};
		customBimParameterItem.PropertyChanged += OnCustomBimParameterPropertyChanged;
		CustomBimParameters.Add(customBimParameterItem);
		DisplayedCustomBimParameters.Add(customBimParameterItem);
		SelectedCustomBimParameter = customBimParameterItem;
		UpdateCustomBimParameterPreviews();
	}

	private void OnCustomBimParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(CustomBimParameterItem.PreviewValue))
		{
			UpdateCustomBimParameterPreviews();
		}
	}

	private void RemoveSelectedCustomBimParameter()
	{
		if (SelectedCustomBimParameter != null)
		{
			var item = SelectedCustomBimParameter;
			CustomBimParameters.Remove(item);
			DisplayedCustomBimParameters.Remove(item);
			SelectedCustomBimParameter = DisplayedCustomBimParameters.FirstOrDefault();
			UpdateCustomBimParameterPreviews();
		}
	}

	public void ResetDefaultCustomBimParameters()
	{
		string currentScope = SelectedTargetScopeForTab04 ?? "Tất cả cấu kiện";
		var existing = CustomBimParameters.Where(p => string.Equals(p.TargetScope, currentScope, StringComparison.OrdinalIgnoreCase)).ToList();
		foreach (var old in existing)
		{
			old.PropertyChanged -= OnCustomBimParameterPropertyChanged;
			CustomBimParameters.Remove(old);
		}
		EnsureDefaultParametersForScope(currentScope);
		RefreshDisplayedCustomBimParameters();
	}

	public void InitializeAllDefaultBimParameters()
	{
		CustomBimParameters.Clear();
		foreach (CustomBimParameterItem item in new List<CustomBimParameterItem>
		{
			new CustomBimParameterItem
			{
				IsActive = true,
				ParamName = "Loại công trình",
				RevitGroup = "Constraints",
				TargetScope = "Tất cả cấu kiện",
				ValueTemplate = "Giao thông",
				Description = "Loại công trình hạ tầng / giao thông"
			},
			new CustomBimParameterItem
			{
				IsActive = true,
				ParamName = "Tên công trình",
				RevitGroup = "Constraints",
				TargetScope = "Tất cả cấu kiện",
				ValueTemplate = "Đường ven biển Đắk Lắk",
				Description = "Tên dự án công trình"
			},
			new CustomBimParameterItem
			{
				IsActive = true,
				ParamName = "Hạng mục",
				RevitGroup = "Constraints",
				TargetScope = "Tất cả cấu kiện",
				ValueTemplate = "cụm Lý trình Km",
				Description = "Hạng mục cống thoát nước"
			},
			new CustomBimParameterItem
			{
				IsActive = true,
				ParamName = "Lý trình",
				RevitGroup = "Constraints",
				TargetScope = "Tất cả cấu kiện",
				ValueTemplate = "{LyTrinh}",
				Description = "Lý trình thiết kế cống"
			},
			new CustomBimParameterItem
			{
				IsActive = true,
				ParamName = "Phân nhóm cấu kiện",
				RevitGroup = "Constraints",
				TargetScope = "Tất cả cấu kiện",
				ValueTemplate = "{LoaiCong} {KhauDo}",
				Description = "Phân nhóm chủng loại và khẩu độ"
			},
			new CustomBimParameterItem
			{
				IsActive = true,
				ParamName = "Tên cấu kiện",
				RevitGroup = "Constraints",
				TargetScope = "Tất cả cấu kiện",
				ValueTemplate = "TCDS.{STT_DOT}",
				Description = "Tên mã hiệu cấu kiện (đốt thân cống)"
			},
			new CustomBimParameterItem
			{
				IsActive = true,
				ParamName = "Mô tả",
				RevitGroup = "Constraints",
				TargetScope = "Tất cả cấu kiện",
				ValueTemplate = "Đốt cống {STT_DOT}",
				Description = "Mô tả chi tiết cấu kiện"
			},
			new CustomBimParameterItem
			{
				IsActive = true,
				ParamName = "Vật liệu",
				RevitGroup = "Constraints",
				TargetScope = "Tất cả cấu kiện",
				ValueTemplate = "{VatLieu}",
				Description = "Vật liệu đồng bộ từ Tab 03"
			},
			new CustomBimParameterItem
			{
				IsActive = false,
				ParamName = "Comments",
				RevitGroup = "Identity Data",
				TargetScope = "Tất cả cấu kiện",
				ValueTemplate = "Cống {STT} - {LoaiCong} ({KhauDo})",
				Description = "Ghi chú Revit mặc định (Comments)"
			},
			new CustomBimParameterItem
			{
				IsActive = false,
				ParamName = "Mark",
				RevitGroup = "Identity Data",
				TargetScope = "Tất cả cấu kiện",
				ValueTemplate = "CN-{STT}",
				Description = "Mã hiệu cấu kiện (Mark)"
			}
		})
		{
			item.PropertyChanged += OnCustomBimParameterPropertyChanged;
			CustomBimParameters.Add(item);
		}
		SelectedCustomBimParameter = CustomBimParameters.FirstOrDefault();
		UpdateCustomBimParameterPreviews();
	}

	private bool _isUpdatingCustomBimPreviews = false;

	public void UpdateCustomBimParameterPreviews()
	{
		if (_isUpdatingCustomBimPreviews) return;
		try
		{
			_isUpdatingCustomBimPreviews = true;
			CulvertRowData row = FilteredCulvertRowsForTab04.FirstOrDefault() ?? AllCulvertRows.FirstOrDefault() ?? new CulvertRowData
		{
			STT = 1,
			LyTrinh = "Km0+100",
			LoaiCong = "CỐNG HỘP ĐÚC SẴN",
			KhauDo = "1.5x1.5",
			SoCua = 1,
			ChieuDai = 12.0,
			DoDoc = 2.5,
			Z1 = 10.5,
			Z2 = 9.8
		};
		string materialName = ComponentMaterials.FirstOrDefault()?.MaterialName ?? "Bê tông C25";
		foreach (CustomBimParameterItem customBimParameter in CustomBimParameters)
		{
			customBimParameter.PreviewValue = BimParameterService.ResolveValueTemplate(customBimParameter.ValueTemplate, row, 1, "TL", materialName);
		}
		}
		finally
		{
			_isUpdatingCustomBimPreviews = false;
		}
	}

	private void ExecuteUpdateBimParametersOnly()
	{
		ExecuteUpdateBimAndMaterials();
	}

	private void ExecuteCopyBimParametersToScope()
	{
		string currentScope = SelectedCulvertTypeForTab04 ?? "TẤT CẢ";
		List<CustomBimParameterItem> list = CustomBimParameters.Where((CustomBimParameterItem p) => p.TargetScope == currentScope || currentScope == "TẤT CẢ").ToList();
		if (!list.Any())
		{
			list = CustomBimParameters.ToList();
		}
		foreach (CustomBimParameterItem item in list)
		{
			item.TargetScope = "Tất cả cấu kiện";
		}
		System.Windows.MessageBox.Show($"Đã đồng bộ {list.Count} tham số BIM sang áp dụng chung cho 'Tất cả cấu kiện'!", "Sao chép cấu hình", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	public void RunValidation()
	{
		ValidationResults.Clear();
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		if (AllCulvertRows.Count == 0)
		{
			ValidationResults.Add(new ValidationResultItem
			{
				Level = ValidationLevel.Warning,
				RowIndex = 0,
				Category = "Dữ liệu",
				Message = "Chưa nạp bảng tính dữ liệu cống."
			});
			num2++;
		}
		foreach (CulvertRowData allCulvertRow in AllCulvertRows)
		{
			if (allCulvertRow.X1 == 0.0 && allCulvertRow.Y1 == 0.0)
			{
				ValidationResults.Add(new ValidationResultItem
				{
					Level = ValidationLevel.Error,
					RowIndex = allCulvertRow.STT,
					Category = "Tọa độ",
					Message = $"Cống STT {allCulvertRow.STT} ({allCulvertRow.LyTrinh}): Thiếu tọa độ Sân cống 1 (X1=0, Y1=0)."
				});
				num++;
			}
			if (allCulvertRow.X2 == 0.0 && allCulvertRow.Y2 == 0.0)
			{
				ValidationResults.Add(new ValidationResultItem
				{
					Level = ValidationLevel.Error,
					RowIndex = allCulvertRow.STT,
					Category = "Tọa độ",
					Message = $"Cống STT {allCulvertRow.STT} ({allCulvertRow.LyTrinh}): Thiếu tọa độ Sân cống 2 (X2=0, Y2=0)."
				});
				num++;
			}
			double num4 = allCulvertRow.TinhChieuDai2D();
			if (num4 < 1.0)
			{
				ValidationResults.Add(new ValidationResultItem
				{
					Level = ValidationLevel.Error,
					RowIndex = allCulvertRow.STT,
					Category = "Hình học",
					Message = $"Cống STT {allCulvertRow.STT} ({allCulvertRow.LyTrinh}): Chiều dài quá ngắn ({num4:F2} m)."
				});
				num++;
			}
			if (allCulvertRow.ChieuDai > 0.0)
			{
				double num5 = Math.Abs(num4 - allCulvertRow.ChieuDai);
				if (num5 > 0.5)
				{
					ValidationResults.Add(new ValidationResultItem
					{
						Level = ValidationLevel.Warning,
						RowIndex = allCulvertRow.STT,
						Category = "Sai số",
						Message = $"Cống STT {allCulvertRow.STT} ({allCulvertRow.LyTrinh}): Chiều dài tọa độ ({num4:F2}m) lệch so với thiết kế ({allCulvertRow.ChieuDai:F2}m) là {num5:F2}m."
					});
					num2++;
				}
			}
			if (allCulvertRow.SoHopNoi == 1 && (allCulvertRow.KC_HN1 <= 0.0 || allCulvertRow.KC_HN1 >= num4))
			{
				ValidationResults.Add(new ValidationResultItem
				{
					Level = ValidationLevel.Error,
					RowIndex = allCulvertRow.STT,
					Category = "Hộp nối",
					Message = $"Cống STT {allCulvertRow.STT}: Khoảng cách Hộp nối 1 KC_HN1 ({allCulvertRow.KC_HN1}m) không nằm trong chiều dài cống."
				});
				num++;
			}
			else if (allCulvertRow.SoHopNoi >= 2 && allCulvertRow.KC_HN1 + allCulvertRow.KC_HN2 >= num4)
			{
				ValidationResults.Add(new ValidationResultItem
				{
					Level = ValidationLevel.Error,
					RowIndex = allCulvertRow.STT,
					Category = "Hộp nối",
					Message = $"Cống STT {allCulvertRow.STT}: Tổng khoảng cách 2 hộp nối (KC_HN1 + KC_HN2) vượt quá chiều dài cống (Va chạm vị trí)."
				});
				num++;
			}
		}
		if (num == 0 && num2 == 0 && AllCulvertRows.Count > 0)
		{
			ValidationResults.Add(new ValidationResultItem
			{
				Level = ValidationLevel.Info,
				RowIndex = 0,
				Category = "Kiểm tra",
				Message = $"Tất cả {AllCulvertRows.Count} cống đều đạt tiêu chuẩn hình học và sẵn sàng dựng mô hình."
			});
			num3++;
		}
		TotalErrors = num;
		TotalWarnings = num2;
		TotalInfos = num3;
		StatusConclusion = ((num == 0) ? "HỢP LỆ (SẴN SÀNG)" : $"CÓ {num} LỖI CẦN XỬ LÝ");
	}

	private void ExecutePlacement()
	{
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		if (AllCulvertRows.Count == 0)
		{
			System.Windows.MessageBox.Show("Vui lòng nạp file Excel trước khi thực hiện.", "Chưa có dữ liệu", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (!PrecastBarrelComponents.Any((CulvertComponentItem c) => c.IsActive && c.SelectedSymbol != null) && !CastInPlaceBarrelComponents.Any((CulvertComponentItem c) => c.IsActive && c.SelectedSymbol != null) && (!IsTechnicalCulvertMode || !TechnicalBarrelComponents.Any((CulvertComponentItem c) => c.IsActive && c.SelectedSymbol != null)))
		{
			System.Windows.MessageBox.Show("Vui lòng kích hoạt và chọn Family cho Cống đúc sẵn hoặc Cống đổ tại chỗ ở Khung 2.", "Chưa chọn Family", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		RunValidation();
		if (TotalErrors > 0 && System.Windows.MessageBox.Show($"Dữ liệu đang có {TotalErrors} lỗi hình học. Bạn có chắc chắn muốn tiếp tục bỏ qua các cống lỗi để tạo các cống hợp lệ?", "Cảnh báo lỗi", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
		{
			return;
		}
		ExecutionStatus = "Đang dựng mô hình trong Revit...";
		List<CulvertRowData> rowsToBuild = AllCulvertRows.Where((CulvertRowData r) => r.IsSelected && !r.HasError).ToList();
		CulvertArrayMode modePrecast = ((SelectedArrayModeIndex_Precast != 0) ? CulvertArrayMode.OneWay : CulvertArrayMode.CenterOut);
		CulvertArrayMode modeCastInPlace = ((SelectedArrayModeIndex_CastInPlace != 0) ? CulvertArrayMode.OneWay : CulvertArrayMode.CenterOut);
		bool useSurveyPoint = SelectedCoordSysIndex == 0;
		List<CulvertComponentItem> targetPrecast = (IsTechnicalCulvertMode ? TechnicalBarrelComponents.ToList() : PrecastBarrelComponents.ToList());
		List<CulvertComponentItem> targetManhole = (IsTechnicalCulvertMode ? TechnicalManholeComponents.ToList() : ManholeComponents.ToList());
		double curLStdPrecast = (IsTechnicalCulvertMode ? L_Std : L_Std_Precast);
		double curKheHoPrecast = (IsTechnicalCulvertMode ? Khe_Ho : Khe_Ho_Precast);
		_eventHandler.SetAction((UIApplication app) =>
		{
			Document document = app.ActiveUIDocument.Document;
			(int SuccessCount, int ErrorCount, List<string> Logs) result = CulvertCoreEngine.BuildAllCulvertsV2(document, rowsToBuild, BimConfig, CustomBimParameters, ParameterMappings, targetPrecast, CastInPlaceBarrelComponents.ToList(), OutletComponents.ToList(), ApronComponents.ToList(), targetManhole, curLStdPrecast, curKheHoPrecast, modePrecast, L_Std_CastInPlace, Khe_Ho_CastInPlace, modeCastInPlace, B_Box, KhoangCachTimDefault, useSurveyPoint, ComponentMaterials, SelectedDuplicateOptionIndex, CastInPlaceProfiles.ToList());
			try
			{
				ProjectConfigStorageService.SaveToDocument(document, ExportCurrentState());
			}
			catch { }
			((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)(() =>
			{
				ExecutionStatus = $"Hoàn tất: Thực hiện thành công {result.SuccessCount}/{rowsToBuild.Count} cống.";
				if (result.ErrorCount > 0)
				{
					string value = string.Join("\n", result.Logs.Where((string l) => l.Contains("❌")));
					System.Windows.MessageBox.Show($"Quá trình thực hiện hoàn thành có cảnh báo/lỗi!\n- Thành công: {result.SuccessCount}\n- Thất bại: {result.ErrorCount}\n\nChi tiết lỗi:\n{value}\n\n(Xem thêm log đầy đủ tại: culvert_build_error.log)", "Kết quả thực hiện", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
				else
				{
					System.Windows.MessageBox.Show($"Quá trình thực hiện mô hình cống hoàn thành!\n- Thành công: {result.SuccessCount}/{rowsToBuild.Count} cống\n- Thất bại: {result.ErrorCount}\n\n(Các cống đã dựng trong Revit đã được tự động cập nhật thông tin BIM & Vật liệu)", "Kết quả thực hiện", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
				try
				{
					SaveCurrentSheetOverrides();
					UpdateInspectorCulvertRows();
					RefreshInspectorParameterMappings();
					UpdatePreviewGeometry();
				}
				catch
				{
				}
			}));
		});
		_externalEvent.Raise();
	}

	private void ExecuteUpdateBimAndMaterials()
	{
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		if (AllCulvertRows.Count == 0)
		{
			System.Windows.MessageBox.Show("Vui lòng nạp file Excel trước khi thực hiện.", "Chưa có dữ liệu", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		List<CulvertRowData> rowsToUpdate = AllCulvertRows.Where((CulvertRowData r) => r.IsSelected && !r.HasError).ToList();
		if (rowsToUpdate.Count == 0)
		{
			System.Windows.MessageBox.Show("Vui lòng chọn ít nhất một cống hợp lệ để cập nhật.", "Chưa chọn cống", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		ExecutionStatus = "Đang cập nhật thông tin BIM và Vật liệu vào Revit...";
		bool useSurveyPoint = SelectedCoordSysIndex == 0;
		_eventHandler.SetAction((UIApplication app) =>
		{
			Document document = app.ActiveUIDocument.Document;
			(int SuccessCount, int ErrorCount, int TotalElementsUpdated, List<string> Logs) result = CulvertCoreEngine.UpdateCulvertBimAndMaterials(document, rowsToUpdate, BimConfig, CustomBimParameters, ParameterMappings, ComponentMaterials, useSurveyPoint);
			try
			{
				ProjectConfigStorageService.SaveToDocument(document, ExportCurrentState());
			}
			catch { }
			((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)(() =>
			{
				ExecutionStatus = $"Hoàn tất cập nhật: {result.SuccessCount}/{rowsToUpdate.Count} cống ({result.TotalElementsUpdated} cấu kiện).";
				if (result.ErrorCount > 0)
				{
					string value = string.Join("\n", result.Logs.Where((string l) => l.Contains("❌") || l.Contains("⚠\ufe0f")));
					System.Windows.MessageBox.Show($"Cập nhật hoàn thành có cảnh báo/lỗi:\n- Cập nhật thành công: {result.SuccessCount} cống ({result.TotalElementsUpdated} cấu kiện)\n- Chưa tìm thấy/Lỗi: {result.ErrorCount}\n\nChi tiết:\n{value}\n\n(Nếu cống chưa có mô hình trong Revit, vui lòng bấm 'THỰC HIỆN TẠO MÔ HÌNH')", "Kết quả cập nhật BIM & Vật liệu", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
				else
				{
					System.Windows.MessageBox.Show($"Đã tự động cập nhật thành công thông tin BIM và Vật liệu cho {result.SuccessCount} cống ({result.TotalElementsUpdated} cấu kiện) đã dựng trong Revit!", "Cập nhật thành công", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
				try
				{
					SaveCurrentSheetOverrides();
					UpdateInspectorCulvertRows();
					RefreshInspectorParameterMappings();
					UpdatePreviewGeometry();
				}
				catch { }
			}));
		});
		_externalEvent.Raise();
	}

	#region Project State Persistence (LÆ°u & KhÃ´i phá»¥c cáº¥u hÃ¬nh dá»± Ã¡n)
	private List<CulvertRowOverrideSaveDto> GetAllSheetOverridesForExport()
	{
		SaveCurrentSheetOverrides();
		var list = _overridesBySheet.Values.SelectMany(x => x).ToList();
		if (!list.Any() && AllCulvertRows != null && AllCulvertRows.Count > 0)
		{
			list = AllCulvertRows
				.Where(r => r.ParameterOverrides != null && r.ParameterOverrides.Count > 0)
				.Select(r => new CulvertRowOverrideSaveDto
				{
					SheetName = SelectedSheetName ?? string.Empty,
					LyTrinh = r.LyTrinh ?? string.Empty,
					STT = r.STT,
					Overrides = new Dictionary<string, string>(r.ParameterOverrides, StringComparer.OrdinalIgnoreCase)
				}).ToList();
		}
		return list;
	}

	public ProjectStateConfig ExportCurrentState()
	{
		return new ProjectStateConfig
		{
			Version = "1.0",
			SavedAt = DateTime.Now,
			ExcelFilePath = ExcelFilePath ?? string.Empty,
			SelectedSheetName = SelectedSheetName ?? string.Empty,
			SelectedCoordSysIndex = SelectedCoordSysIndex,
			DuplicateOption = SelectedDuplicateOptionIndex,
			SelectedCulvertTemplateType = SelectedCulvertTemplateType ?? "Cá»NG Há»˜P ÄÃšC Sáº´N",
			IsTechnicalCulvertMode = IsTechnicalCulvertMode,
			L_Std_Precast = L_Std_Precast,
			Khe_Ho_Precast = Khe_Ho_Precast,
			SelectedArrayModeIndex_Precast = SelectedArrayModeIndex_Precast,
			L_Std_CastInPlace = L_Std_CastInPlace,
			Khe_Ho_CastInPlace = Khe_Ho_CastInPlace,
			SelectedArrayModeIndex_CastInPlace = SelectedArrayModeIndex_CastInPlace,
			L_Std_Technical = L_Std_Technical,
			Khe_Ho_Technical = Khe_Ho_Technical,
			SelectedArrayModeIndex_Technical = SelectedArrayModeIndex_Technical,
			L_Std = L_Std,
			Khe_Ho = Khe_Ho,
			B_Box = B_Box,
			DefaultKhoangCachTim = KhoangCachTimDefault,
			PrecastBarrels = PrecastBarrelComponents.Select(ToComponentDto).ToList(),
			CastInPlaceBarrels = CastInPlaceBarrelComponents.Select(ToComponentDto).ToList(),
			Outlets = OutletComponents.Select(ToComponentDto).ToList(),
			Aprons = ApronComponents.Select(ToComponentDto).ToList(),
			Manholes = ManholeComponents.Select(ToComponentDto).ToList(),
			TechnicalBarrels = TechnicalBarrelComponents.Select(ToComponentDto).ToList(),
			TechnicalManholes = TechnicalManholeComponents.Select(ToComponentDto).ToList(),
			CastInPlaceProfiles = CastInPlaceProfiles.Select((CulvertTypeProfile p) => new ProfileSaveDto
			{
				TypeName = p.TypeName,
				CulvertKind = p.CulvertKind,
				L_Std = p.L_Std,
				Khe_Ho = p.Khe_Ho,
				SelectedArrayModeIndex = p.SelectedArrayModeIndex,
				Components = p.Components.Select(ToComponentDto).ToList()
			}).ToList(),
			ParameterMappings = ParameterMappings.Select((ParameterMappingItem m) => new ParameterMappingSaveDto
			{
				CategoryName = m.CategoryName,
				ProfileName = m.ProfileName,
				FamilyName = m.FamilyName,
				InternalName = m.InternalName,
				DisplayName = m.DisplayName,
				GroupName = m.GroupName,
				MappedField = m.MappedField,
				CustomValue = m.CustomValue,
				DefaultValue = m.DefaultValue,
				DataType = m.DataType,
				IsSelected = m.IsSelected
			}).ToList(),
			CulvertRowOverrides = GetAllSheetOverridesForExport(),
			Materials = ComponentMaterials.Select((CulvertMaterialItem m) => new MaterialSaveDto
			{
				GroupType = m.GroupType,
				CulvertType = m.CulvertType,
				CategoryType = m.CategoryType,
				FamilyDisplayName = m.FamilyDisplayName,
				MaterialName = m.MaterialName,
				ColorHex = m.ColorHex,
				IsActive = m.IsActive
			}).ToList(),
			BimConfig = new BimInfoConfig
			{
				TenCongTrinh = BimConfig.TenCongTrinh,
				TenNhom = BimConfig.TenNhom,
				MoTa = BimConfig.MoTa,
				MauTenDotCong = BimConfig.MauTenDotCong,
				MauTenSanCongTL = BimConfig.MauTenSanCongTL,
				MauTenSanCongHL = BimConfig.MauTenSanCongHL,
				MauTenHopNoi = BimConfig.MauTenHopNoi,
				MauTenBeTongLot = BimConfig.MauTenBeTongLot,
				AutoSetCoordinates = BimConfig.AutoSetCoordinates,
				AutoSetElevations = BimConfig.AutoSetElevations,
				AutoCreateSharedParameters = BimConfig.AutoCreateSharedParameters
			},
			CustomBimParameters = CustomBimParameters.Select((CustomBimParameterItem p) => new CustomBimParameterSaveDto
			{
				IsActive = p.IsActive,
				ParamName = p.ParamName,
				RevitGroup = p.RevitGroup,
				TargetScope = p.TargetScope,
				ValueTemplate = p.ValueTemplate,
				Description = p.Description
			}).ToList()
		};
	}

	private ComponentSaveDto ToComponentDto(CulvertComponentItem c)
	{
		return new ComponentSaveDto
		{
			GroupType = c.GroupType,
			ProfileName = c.ProfileName,
			CategoryType = c.CategoryType,
			FamilyName = c.SelectedSymbol?.Symbol?.FamilyName ?? c.SelectedSymbol?.FamilyName ?? string.Empty,
			TypeName = c.SelectedSymbol?.Symbol?.Name ?? c.SelectedSymbol?.TypeName ?? string.Empty,
			OffsetZ = c.OffsetZ,
			IsActive = c.IsActive,
			Note = c.Note
		};
	}

	public void ApplyState(ProjectStateConfig state)
	{
		if (state == null) return;
		bool prevInit = _isInitializing;
		_isInitializing = true;
		try
		{
			SelectedCoordSysIndex = state.SelectedCoordSysIndex;
			SelectedDuplicateOptionIndex = state.DuplicateOption;
			L_Std_Precast = state.L_Std_Precast;
			Khe_Ho_Precast = state.Khe_Ho_Precast;
			SelectedArrayModeIndex_Precast = state.SelectedArrayModeIndex_Precast;
			L_Std_CastInPlace = state.L_Std_CastInPlace;
			Khe_Ho_CastInPlace = state.Khe_Ho_CastInPlace;
			SelectedArrayModeIndex_CastInPlace = state.SelectedArrayModeIndex_CastInPlace;
			L_Std_Technical = state.L_Std_Technical;
			Khe_Ho_Technical = state.Khe_Ho_Technical;
			SelectedArrayModeIndex_Technical = state.SelectedArrayModeIndex_Technical;
			L_Std = state.L_Std;
			Khe_Ho = state.Khe_Ho;
			B_Box = state.B_Box;
			KhoangCachTimDefault = state.DefaultKhoangCachTim;
			if (!string.IsNullOrEmpty(state.SelectedCulvertTemplateType))
			{
				SelectedCulvertTemplateType = state.SelectedCulvertTemplateType;
			}
			IsTechnicalCulvertMode = state.IsTechnicalCulvertMode;

			ApplyComponentDtos(PrecastBarrelComponents, state.PrecastBarrels);
			ApplyComponentDtos(OutletComponents, state.Outlets);
			ApplyComponentDtos(ApronComponents, state.Aprons);
			ApplyComponentDtos(ManholeComponents, state.Manholes);
			ApplyComponentDtos(TechnicalBarrelComponents, state.TechnicalBarrels);
			ApplyComponentDtos(TechnicalManholeComponents, state.TechnicalManholes);

			if (state.CastInPlaceProfiles != null && state.CastInPlaceProfiles.Count > 0)
			{
				foreach (var p in CastInPlaceProfiles)
				{
					p.PropertyChanged -= OnCastInPlaceProfilePropertyChanged;
					foreach (var c in p.Components) c.PropertyChanged -= OnAssemblyComponentPropertyChanged;
				}
				CastInPlaceProfiles.Clear();
				foreach (var pDto in state.CastInPlaceProfiles)
				{
					var prof = new CulvertTypeProfile(pDto.TypeName, pDto.L_Std, pDto.Khe_Ho, pDto.SelectedArrayModeIndex)
					{
						CulvertKind = pDto.CulvertKind
					};
					if (pDto.Components != null && pDto.Components.Count > 0)
					{
						foreach (var cDto in pDto.Components)
						{
							var comp = new CulvertComponentItem
							{
								GroupType = prof.TypeName,
								ProfileName = prof.TypeName,
								CategoryType = cDto.CategoryType,
								OffsetZ = cDto.OffsetZ,
								IsActive = cDto.IsActive,
								Note = cDto.Note,
								SelectedSymbol = FindSymbol(cDto.FamilyName, cDto.TypeName)
							};
							comp.PropertyChanged += OnAssemblyComponentPropertyChanged;
							prof.Components.Add(comp);
						}
					}
					prof.PropertyChanged += OnCastInPlaceProfilePropertyChanged;
					CastInPlaceProfiles.Add(prof);
				}
				SelectedCastInPlaceProfile = CastInPlaceProfiles.FirstOrDefault();
			}

			if (state.ParameterMappings != null && state.ParameterMappings.Count > 0)
			{
				foreach (var savedMap in state.ParameterMappings)
				{
					var target = ParameterMappings.FirstOrDefault(m =>
						(string.Equals(m.InternalName, savedMap.InternalName, StringComparison.OrdinalIgnoreCase) ||
						 string.Equals(m.DisplayName, savedMap.DisplayName, StringComparison.OrdinalIgnoreCase)) &&
						string.Equals(m.CategoryName, savedMap.CategoryName, StringComparison.OrdinalIgnoreCase) &&
						string.Equals(m.ProfileName, savedMap.ProfileName, StringComparison.OrdinalIgnoreCase));
					if (target != null)
					{
						if (!string.IsNullOrEmpty(savedMap.MappedField)) target.MappedField = savedMap.MappedField;
						if (!string.IsNullOrEmpty(savedMap.CustomValue)) target.CustomValue = savedMap.CustomValue;
						target.IsSelected = savedMap.IsSelected;
					}
				}
			}

			if (state.Materials != null && state.Materials.Count > 0)
			{
				foreach (var savedMat in state.Materials)
				{
					var target = ComponentMaterials.FirstOrDefault(m =>
						string.Equals(m.GroupType, savedMat.GroupType, StringComparison.OrdinalIgnoreCase) &&
						string.Equals(m.CategoryType, savedMat.CategoryType, StringComparison.OrdinalIgnoreCase) &&
						string.Equals(m.CulvertType, savedMat.CulvertType, StringComparison.OrdinalIgnoreCase));
					if (target != null)
					{
						target.MaterialName = savedMat.MaterialName;
						target.ColorHex = savedMat.ColorHex;
						target.IsActive = savedMat.IsActive;
					}
				}
			}

			if (state.BimConfig != null)
			{
				BimConfig.TenCongTrinh = state.BimConfig.TenCongTrinh;
				BimConfig.TenNhom = state.BimConfig.TenNhom;
				BimConfig.MoTa = state.BimConfig.MoTa;
				BimConfig.MauTenDotCong = state.BimConfig.MauTenDotCong;
				BimConfig.MauTenSanCongTL = state.BimConfig.MauTenSanCongTL;
				BimConfig.MauTenSanCongHL = state.BimConfig.MauTenSanCongHL;
				BimConfig.MauTenHopNoi = state.BimConfig.MauTenHopNoi;
				BimConfig.MauTenBeTongLot = state.BimConfig.MauTenBeTongLot;
				BimConfig.AutoSetCoordinates = state.BimConfig.AutoSetCoordinates;
				BimConfig.AutoSetElevations = state.BimConfig.AutoSetElevations;
				BimConfig.AutoCreateSharedParameters = state.BimConfig.AutoCreateSharedParameters;
			}

			if (state.CustomBimParameters != null && state.CustomBimParameters.Count > 0)
			{
				CustomBimParameters.Clear();
				foreach (var p in state.CustomBimParameters)
				{
					var item = new CustomBimParameterItem
					{
						IsActive = p.IsActive,
						ParamName = p.ParamName,
						RevitGroup = p.RevitGroup,
						TargetScope = p.TargetScope,
						ValueTemplate = p.ValueTemplate,
						Description = p.Description
					};
					item.PropertyChanged += OnCustomBimParameterPropertyChanged;
					CustomBimParameters.Add(item);
				}
				SelectedCustomBimParameter = CustomBimParameters.FirstOrDefault();
				UpdateCustomBimParameterPreviews();
			}

			if (!string.IsNullOrEmpty(state.ExcelFilePath) && File.Exists(state.ExcelFilePath))
			{
				ExcelFilePath = state.ExcelFilePath;
				SheetNames.Clear();
				foreach (string sheetName in ExcelReaderService.GetSheetNames(ExcelFilePath))
				{
					SheetNames.Add(sheetName);
				}
				if (!string.IsNullOrEmpty(state.SelectedSheetName) && SheetNames.Contains(state.SelectedSheetName))
				{
					SelectedSheetName = state.SelectedSheetName;
				}
				else if (SheetNames.Count > 0)
				{
					SelectedSheetName = SheetNames[0];
				}
				LoadExcelData();

				_overridesBySheet.Clear();
				if (state.CulvertRowOverrides != null && state.CulvertRowOverrides.Count > 0)
				{
					foreach (var ov in state.CulvertRowOverrides)
					{
						string sheet = string.IsNullOrEmpty(ov.SheetName) ? (state.SelectedSheetName ?? "") : ov.SheetName;
						if (!_overridesBySheet.ContainsKey(sheet))
							_overridesBySheet[sheet] = new List<CulvertRowOverrideSaveDto>();
						_overridesBySheet[sheet].Add(ov);
					}
					RestoreCurrentSheetOverrides();
				}
				UpdateInspectorCulvertRows();
				RefreshInspectorParameterMappings();
				UpdateAvailableTargetScopesForTab04();
				RefreshDisplayedCustomBimParameters();
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Lỗi ApplyState: {ex.Message}");
		}
		finally
		{
			_isInitializing = prevInit;
		}
		if (!_isInitializing)
		{
			RefreshActiveAssignedFamiliesForTab02();
			ApplyTab02FamilyFilter();
			ApplyMaterialGroupFilter();
			UpdatePreviewGeometry();
		}
	}

	private void ApplyComponentDtos(ObservableCollection<CulvertComponentItem> targetList, List<ComponentSaveDto> dtos)
	{
		if (dtos == null || dtos.Count == 0) return;
		for (int i = 0; i < dtos.Count; i++)
		{
			var dto = dtos[i];
			if (i < targetList.Count)
			{
				var item = targetList[i];
				item.IsActive = dto.IsActive;
				item.OffsetZ = dto.OffsetZ;
				if (!string.IsNullOrEmpty(dto.Note)) item.Note = dto.Note;
				var sym = FindSymbol(dto.FamilyName, dto.TypeName);
				if (sym != null) item.SelectedSymbol = sym;
			}
			else
			{
				var item = new CulvertComponentItem
				{
					GroupType = dto.GroupType,
					ProfileName = dto.ProfileName,
					CategoryType = dto.CategoryType,
					IsActive = dto.IsActive,
					OffsetZ = dto.OffsetZ,
					Note = dto.Note,
					SelectedSymbol = FindSymbol(dto.FamilyName, dto.TypeName)
				};
				item.PropertyChanged += OnAssemblyComponentPropertyChanged;
				targetList.Add(item);
			}
		}
	}

	public bool CheckDocumentHasCulvertFamilies()
	{
		if (Doc == null) return false;
		try
		{
			var cfg = ProjectConfigStorageService.LoadFromDocument(Doc);
			if (cfg != null) return true;

			return new FilteredElementCollector(Doc)
				.OfClass(typeof(Family))
				.Cast<Family>()
				.Any(f => (f.Name != null && f.Name.StartsWith("TNN_", StringComparison.OrdinalIgnoreCase))
					   || (f.Name != null && f.Name.Contains("THAN CONG", StringComparison.OrdinalIgnoreCase))
					   || (f.Name != null && f.Name.Contains("HO KY THUAT", StringComparison.OrdinalIgnoreCase)));
		}
		catch
		{
			return false;
		}
	}

	public void AutoLoadFamiliesForNewProject()
	{
		string folder = FamilyFolderPath;
		if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
		{
			folder = @"C:\Users\ADMIN\Desktop\TEST TOOL\FAMLY REVIT_HTKT";
		}
		if (Directory.Exists(folder) && Doc != null && !Doc.IsReadOnly && _eventHandler != null && _externalEvent != null)
		{
			FamilyFolderPath = folder;
			string[] rfaFiles = Directory.GetFiles(folder, "*.rfa", SearchOption.AllDirectories);
			if (rfaFiles.Length > 0)
			{
				_eventHandler.SetAction((UIApplication app) =>
				{
					var doc = app.ActiveUIDocument?.Document;
					if (doc == null) return;
					int loadedCount = 0;
					using (Transaction tx = new Transaction(doc, "u0110\u1ed3ng b\u1ed9 Family c\u1ed1ng m\u1edbi"))
					{
						tx.Start();
						CustomFamilyLoadOptions opt = new CustomFamilyLoadOptions();
						foreach (string file in rfaFiles)
						{
							try
							{
								if (doc.LoadFamily(file, opt, out Family fam))
								{
									loadedCount++;
								}
								else
								{
									loadedCount++;
								}
							}
							catch { }
						}
						tx.Commit();
					}
					((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)(() =>
					{
						LoadAvailableFamilies();
						FamilyLoadStatus = $"D\u1ef1 \u00e1n m\u1edbi: \u0110\u00e3 t\u1ef1 \u0111\u1ed9ng n\u1ea1p {loadedCount}/{rfaFiles.Length} Family th\u00e0nh c\u00f4ng!";
					}));
				});
				_externalEvent.Raise();
			}
		}
	}

	public void TryAutoLoadConfiguration()
	{
		StartupLogger.Log("[VM] TryAutoLoadConfiguration START");
		try
		{
			ProjectStateConfig? config = null;
			bool hasCulvertFamilies = CheckDocumentHasCulvertFamilies();

			if (Doc != null && !Doc.IsReadOnly)
			{
				StartupLogger.Log("[VM] TryAutoLoadConfiguration: calling LoadFromDocument...");
				config = ProjectConfigStorageService.LoadFromDocument(Doc);
				StartupLogger.Log($"[VM] TryAutoLoadConfiguration: LoadFromDocument result: {(config != null ? "CONFIG FOUND" : "NULL")}");
			}
			if (config == null && hasCulvertFamilies)
			{
				StartupLogger.Log("[VM] TryAutoLoadConfiguration: calling LoadFromLastSession...");
				config = ProjectConfigStorageService.LoadFromLastSession();
				StartupLogger.Log($"[VM] TryAutoLoadConfiguration: LoadFromLastSession result: {(config != null ? "CONFIG FOUND" : "NULL")}");
			}
			if (config != null)
			{
				StartupLogger.Log("[VM] TryAutoLoadConfiguration: calling ApplyState...");
				ApplyState(config);
				StartupLogger.Log("[VM] TryAutoLoadConfiguration: ApplyState finished.");
				FamilyLoadStatus = "\u0110\u00e3 nh\u1eadn di\u1ec7n Family c\u00f3 s\u1eb5n trong File Revit (kh\u00f4ng n\u1ea1p l\u1ea1i t\u1eeb \u1ed5 c\u1ee9ng).";
				ExecutionStatus = $"\u0110\u00e3 t\u1ef1 \u0111\u1ed9ng kh\u00f4i ph\u1ee5c c\u1ea5u h\u00ecnh d\u1ef1 \u00e1n (l\u01b0u l\u00fac: {config.SavedAt:dd/MM/yyyy HH:mm}).";
			}
			else
			{
				if (!hasCulvertFamilies)
				{
					StartupLogger.Log("[VM] New project detected: auto-loading families from folder...");
					AutoLoadFamiliesForNewProject();
				}
				else
				{
					FamilyLoadStatus = "\u0110\u00e3 nh\u1eadn di\u1ec7n Family c\u00f3 s\u1eb5n trong File Revit (kh\u00f4ng n\u1ea1p l\u1ea1i t\u1eeb \u1ed5 c\u1ee9ng).";
				}
			}
		}
		catch (Exception ex)
		{
			StartupLogger.Log($"[VM] TryAutoLoadConfiguration EXCEPTION: {ex.Message}");
			System.Diagnostics.Debug.WriteLine($"Lá»—i TryAutoLoadConfiguration: {ex.Message}");
		}
		StartupLogger.Log("[VM] TryAutoLoadConfiguration END");
	}

	private void ExecuteSaveProjectConfig()
	{
		try
		{
			var state = ExportCurrentState();
			if (_eventHandler != null && _externalEvent != null)
			{
				_eventHandler.SetAction((UIApplication app) =>
				{
					var doc = app.ActiveUIDocument?.Document;
					if (doc != null)
					{
						bool ok = ProjectConfigStorageService.SaveToDocument(doc, state);
						((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)(() =>
						{
							if (ok)
							{
								System.Windows.MessageBox.Show("ÄÃ£ lÆ°u toÃ n bá»™ thÃ´ng sá»‘ vÃ  cáº¥u hÃ¬nh 4 Tab trá»±c tiáº¿p vÃ o file Revit (.rvt) thÃ nh cÃ´ng!\n\nLáº§n sau má»Ÿ file Revit nÃ y lÃªn, toÃ n bá»™ dá»¯ liá»‡u sáº½ tá»± Ä‘á»™ng khÃ´i phá»¥c 100%.", "LÆ°u cáº¥u hÃ¬nh Revit", MessageBoxButton.OK, MessageBoxImage.Asterisk);
								ExecutionStatus = "ÄÃ£ lÆ°u cáº¥u hÃ¬nh vÃ o Revit Document thÃ nh cÃ´ng.";
							}
							else
							{
								ProjectConfigStorageService.SaveToLastSession(state);
								System.Windows.MessageBox.Show("KhÃ´ng thá»ƒ ghi vÃ o file Revit (Document Ä‘ang báº­n hoáº·c chá»‰ Ä‘á»c). ÄÃ£ tá»± Ä‘á»™ng lÆ°u cáº¥u hÃ¬nh vÃ o bá»™ nhá»› mÃ¡y tÃ­nh cá»¥c bá»™.", "ThÃ´ng bÃ¡o", MessageBoxButton.OK, MessageBoxImage.Exclamation);
							}
						}));
					}
				});
				_externalEvent.Raise();
			}
			else if (Doc != null && !Doc.IsReadOnly)
			{
				bool ok = ProjectConfigStorageService.SaveToDocument(Doc, state);
				if (ok)
				{
					System.Windows.MessageBox.Show("ÄÃ£ lÆ°u toÃ n bá»™ cáº¥u hÃ¬nh 4 Tab vÃ o file Revit thÃ nh cÃ´ng!", "LÆ°u cáº¥u hÃ¬nh", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
				else
				{
					ProjectConfigStorageService.SaveToLastSession(state);
					System.Windows.MessageBox.Show("ÄÃ£ lÆ°u cáº¥u hÃ¬nh vÃ o bá»™ nhá»› mÃ¡y tÃ­nh cá»¥c bá»™!", "LÆ°u cáº¥u hÃ¬nh", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
			}
			else
			{
				ProjectConfigStorageService.SaveToLastSession(state);
				System.Windows.MessageBox.Show("ÄÃ£ lÆ°u cáº¥u hÃ¬nh vÃ o bá»™ nhá»› mÃ¡y tÃ­nh cá»¥c bá»™!", "LÆ°u cáº¥u hÃ¬nh", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show($"Lá»—i lÆ°u cáº¥u hÃ¬nh: {ex.Message}", "Lá»—i", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void ExecuteExportPreset()
	{
		try
		{
			var dlg = new Microsoft.Win32.SaveFileDialog
			{
				Title = "Xuáº¥t Cáº¥u HÃ¬nh Dá»± Ãn Cá»‘ng Ra File Preset",
				Filter = "Cáº¥u hÃ¬nh Cá»‘ng (*.json)|*.json|Táº¥t cáº£ tá»‡p (*.*)|*.*",
				FileName = "CauHinhCong_Preset.json"
			};
			if (dlg.ShowDialog() == true)
			{
				var state = ExportCurrentState();
				bool ok = ProjectConfigStorageService.ExportPresetToFile(dlg.FileName, state);
				if (ok)
				{
					System.Windows.MessageBox.Show($"ÄÃ£ xuáº¥t file Preset cáº¥u hÃ¬nh thÃ nh cÃ´ng táº¡i:\n{dlg.FileName}\n\nBáº¡n cÃ³ thá»ƒ chia sáº» tá»‡p nÃ y cho Ä‘á»“ng nghiá»‡p hoáº·c dÃ¹ng cho dá»± Ã¡n khÃ¡c.", "Xuáº¥t Preset thÃ nh cÃ´ng", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
				else
				{
					System.Windows.MessageBox.Show("KhÃ´ng thá»ƒ ghi file Preset.", "Lá»—i xuáº¥t Preset", MessageBoxButton.OK, MessageBoxImage.Hand);
				}
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show($"Lá»—i xuáº¥t preset: {ex.Message}", "Lá»—i", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void ExecuteImportPreset()
	{
		try
		{
			var dlg = new Microsoft.Win32.OpenFileDialog
			{
				Title = "Náº¡p Cáº¥u HÃ¬nh Dá»± Ãn Cá»‘ng Tá»« File Preset",
				Filter = "Cáº¥u hÃ¬nh Cá»‘ng (*.json)|*.json|Táº¥t cáº£ tá»‡p (*.*)|*.*"
			};
			if (dlg.ShowDialog() == true)
			{
				var state = ProjectConfigStorageService.ImportPresetFromFile(dlg.FileName);
				if (state != null)
				{
					ApplyState(state);
					System.Windows.MessageBox.Show($"ÄÃ£ náº¡p vÃ  Ã¡p dá»¥ng thÃ nh cÃ´ng toÃ n bá»™ cáº¥u hÃ¬nh tá»« file Preset:\n{Path.GetFileName(dlg.FileName)}", "Náº¡p Preset thÃ nh cÃ´ng", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
				else
				{
					System.Windows.MessageBox.Show("Tá»‡p Preset khÃ´ng há»£p lá»‡ hoáº·c bá»‹ lá»—i Ä‘á»‹nh dáº¡ng.", "Lá»—i náº¡p Preset", MessageBoxButton.OK, MessageBoxImage.Hand);
				}
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show($"Lá»—i náº¡p preset: {ex.Message}", "Lá»—i", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}
	#endregion
}
