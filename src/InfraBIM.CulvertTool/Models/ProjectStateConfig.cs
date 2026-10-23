using System;
using System.Collections.Generic;

namespace InfraBIM.CulvertTool.Models
{
    /// <summary>
    /// Đóng gói toàn bộ trạng thái cấu hình của dự án cống (cả 4 Tab)
    /// Dùng cho Extensible Storage (lưu trong file Revit) và Export/Import Preset JSON
    /// </summary>
    public class ProjectStateConfig
    {
        public string Version { get; set; } = "1.0";
        public DateTime SavedAt { get; set; } = DateTime.Now;

        // Tab 01 - Khai báo & Vị trí
        public string FamilyFolderPath { get; set; } = string.Empty;
        public string ExcelFilePath { get; set; } = string.Empty;
        public string SelectedSheetName { get; set; } = string.Empty;
        public int SelectedCoordSysIndex { get; set; } = 0;
        public int DuplicateOption { get; set; } = 0;
        public string SelectedCulvertTemplateType { get; set; } = "CỐNG HỘP ĐÚC SẴN";
        public bool IsTechnicalCulvertMode { get; set; } = false;

        public double L_Std_Precast { get; set; } = 1.0;
        public double Khe_Ho_Precast { get; set; } = 0.01;
        public int SelectedArrayModeIndex_Precast { get; set; } = 1;

        public double L_Std_CastInPlace { get; set; } = 4.0;
        public double Khe_Ho_CastInPlace { get; set; } = 0.02;
        public int SelectedArrayModeIndex_CastInPlace { get; set; } = 1;

        public double L_Std_Technical { get; set; } = 1.0;
        public double Khe_Ho_Technical { get; set; } = 0.01;
        public int SelectedArrayModeIndex_Technical { get; set; } = 0;

        public double L_Std { get; set; } = 1.0;
        public double Khe_Ho { get; set; } = 0.01;
        public double B_Box { get; set; } = 1.5;
        public double DefaultKhoangCachTim { get; set; } = 2.0;

        // Assembly Components (Tab 01)
        public List<ComponentSaveDto> PrecastBarrels { get; set; } = new();
        public List<ComponentSaveDto> CastInPlaceBarrels { get; set; } = new();
        public List<ProfileSaveDto> CastInPlaceProfiles { get; set; } = new();
        public List<ComponentSaveDto> Outlets { get; set; } = new();
        public List<ComponentSaveDto> Aprons { get; set; } = new();
        public List<ComponentSaveDto> Manholes { get; set; } = new();
        public List<ComponentSaveDto> TechnicalBarrels { get; set; } = new();
        public List<ComponentSaveDto> TechnicalManholes { get; set; } = new();

        // Tab 02 - Parameter hình học (Mappings & Custom Dimensions)
        public List<ParameterMappingSaveDto> ParameterMappings { get; set; } = new();

        // Tab 02 Khung 2 - Tùy biến chi tiết theo từng lý trình
        public List<CulvertRowOverrideSaveDto> CulvertRowOverrides { get; set; } = new();

        // Tab 03 - Vật liệu cấu kiện
        public List<MaterialSaveDto> Materials { get; set; } = new();

        // Tab 04 - Thông tin BIM
        public BimInfoConfig BimConfig { get; set; } = new();
        public List<CustomBimParameterSaveDto> CustomBimParameters { get; set; } = new();
    }

    public class ComponentSaveDto
    {
        public string GroupType { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public string CategoryType { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public double OffsetZ { get; set; }
        public bool IsActive { get; set; } = true;
        public string Note { get; set; } = string.Empty;
    }

    public class ProfileSaveDto
    {
        public string TypeName { get; set; } = string.Empty;
        public string CulvertKind { get; set; } = "CastInPlace";
        public double L_Std { get; set; } = 4.0;
        public double Khe_Ho { get; set; } = 0.02;
        public int SelectedArrayModeIndex { get; set; } = 0;
        public string Description { get; set; } = string.Empty;
        public List<ComponentSaveDto> Components { get; set; } = new();
    }

    public class ParameterMappingSaveDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string InternalName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string MappedField { get; set; } = string.Empty;
        public string CustomValue { get; set; } = string.Empty;
        public string DefaultValue { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true;
    }

    public class CulvertRowOverrideSaveDto
    {
        public string SheetName { get; set; } = string.Empty;
        public string LyTrinh { get; set; } = string.Empty;
        public int STT { get; set; }
        public Dictionary<string, string> Overrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class MaterialSaveDto
    {
        public string GroupType { get; set; } = string.Empty;
        public string CulvertType { get; set; } = string.Empty;
        public string CategoryType { get; set; } = string.Empty;
        public string FamilyDisplayName { get; set; } = string.Empty;
        public string MaterialName { get; set; } = string.Empty;
        public string ColorHex { get; set; } = string.Empty;
        public double Transparency { get; set; } = 0.0;
        public string MaterialParamName { get; set; } = "Material";
        public bool IsActive { get; set; } = true;
    }

    public class CustomBimParameterSaveDto
    {
        public bool IsActive { get; set; } = true;
        public string ParamName { get; set; } = string.Empty;
        public string RevitGroup { get; set; } = "Constraints";
        public string TargetScope { get; set; } = string.Empty;
        public string ValueTemplate { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
