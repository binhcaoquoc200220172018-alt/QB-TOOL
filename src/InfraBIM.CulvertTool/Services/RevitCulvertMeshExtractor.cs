using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Autodesk.Revit.DB;
using InfraBIM.CulvertTool.Models;

namespace InfraBIM.CulvertTool.Services
{
    /// <summary>
    /// Trích xuất hình học thực (3D Solids / Meshes) trực tiếp từ các FamilyInstance của cống trong Revit Document
    /// Giúp cửa sổ Review 3D hiển thị 100% hình học thực tế của Family người dùng vừa xuất ra
    /// </summary>
    public static class RevitCulvertMeshExtractor
    {
        public static bool TryExtractRevitGeometry(
            Document? doc,
            CulvertRowData? row,
            out List<(MeshGeometry3D Mesh, Brush Brush)> result)
        {
            return TryExtractRevitGeometry(doc, row, true, out result);
        }

        public static bool TryExtractRevitGeometry(
            Document? doc,
            CulvertRowData? row,
            bool useSurveyPoint,
            out List<(MeshGeometry3D Mesh, Brush Brush)> result)
        {
            result = new List<(MeshGeometry3D, Brush)>();
            if (doc == null || row == null) return false;

            // Thử với hệ tọa độ được chỉ định
            if (ExtractGeometryInternal(doc, row, useSurveyPoint, result) && result.Count > 0)
            {
                return true;
            }

            // Fallback nếu chưa tìm thấy: thử với hệ tọa độ đảo lại (Survey Point / Project Base Point)
            if (ExtractGeometryInternal(doc, row, !useSurveyPoint, result) && result.Count > 0)
            {
                return true;
            }

            return false;
        }

        private static bool ExtractGeometryInternal(
            Document doc,
            CulvertRowData row,
            bool useSurveyPoint,
            List<(MeshGeometry3D Mesh, Brush Brush)> result)
        {
            try
            {
                // 1. Lấy tọa độ P1, P2 trong Revit Internal Units (Feet)
                XYZ p1 = CoordinateService.ConvertVN2000ToRevitInternal(doc, row.X1, row.Y1, row.Z1, useSurveyPoint);
                XYZ p2 = CoordinateService.ConvertVN2000ToRevitInternal(doc, row.X2, row.Y2, row.Z2, useSurveyPoint);
                XYZ v = p2 - p1;
                double lenFeet = v.GetLength();
                if (lenFeet < 0.1) return false;

                XYZ u = v.Normalize();
                double rot = Math.Atan2(u.Y, u.X);
                double cosRot = Math.Cos(rot);
                double sinRot = Math.Sin(rot);

                // 2. Tìm kiếm các FamilyInstance nằm trong vùng không gian cống này
                double searchRadiusFeet = UnitUtils.ConvertToInternalUnits(25.0, UnitTypeId.Meters);
                XYZ minBbox = new XYZ(
                    Math.Min(p1.X, p2.X) - searchRadiusFeet,
                    Math.Min(p1.Y, p2.Y) - searchRadiusFeet,
                    Math.Min(p1.Z, p2.Z) - searchRadiusFeet);
                XYZ maxBbox = new XYZ(
                    Math.Max(p1.X, p2.X) + searchRadiusFeet,
                    Math.Max(p1.Y, p2.Y) + searchRadiusFeet,
                    Math.Max(p1.Z, p2.Z) + searchRadiusFeet);

                var outline = new Outline(minBbox, maxBbox);
                var filter = new BoundingBoxIntersectsFilter(outline);

                var instances = new FilteredElementCollector(doc)
                    .WherePasses(filter)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .ToList();

                // Fallback nếu BoundingBoxIntersectsFilter không lấy được: quét toàn bộ FamilyInstance có tên liên quan
                if (instances.Count == 0)
                {
                    instances = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .Where(i => IsCulvertFamily(i.Symbol?.FamilyName ?? ""))
                        .ToList();
                }

                if (instances.Count == 0) return false;

                // 3. Lọc các FamilyInstance cấu kiện liên quan đến cống
                var culvertInstances = new List<FamilyInstance>();
                foreach (var inst in instances)
                {
                    string fn = inst.Symbol?.FamilyName ?? "";
                    if (!IsCulvertFamily(fn)) continue;

                    // Kiểm tra vị trí nằm gần tim cống P1-P2
                    if (inst.Location is LocationPoint lp)
                    {
                        double distFeet = DistancePointToSegment(lp.Point, p1, p2);
                        if (distFeet > searchRadiusFeet) continue;
                    }
                    culvertInstances.Add(inst);
                }

                if (culvertInstances.Count == 0) return false;

                var opt = new Options
                {
                    DetailLevel = ViewDetailLevel.Fine,
                    ComputeReferences = false,
                    IncludeNonVisibleObjects = false
                };

                foreach (var inst in culvertInstances)
                {
                    var geom = inst.get_Geometry(opt);
                    if (geom == null) continue;

                    Brush brush = GetBrushForFamily(inst.Symbol?.FamilyName ?? "");
                    ExtractSolidsRecursive(geom, p1, cosRot, sinRot, brush, result);
                }

                return result.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsCulvertFamily(string fn)
        {
            string upper = fn.ToUpperInvariant();
            return upper.Contains("TNN") || upper.Contains("CONG") || upper.Contains("CỐNG") ||
                   upper.Contains("THAN") || upper.Contains("THÂN") || upper.Contains("CUA XA") || upper.Contains("CỬA XẢ") ||
                   upper.Contains("HOP NOI") || upper.Contains("HỘP NỐI") || upper.Contains("HO GA") || upper.Contains("HỐ GA") ||
                   upper.Contains("SGC") || upper.Contains("SAN GIA CO") || upper.Contains("SÂN GIA CỐ") ||
                   upper.Contains("BT LOT") || upper.Contains("DA DAM") || upper.Contains("BTL") || upper.Contains("DDD");
        }

        private static double DistancePointToSegment(XYZ pt, XYZ p1, XYZ p2)
        {
            XYZ v = p2 - p1;
            double lenSq = v.DotProduct(v);
            if (lenSq < 1e-6) return pt.DistanceTo(p1);

            double t = Math.Clamp((pt - p1).DotProduct(v) / lenSq, 0.0, 1.0);
            XYZ proj = p1 + t * v;
            return pt.DistanceTo(proj);
        }

        private static Brush GetBrushForFamily(string fn)
        {
            string upper = fn.ToUpperInvariant();
            if (upper.Contains("THAN CONG") || upper.Contains("THÂN CỐNG") || upper.Contains("THAN_CONG") || upper.Contains("DOT") || upper.Contains("ĐỐT"))
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 68, 78)); // Bê tông xám than đậm (#3F444E) như Hình 2
            if (upper.Contains("HOP NOI") || upper.Contains("HỘP NỐI") || upper.Contains("HO GA") || upper.Contains("HỐ GA") || upper.Contains("HN"))
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199)); // Xanh dương (#0284C7) như Hình 2
            if (upper.Contains("CUA XA") || upper.Contains("CỬA XẢ") || upper.Contains("CX"))
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199)); // Xanh dương (#0284C7) như Hình 2
            if (upper.Contains("SAN GIA CO") || upper.Contains("SÂN GIA CỐ") || upper.Contains("SGC"))
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199)); // Xanh dương (#0284C7) như Hình 2
            if (upper.Contains("BT LOT") || upper.Contains("BTL") || upper.Contains("BE TONG LOT"))
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)); // Bê tông lót (#94A3B8)
            if (upper.Contains("DA DAM") || upper.Contains("DDD") || upper.Contains("ĐÁ DĂM"))
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(202, 138, 4));  // Đá dăm đệm (#CA8A04)
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184));
        }

        private static void ExtractSolidsRecursive(
            GeometryElement geomElem,
            XYZ p1,
            double cosRot,
            double sinRot,
            Brush brush,
            List<(MeshGeometry3D, Brush)> list)
        {
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid && solid.Volume > 1e-6)
                {
                    var mesh = ConvertSolidToLocalMesh(solid, p1, cosRot, sinRot);
                    if (mesh != null && mesh.Positions.Count > 0)
                    {
                        list.Add((mesh, brush));
                    }
                }
                else if (obj is GeometryInstance gi)
                {
                    var instGeom = gi.GetInstanceGeometry();
                    if (instGeom != null)
                    {
                        ExtractSolidsRecursive(instGeom, p1, cosRot, sinRot, brush, list);
                    }
                }
            }
        }

        private static MeshGeometry3D? ConvertSolidToLocalMesh(
            Solid solid,
            XYZ p1,
            double cosRot,
            double sinRot)
        {
            var mesh = new MeshGeometry3D();
            const double ft2m = 0.3048;

            foreach (Face face in solid.Faces)
            {
                var triangulated = face.Triangulate();
                if (triangulated == null) continue;

                for (int i = 0; i < triangulated.NumTriangles; i++)
                {
                    var tri = triangulated.get_Triangle(i);
                    XYZ v0 = tri.get_Vertex(0);
                    XYZ v1 = tri.get_Vertex(1);
                    XYZ v2 = tri.get_Vertex(2);

                    Point3D pA = WorldToLocal(v0, p1, cosRot, sinRot, ft2m);
                    Point3D pB = WorldToLocal(v1, p1, cosRot, sinRot, ft2m);
                    Point3D pC = WorldToLocal(v2, p1, cosRot, sinRot, ft2m);

                    int idx = mesh.Positions.Count;
                    mesh.Positions.Add(pA);
                    mesh.Positions.Add(pB);
                    mesh.Positions.Add(pC);

                    Vector3D normal = Vector3D.CrossProduct(pB - pA, pC - pA);
                    if (normal.LengthSquared > 1e-6) normal.Normalize();
                    else normal = new Vector3D(0, 1, 0);

                    mesh.Normals.Add(normal);
                    mesh.Normals.Add(normal);
                    mesh.Normals.Add(normal);

                    mesh.TriangleIndices.Add(idx);
                    mesh.TriangleIndices.Add(idx + 1);
                    mesh.TriangleIndices.Add(idx + 2);
                }
            }

            return mesh;
        }

        private static Point3D WorldToLocal(XYZ pt, XYZ p1, double cosRot, double sinRot, double ft2m)
        {
            double dx = pt.X - p1.X;
            double dy = pt.Y - p1.Y;
            double dz = pt.Z - p1.Z;

            // X: Dọc tuyến từ P1 -> P2 (mét)
            // Z: Ngang tim cống (mét)
            // Y: Cao độ thẳng đứng tương đối (mét)
            double locX = (dx * cosRot + dy * sinRot) * ft2m;
            double locZ = (-dx * sinRot + dy * cosRot) * ft2m;
            double locY = dz * ft2m;

            return new Point3D(locX, locY, locZ);
        }
    }
}
