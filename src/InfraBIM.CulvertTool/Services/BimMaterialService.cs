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
        public static List<string> GetAllMaterialNames(Document doc)
        {
            var names = new List<string>();
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

            // Tìm material có sẵn
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => string.Equals(m.Name, materialName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                try
                {
                    // Cập nhật lại màu sắc nếu cần
                    existing.Color = new Color(r, g, b);
                    int transInt = Math.Clamp((int)transparency, 0, 100);
                    existing.Transparency = transInt;
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
                        newMat.Color = new Color(r, g, b);
                        int transInt = Math.Clamp((int)transparency, 0, 100);
                        newMat.Transparency = transInt;
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
        /// Gán Material vào đối tượng FamilyInstance vừa được đặt trong mô hình
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

            return assigned;
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
