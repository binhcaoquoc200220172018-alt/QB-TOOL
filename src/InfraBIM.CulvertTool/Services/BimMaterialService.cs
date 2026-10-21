using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using InfraBIM.CulvertTool.Models;

namespace InfraBIM.CulvertTool.Services
{
    /// <summary>
    /// Quản lý, khởi tạo và gán vật liệu, màu sắc đồ họa cho các cấu kiện trong Revit 2025
    /// </summary>
    public static class BimMaterialService
    {
        /// <summary>
        /// Lấy danh sách tên tất cả các Material có trong Document
        /// </summary>
        public static List<string> GetAllMaterialNames(Document? doc)
        {
            var names = new List<string>();
            if (doc == null) return names;
            try
            {
                var materials = new FilteredElementCollector(doc)
                    .OfClass(typeof(Material))
                    .Cast<Material>()
                    .Where(m => !string.IsNullOrWhiteSpace(m.Name))
                    .OrderBy(m => m.Name);

                foreach (var m in materials)
                {
                    names.Add(m.Name);
                }
            }
            catch
            {
                // Bỏ qua lỗi nếu collector rỗng
            }
            return names;
        }

        private static ElementId? _cachedSolidFillId = null;

        /// <summary>
        /// Tìm FillPattern Solid Fill trong Revit Document để hiển thị màu đặc trên bề mặt
        /// </summary>
        public static ElementId? GetSolidFillPatternId(Document doc)
        {
            if (_cachedSolidFillId != null && _cachedSolidFillId != ElementId.InvalidElementId)
            {
                var test = doc.GetElement(_cachedSolidFillId);
                if (test != null) return _cachedSolidFillId;
            }

            try
            {
                var fps = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>();

                foreach (var fp in fps)
                {
                    var pat = fp.GetFillPattern();
                    if (pat != null && pat.IsSolidFill && pat.Target == FillPatternTarget.Model)
                    {
                        _cachedSolidFillId = fp.Id;
                        return _cachedSolidFillId;
                    }
                }

                foreach (var fp in fps)
                {
                    var pat = fp.GetFillPattern();
                    if (pat != null && pat.IsSolidFill)
                    {
                        _cachedSolidFillId = fp.Id;
                        return _cachedSolidFillId;
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Tìm hoặc tạo mới Material trong Document với tên, màu sắc RGB và độ trong suốt
        /// </summary>
        public static Material? GetOrCreateMaterial(
            Document doc, 
            string materialName, 
            byte r, 
            byte g, 
            byte b, 
            double transparency = 0.0)
        {
            if (string.IsNullOrWhiteSpace(materialName)) return null;

            ElementId? solidFillId = GetSolidFillPatternId(doc);
            Color matColor = new Color(r, g, b);
            int transInt = Math.Clamp((int)transparency, 0, 100);

            // Tìm material có sẵn
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => string.Equals(m.Name, materialName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                try
                {
                    // Tắt Render Appearance để Revit ưu tiên hiển thị Shading Color đúng theo người dùng chọn
                    existing.UseRenderAppearanceForShading = false;
                    existing.Color = matColor;
                    existing.Transparency = transInt;

                    if (solidFillId != null && solidFillId != ElementId.InvalidElementId)
                    {
                        existing.SurfaceForegroundPatternId = solidFillId;
                        existing.SurfaceForegroundPatternColor = matColor;
                        existing.CutForegroundPatternId = solidFillId;
                        existing.CutForegroundPatternColor = matColor;
                    }
                }
                catch
                {
                    // Bỏ qua nếu material bị khóa
                }
                return existing;
            }

            // Chưa có -> Tạo mới
            try
            {
                ElementId newId = Material.Create(doc, materialName.Trim());
                if (newId != ElementId.InvalidElementId)
                {
                    var newMat = doc.GetElement(newId) as Material;
                    if (newMat != null)
                    {
                        newMat.UseRenderAppearanceForShading = false;
                        newMat.Color = matColor;
                        newMat.Transparency = transInt;

                        if (solidFillId != null && solidFillId != ElementId.InvalidElementId)
                        {
                            newMat.SurfaceForegroundPatternId = solidFillId;
                            newMat.SurfaceForegroundPatternColor = matColor;
                            newMat.CutForegroundPatternId = solidFillId;
                            newMat.CutForegroundPatternColor = matColor;
                        }
                        return newMat;
                    }
                }
            }
            catch
            {
                // Nếu tên bị trùng hoặc lỗi ký tự đặc biệt
            }

            return null;
        }

        /// <summary>
        /// Gán Material và Graphic Overrides đổi màu trực tiếp vào đối tượng FamilyInstance và toàn bộ Subcomponents
        /// </summary>
        public static bool ApplyMaterialToInstance(
            Document doc, 
            FamilyInstance instance, 
            CulvertMaterialItem matItem)
        {
            if (instance == null || matItem == null || !matItem.IsActive) return false;
            if (string.IsNullOrWhiteSpace(matItem.MaterialName)) return false;

            var mat = GetOrCreateMaterial(doc, matItem.MaterialName, matItem.ColorR, matItem.ColorG, matItem.ColorB, matItem.Transparency);
            if (mat == null) return false;

            bool assigned = false;

            // 1. Thử gán vào Instance Parameter trước
            assigned = TryAssignMaterialParameter(instance.Parameters, mat.Id, matItem.MaterialParamName);

            // 2. Nếu instance không có, thử tìm trên Type Parameter (FamilySymbol)
            if (!assigned && instance.Symbol != null)
            {
                assigned = TryAssignMaterialParameter(instance.Symbol.Parameters, mat.Id, matItem.MaterialParamName);
            }

            // 3. Đệ quy gán cho các cấu kiện lồng bên trong (Subcomponents) nếu có
            try
            {
                var subIds = instance.GetSubComponentIds();
                if (subIds != null && subIds.Count > 0)
                {
                    foreach (var sId in subIds)
                    {
                        var subElem = doc.GetElement(sId) as FamilyInstance;
                        if (subElem != null)
                        {
                            TryAssignMaterialParameter(subElem.Parameters, mat.Id, matItem.MaterialParamName);
                            if (subElem.Symbol != null)
                            {
                                TryAssignMaterialParameter(subElem.Symbol.Parameters, mat.Id, matItem.MaterialParamName);
                            }
                        }
                    }
                }
            }
            catch { }

            // 4. QUAN TRỌNG: Thiết lập View Graphic Overrides trực tiếp trong ActiveView
            // Đảm bảo mô hình Revit 3D lập tức đổi màu chuẩn 100% theo màu người dùng chọn
            try
            {
                ApplyGraphicOverrides(doc, instance, matItem);
            }
            catch { }

            return assigned;
        }

        /// <summary>
        /// Gán màu hiển thị (OverrideGraphicSettings) trực tiếp lên view Revit cho đối tượng và subcomponents
        /// </summary>
        public static void ApplyGraphicOverrides(
            Document doc,
            FamilyInstance instance,
            CulvertMaterialItem matItem)
        {
            if (doc.ActiveView == null || doc.ActiveView.IsTemplate) return;

            ElementId? solidFillId = GetSolidFillPatternId(doc);
            Color color = new Color(matItem.ColorR, matItem.ColorG, matItem.ColorB);
            int trans = Math.Clamp((int)matItem.Transparency, 0, 100);

            var ogs = new OverrideGraphicSettings();
            if (solidFillId != null && solidFillId != ElementId.InvalidElementId)
            {
                ogs.SetSurfaceForegroundPatternId(solidFillId);
                ogs.SetSurfaceForegroundPatternColor(color);
                ogs.SetSurfaceForegroundPatternVisible(true);

                ogs.SetCutForegroundPatternId(solidFillId);
                ogs.SetCutForegroundPatternColor(color);
                ogs.SetCutForegroundPatternVisible(true);
            }
            ogs.SetSurfaceTransparency(trans);

            try
            {
                doc.ActiveView.SetElementOverrides(instance.Id, ogs);
            }
            catch { }

            try
            {
                var subIds = instance.GetSubComponentIds();
                if (subIds != null && subIds.Count > 0)
                {
                    foreach (var sId in subIds)
                    {
                        doc.ActiveView.SetElementOverrides(sId, ogs);
                    }
                }
            }
            catch { }
        }

        private static bool TryAssignMaterialParameter(
            ParameterSet parameterSet, 
            ElementId matId, 
            string preferredName)
        {
            // Tìm theo tên ưu tiên trước
            if (!string.IsNullOrWhiteSpace(preferredName))
            {
                foreach (Parameter p in parameterSet)
                {
                    if (p.IsReadOnly) continue;
                    if (string.Equals(p.Definition?.Name, preferredName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (p.StorageType == StorageType.ElementId)
                        {
                            p.Set(matId);
                            return true;
                        }
                    }
                }
            }

            // Tìm theo nhận diện từ khóa vật liệu
            foreach (Parameter p in parameterSet)
            {
                if (p.IsReadOnly || p.Definition == null) continue;
                string defName = p.Definition.Name.ToUpperInvariant();

                bool isMatType = false;
                try
                {
                    var dt = p.Definition.GetDataType();
                    if (dt == SpecTypeId.Reference.Material) isMatType = true;
                }
                catch { }

                if (isMatType || 
                    defName.Contains("MATERIAL") || 
                    defName.Contains("VẬT LIỆU") || 
                    defName.Contains("VAT LIEU") || 
                    defName.StartsWith("VL_") || 
                    defName.Contains("BE TONG") || 
                    defName.Contains("BÊ TÔNG"))
                {
                    if (p.StorageType == StorageType.ElementId)
                    {
                        p.Set(matId);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
