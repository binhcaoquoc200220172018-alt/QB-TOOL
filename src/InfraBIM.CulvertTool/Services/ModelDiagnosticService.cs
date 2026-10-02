using System;
using System.Collections.Generic;
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
                RunDeepDiagnostic(uiApp.Application, doc);
            }
            catch (Exception ex)
            {
                File.WriteAllText(@"C:\Users\ADMIN\Desktop\DEBUG_REVIT_ERROR.txt", ex.ToString());
            }
        }

        public static void RunDeepDiagnostic(Autodesk.Revit.ApplicationServices.Application app, Document doc)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== INFRA BIM - BÁO CÁO PHÂN TÍCH CHUYÊN SÂU HỆ TỌA ĐỘ VÀ CẤU KIỆN CỐNG ===");
            sb.AppendLine($"Thời gian: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"File Revit hiện tại: {doc.PathName}");
            sb.AppendLine($"Tiêu đề Document: {doc.Title}");

            // 1. Hệ tọa độ của 1.rvt
            sb.AppendLine("\n--- 1. HỆ TỌA ĐỘ 1.RVT ---");
            if (doc.ActiveProjectLocation != null)
            {
                var pos = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
                sb.AppendLine($"Active Project Location: {doc.ActiveProjectLocation.Name}");
                sb.AppendLine($"EastWest (m): {UnitUtils.ConvertFromInternalUnits(pos.EastWest, UnitTypeId.Meters):F4}");
                sb.AppendLine($"NorthSouth (m): {UnitUtils.ConvertFromInternalUnits(pos.NorthSouth, UnitTypeId.Meters):F4}");
                sb.AppendLine($"Elevation (m): {UnitUtils.ConvertFromInternalUnits(pos.Elevation, UnitTypeId.Meters):F4}");
                sb.AppendLine($"Angle (rad): {pos.Angle:F6} (deg: {pos.Angle * 180.0 / Math.PI:F4})");
            }
            var basePoints = new FilteredElementCollector(doc).OfClass(typeof(BasePoint)).Cast<BasePoint>().ToList();
            foreach (var bp in basePoints)
            {
                string bpType = bp.IsShared ? "SURVEY POINT" : "PROJECT BASE POINT";
                XYZ pt = bp.Position;
                sb.AppendLine($"{bpType}: Id={bp.Id}, X={UnitUtils.ConvertFromInternalUnits(pt.X, UnitTypeId.Meters):F4}m, Y={UnitUtils.ConvertFromInternalUnits(pt.Y, UnitTypeId.Meters):F4}m, Z={UnitUtils.ConvertFromInternalUnits(pt.Z, UnitTypeId.Meters):F4}m");
            }

            // 2. Các FamilySymbol cống đã nạp trong 1.rvt kèm toàn bộ Type Parameters
            sb.AppendLine("\n--- 2. DANH SÁCH FAMILY SYMBOLS CỐNG TRONG 1.RVT ---");
            var culvertSymbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(s =>
                {
                    string fn = s.FamilyName.ToUpperInvariant();
                    return fn.Contains("TNN") || fn.Contains("TNM") || fn.Contains("CONG") || fn.Contains("CUA XA") || fn.Contains("HO GA");
                })
                .ToList();

            foreach (var sym in culvertSymbols)
            {
                sb.AppendLine($"\nFamily: {sym.FamilyName} | Type: {sym.Name} | Category: {sym.Category?.Name}");
                sb.AppendLine("   [Type Parameters]:");
                foreach (Parameter p in sym.Parameters)
                {
                    if (p.Definition == null) continue;
                    string val = ParameterToString(p);
                    if (!string.IsNullOrEmpty(val))
                    {
                        sb.AppendLine($"      {p.Definition.Name} = {val} ({p.StorageType})");
                    }
                }
            }

            // 3. Các đối tượng cống đã rải trong 1.rvt kèm BoundingBox và Instance Parameters
            sb.AppendLine("\n--- 3. CÁC ĐỐI TƯỢNG CỐNG ĐÃ RẢI TRONG 1.RVT ---");
            var instancesInCurrent = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi =>
                {
                    string fn = fi.Symbol.FamilyName.ToUpperInvariant();
                    return fn.Contains("TNN") || fn.Contains("TNM") || fn.Contains("CONG") || fn.Contains("CUA XA") || fn.Contains("HO GA");
                })
                .ToList();

            sb.AppendLine($"Tổng số đối tượng: {instancesInCurrent.Count}");
            // Lấy mẫu từng họ Family trong 1.rvt
            var groupedByFam = instancesInCurrent.GroupBy(fi => fi.Symbol.FamilyName);
            foreach (var grp in groupedByFam)
            {
                sb.AppendLine($"\n=== HỌ FAMILY: {grp.Key} (Số lượng: {grp.Count()}) ===");
                var fiSample = grp.First();
                LogInstanceDetails(sb, doc, fiSample);
            }

            // 4. Phân tích mô hình tham khảo 770B
            string refPath = @"C:\Users\ADMIN\Desktop\HTKT_TNN_DUONG VEN BIEN PHU YEN\THAM KHAO\770B-iDECO-CD-PD2-D-M3-CONG_DUC_SAN.rvt";
            if (File.Exists(refPath))
            {
                sb.AppendLine("\n=======================================================");
                sb.AppendLine("--- 4. PHÂN TÍCH MÔ HÌNH THAM KHẢO 770B ---");
                try
                {
                    var opt = new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DoNotDetach };
                    var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(refPath);
                    using var refDoc = app.OpenDocumentFile(modelPath, opt);

                    if (refDoc.ActiveProjectLocation != null)
                    {
                        var pos = refDoc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
                        sb.AppendLine($"Reference Active Project Location: {refDoc.ActiveProjectLocation.Name}");
                        sb.AppendLine($"EastWest (m): {UnitUtils.ConvertFromInternalUnits(pos.EastWest, UnitTypeId.Meters):F4}");
                        sb.AppendLine($"NorthSouth (m): {UnitUtils.ConvertFromInternalUnits(pos.NorthSouth, UnitTypeId.Meters):F4}");
                        sb.AppendLine($"Elevation (m): {UnitUtils.ConvertFromInternalUnits(pos.Elevation, UnitTypeId.Meters):F4}");
                        sb.AppendLine($"Angle (deg): {pos.Angle * 180.0 / Math.PI:F4}");
                    }

                    var refInstances = new FilteredElementCollector(refDoc)
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .Where(fi =>
                        {
                            string fn = fi.Symbol.FamilyName.ToUpperInvariant();
                            return fn.Contains("TNN") || fn.Contains("TNM") || fn.Contains("CONG") || fn.Contains("CUA XA") || fn.Contains("HO GA") || fn.Contains("SAN GIA CO");
                        })
                        .ToList();

                    sb.AppendLine($"Tổng số đối tượng cống trong file tham khảo: {refInstances.Count}");

                    // Nhóm theo FamilyName
                    var refGrouped = refInstances.GroupBy(fi => fi.Symbol.FamilyName);
                    foreach (var grp in refGrouped)
                    {
                        sb.AppendLine($"\n>>> THAM KHẢO HỌ: {grp.Key} (Tổng: {grp.Count()}) <<<");
                        // Lấy 2 mẫu đầu tiên
                        foreach (var fi in grp.Take(2))
                        {
                            LogInstanceDetails(sb, refDoc, fi);
                        }
                    }

                    refDoc.Close(false);
                }
                catch (Exception exRef)
                {
                    sb.AppendLine($"Lỗi khi phân tích file tham khảo: {exRef.Message}");
                }
            }

            string outPath = @"C:\Users\ADMIN\Desktop\DEBUG_DEEP_ANALYSIS.txt";
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }

        private static void LogInstanceDetails(StringBuilder sb, Document doc, FamilyInstance fi)
        {
            string famName = fi.Symbol.FamilyName;
            string typeName = fi.Name;
            string catName = fi.Category?.Name ?? "N/A";
            XYZ loc = (fi.Location as LocationPoint)?.Point ?? XYZ.Zero;
            double locX_M = UnitUtils.ConvertFromInternalUnits(loc.X, UnitTypeId.Meters);
            double locY_M = UnitUtils.ConvertFromInternalUnits(loc.Y, UnitTypeId.Meters);
            double locZ_M = UnitUtils.ConvertFromInternalUnits(loc.Z, UnitTypeId.Meters);
            double rotDeg = (fi.Location is LocationPoint lp) ? (lp.Rotation * 180.0 / Math.PI) : 0.0;
            XYZ facing = fi.FacingOrientation;
            XYZ hand = fi.HandOrientation;

            var bbox = fi.get_BoundingBox(null);
            string bboxStr = "N/A";
            if (bbox != null)
            {
                double dx = UnitUtils.ConvertFromInternalUnits(bbox.Max.X - bbox.Min.X, UnitTypeId.Meters);
                double dy = UnitUtils.ConvertFromInternalUnits(bbox.Max.Y - bbox.Min.Y, UnitTypeId.Meters);
                double dz = UnitUtils.ConvertFromInternalUnits(bbox.Max.Z - bbox.Min.Z, UnitTypeId.Meters);
                bboxStr = $"Size(Dx={dx:F3}m, Dy={dy:F3}m, Dz={dz:F3}m)";
            }

            sb.AppendLine($"   ID: {fi.Id.Value} | {famName} | Type: {typeName} | Cat: {catName}");
            sb.AppendLine($"      Pos(m): ({locX_M:F3}, {locY_M:F3}, {locZ_M:F3}) | Rot: {rotDeg:F2}° | {bboxStr}");
            sb.AppendLine($"      Facing: ({facing.X:F3}, {facing.Y:F3}, {facing.Z:F3}) | Hand: ({hand.X:F3}, {hand.Y:F3}, {hand.Z:F3})");

            // Instance Parameters quan trọng
            sb.AppendLine("      [Instance Parameters]:");
            foreach (Parameter p in fi.Parameters)
            {
                if (p.Definition == null || p.IsReadOnly) continue;
                string val = ParameterToString(p);
                if (!string.IsNullOrEmpty(val))
                {
                    sb.AppendLine($"         {p.Definition.Name} = {val}");
                }
            }
        }

        private static string ParameterToString(Parameter p)
        {
            try
            {
                switch (p.StorageType)
                {
                    case StorageType.Double:
                        return $"{UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Meters):F4}m (raw: {p.AsDouble():F4})";
                    case StorageType.Integer:
                        return p.AsInteger().ToString();
                    case StorageType.String:
                        return p.AsString();
                    case StorageType.ElementId:
                        return p.AsElementId().Value.ToString();
                    default:
                        return p.AsValueString();
                }
            }
            catch
            {
                return "";
            }
        }
    }
}
