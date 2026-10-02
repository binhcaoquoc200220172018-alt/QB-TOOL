using System;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace InfraBIM.CulvertTool.Services
{
    public static class ModelDiagnosticService
    {
        public static void RunDiagnostic(UIApplication uiApp)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var sb = new StringBuilder();

                sb.AppendLine("=== INFRA BIM - BÁO CÁO CHẨN ĐOÁN HỆ TỌA ĐỘ VÀ CẤU KIỆN TRONG 1.RVT ===");
                sb.AppendLine($"Thời gian: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"File Revit hiện tại: {doc.PathName}");
                sb.AppendLine($"Tiêu đề Document: {doc.Title}");

                // 1. Hệ tọa độ
                sb.AppendLine("\n--- 1. HỆ TỌA ĐỘ DỰ ÁN ---");
                if (doc.ActiveProjectLocation != null)
                {
                    var pos = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
                    sb.AppendLine($"Active Project Location: {doc.ActiveProjectLocation.Name}");
                    sb.AppendLine($"EastWest (m): {UnitUtils.ConvertFromInternalUnits(pos.EastWest, UnitTypeId.Meters):F4}");
                    sb.AppendLine($"NorthSouth (m): {UnitUtils.ConvertFromInternalUnits(pos.NorthSouth, UnitTypeId.Meters):F4}");
                    sb.AppendLine($"Elevation (m): {UnitUtils.ConvertFromInternalUnits(pos.Elevation, UnitTypeId.Meters):F4}");
                    sb.AppendLine($"Angle (rad): {pos.Angle:F6} (deg: {pos.Angle * 180.0 / Math.PI:F4})");
                }
                else
                {
                    sb.AppendLine("Không tìm thấy ActiveProjectLocation!");
                }

                // Base points
                var basePoints = new FilteredElementCollector(doc).OfClass(typeof(BasePoint)).Cast<BasePoint>().ToList();
                foreach (var bp in basePoints)
                {
                    string bpType = bp.IsShared ? "SURVEY POINT" : "PROJECT BASE POINT";
                    XYZ pt = bp.Position;
                    sb.AppendLine($"{bpType}: Id={bp.Id}, X={UnitUtils.ConvertFromInternalUnits(pt.X, UnitTypeId.Meters):F4}m, Y={UnitUtils.ConvertFromInternalUnits(pt.Y, UnitTypeId.Meters):F4}m, Z={UnitUtils.ConvertFromInternalUnits(pt.Z, UnitTypeId.Meters):F4}m");
                }

                // 2. Danh sách đối tượng cống đã được tạo trong mô hình 1.rvt
                sb.AppendLine("\n--- 2. CÁC ĐỐI TƯỢNG CỐNG TRONG MÔ HÌNH HIỆN TẠI (1.RVT) ---");
                var culvertInstances = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(fi =>
                    {
                        string fn = fi.Symbol.FamilyName.ToUpperInvariant();
                        return fn.Contains("TNN") || fn.Contains("TNM") || fn.Contains("CUA XA") || fn.Contains("THAN CONG") || fn.Contains("HO GA") || fn.Contains("CONG");
                    })
                    .ToList();

                sb.AppendLine($"Tổng số đối tượng cống tìm thấy: {culvertInstances.Count}");
                foreach (var fi in culvertInstances)
                {
                    string famName = fi.Symbol.FamilyName;
                    string typeName = fi.Name;
                    XYZ loc = (fi.Location as LocationPoint)?.Point ?? XYZ.Zero;
                    double locX_M = UnitUtils.ConvertFromInternalUnits(loc.X, UnitTypeId.Meters);
                    double locY_M = UnitUtils.ConvertFromInternalUnits(loc.Y, UnitTypeId.Meters);
                    double locZ_M = UnitUtils.ConvertFromInternalUnits(loc.Z, UnitTypeId.Meters);

                    double rotDeg = 0.0;
                    if (fi.Location is LocationPoint lp)
                    {
                        rotDeg = lp.Rotation * 180.0 / Math.PI;
                    }

                    XYZ facing = fi.FacingOrientation;
                    XYZ hand = fi.HandOrientation;

                    // Tọa độ thực VN2000
                    var (vnX, vnY, vnZ) = CoordinateService.ConvertRevitInternalToVN2000(doc, loc, true);

                    sb.AppendLine($"ID: {fi.Id.Value} | Family: {famName} | Type: {typeName}");
                    sb.AppendLine($"   Internal(m): ({locX_M:F3}, {locY_M:F3}, {locZ_M:F3}) | VN2000: ({vnX:F3}, {vnY:F3}, {vnZ:F3})");
                    sb.AppendLine($"   Rot: {rotDeg:F2}° | Facing: ({facing.X:F2}, {facing.Y:F2}, {facing.Z:F2}) | Hand: ({hand.X:F2}, {hand.Y:F2}, {hand.Z:F2})");
                    
                    // Các tham số kích thước
                    var pL = fi.LookupParameter("L") ?? fi.LookupParameter("Length") ?? fi.LookupParameter("ChieuDai") ?? fi.LookupParameter("Chiều dài");
                    if (pL != null && pL.StorageType == StorageType.Double)
                    {
                        sb.AppendLine($"   Param L (m): {UnitUtils.ConvertFromInternalUnits(pL.AsDouble(), UnitTypeId.Meters):F3}");
                    }
                }

                // Ghi ra file
                string outPath = @"C:\Users\ADMIN\Desktop\DEBUG_REVIT_MODEL.txt";
                File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);

                // 3. Phân tích mô hình tham khảo
                string refPath = @"C:\Users\ADMIN\Desktop\HTKT_TNN_DUONG VEN BIEN PHU YEN\THAM KHAO\770B-iDECO-CD-PD2-D-M3-CONG_DUC_SAN.rvt";
                if (File.Exists(refPath))
                {
                    InspectReferenceModel(uiApp.Application, refPath);
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText(@"C:\Users\ADMIN\Desktop\DEBUG_REVIT_ERROR.txt", ex.ToString());
            }
        }

        private static void InspectReferenceModel(Autodesk.Revit.ApplicationServices.Application app, string refPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PHÂN TÍCH MÔ HÌNH THAM KHẢO 770B-iDECO-CD-PD2-D-M3-CONG_DUC_SAN.rvt ===");
            try
            {
                var opt = new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DoNotDetach };
                var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(refPath);
                using var refDoc = app.OpenDocumentFile(modelPath, opt);

                // Hệ tọa độ
                if (refDoc.ActiveProjectLocation != null)
                {
                    var pos = refDoc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
                    sb.AppendLine($"Active Project Location: {refDoc.ActiveProjectLocation.Name}");
                    sb.AppendLine($"EastWest (m): {UnitUtils.ConvertFromInternalUnits(pos.EastWest, UnitTypeId.Meters):F4}");
                    sb.AppendLine($"NorthSouth (m): {UnitUtils.ConvertFromInternalUnits(pos.NorthSouth, UnitTypeId.Meters):F4}");
                    sb.AppendLine($"Elevation (m): {UnitUtils.ConvertFromInternalUnits(pos.Elevation, UnitTypeId.Meters):F4}");
                    sb.AppendLine($"Angle (deg): {pos.Angle * 180.0 / Math.PI:F4}");
                }

                var basePoints = new FilteredElementCollector(refDoc).OfClass(typeof(BasePoint)).Cast<BasePoint>().ToList();
                foreach (var bp in basePoints)
                {
                    string bpType = bp.IsShared ? "SURVEY POINT" : "PROJECT BASE POINT";
                    XYZ pt = bp.Position;
                    sb.AppendLine($"{bpType}: Id={bp.Id}, X={UnitUtils.ConvertFromInternalUnits(pt.X, UnitTypeId.Meters):F4}m, Y={UnitUtils.ConvertFromInternalUnits(pt.Y, UnitTypeId.Meters):F4}m, Z={UnitUtils.ConvertFromInternalUnits(pt.Z, UnitTypeId.Meters):F4}m");
                }

                // Quét mẫu 1 cụm cống trong file tham khảo
                var refCulverts = new FilteredElementCollector(refDoc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(fi =>
                    {
                        string fn = fi.Symbol.FamilyName.ToUpperInvariant();
                        return fn.Contains("TNN") || fn.Contains("TNM") || fn.Contains("CUA XA") || fn.Contains("THAN CONG") || fn.Contains("HO GA") || fn.Contains("CONG");
                    })
                    .Take(40)
                    .ToList();

                sb.AppendLine($"\nTìm thấy {refCulverts.Count} đối tượng cống mẫu trong file tham khảo (lấy tối đa 40 mẫu):");
                foreach (var fi in refCulverts)
                {
                    string famName = fi.Symbol.FamilyName;
                    string typeName = fi.Name;
                    XYZ loc = (fi.Location as LocationPoint)?.Point ?? XYZ.Zero;
                    double locX_M = UnitUtils.ConvertFromInternalUnits(loc.X, UnitTypeId.Meters);
                    double locY_M = UnitUtils.ConvertFromInternalUnits(loc.Y, UnitTypeId.Meters);
                    double locZ_M = UnitUtils.ConvertFromInternalUnits(loc.Z, UnitTypeId.Meters);
                    double rotDeg = (fi.Location is LocationPoint lp) ? (lp.Rotation * 180.0 / Math.PI) : 0.0;
                    XYZ facing = fi.FacingOrientation;
                    XYZ hand = fi.HandOrientation;

                    sb.AppendLine($"ID: {fi.Id.Value} | Family: {famName} | Type: {typeName}");
                    sb.AppendLine($"   Internal(m): ({locX_M:F3}, {locY_M:F3}, {locZ_M:F3}) | Rot: {rotDeg:F2}°");
                    sb.AppendLine($"   Facing: ({facing.X:F2}, {facing.Y:F2}, {facing.Z:F2}) | Hand: ({hand.X:F2}, {hand.Y:F2}, {hand.Z:F2})");
                }

                refDoc.Close(false);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Lỗi khi đọc file tham khảo: {ex.Message}");
            }

            File.WriteAllText(@"C:\Users\ADMIN\Desktop\DEBUG_REFERENCE_MODEL.txt", sb.ToString(), Encoding.UTF8);
        }
    }
}
