using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Color = System.Windows.Media.Color;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using InfraBIM.ManholeTool.Models;

namespace InfraBIM.ManholeTool.Services
{
    /// <summary>
    /// Trích xuất hình học 3D thực tế trực tiếp từ Family Revit (Virtual Rollback Transaction)
    /// kết hợp thuật toán tạo Mesh 3D tham số hóa cho Viewport Kiểm Tra Mô Hình
    /// </summary>
    public static class ManholeMeshExtractor
    {
        /// <summary>
        /// Trích xuất Mesh 3D thực tế của FamilySymbol trong Revit mà KHÔNG làm thay đổi bản vẽ
        /// </summary>
        public static ExtractedManholeMeshGroup? ExtractRealGeometryFromRevit(
            Document doc,
            FamilySymbol symbol,
            string heightParamName,
            double heightM)
        {
            if (doc == null || symbol == null) return null;

            var result = new ExtractedManholeMeshGroup
            {
                FamilyName = symbol.FamilyName ?? "Family Hố Ga",
                SymbolName = symbol.Name ?? "Type"
            };

            try
            {
                using var t = new Transaction(doc, "Temp Extract Mesh Virtual");
                t.Start();

                if (!symbol.IsActive) symbol.Activate();

                FamilyInstance? inst = doc.Create.NewFamilyInstance(XYZ.Zero, symbol, StructuralType.NonStructural);
                if (inst != null)
                {
                    if (!string.IsNullOrWhiteSpace(heightParamName) && heightM > 0.1)
                    {
                        double hFeet = UnitUtils.ConvertToInternalUnits(heightM, UnitTypeId.Meters);
                        ManholeCoreEngine.SetParameterValue(inst, heightParamName, hFeet);
                    }
                    doc.Regenerate();

                    var opt = new Options
                    {
                        DetailLevel = ViewDetailLevel.Fine,
                        ComputeReferences = false,
                        IncludeNonVisibleObjects = false
                    };

                    var allInstances = new List<FamilyInstance> { inst };
                    var subIds = inst.GetSubComponentIds();
                    if (subIds != null && subIds.Count > 0)
                    {
                        foreach (var sid in subIds)
                        {
                            if (doc.GetElement(sid) is FamilyInstance sub)
                            {
                                allInstances.Add(sub);
                            }
                        }
                    }

                    foreach (var fi in allInstances)
                    {
                        var geom = fi.get_Geometry(opt);
                        if (geom == null) continue;

                        string categoryName = fi.Symbol?.FamilyName ?? "Cấu kiện";
                        var brush = DetermineBrush(categoryName);
                        ExtractSolidsRecursive(geom, brush, categoryName, result.Parts);
                    }
                }

                t.RollBack(); // Luôn Rollback ngay để không để lại dấu vết
            }
            catch { }

            return result.Parts.Count > 0 ? result : null;
        }

        private static void ExtractSolidsRecursive(
            GeometryElement geomElem, 
            System.Windows.Media.Brush brush, 
            string categoryName,
            List<(MeshGeometry3D Mesh, System.Windows.Media.Brush Brush, string CategoryName)> parts)
        {
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid && solid.Faces.Size > 0 && solid.Volume > 1e-6)
                {
                    var mesh = ConvertSolidToMesh3D(solid);
                    if (mesh != null && mesh.Positions.Count > 0)
                    {
                        parts.Add((mesh, brush, categoryName));
                    }
                }
                else if (obj is GeometryInstance inst)
                {
                    var subGeom = inst.GetInstanceGeometry();
                    if (subGeom != null)
                    {
                        ExtractSolidsRecursive(subGeom, brush, categoryName, parts);
                    }
                }
            }
        }

        private static MeshGeometry3D? ConvertSolidToMesh3D(Solid solid)
        {
            try
            {
                var mesh = new MeshGeometry3D();
                int vertexIndex = 0;

                foreach (Face face in solid.Faces)
                {
                    Mesh trMesh = face.Triangulate(0.5);
                    if (trMesh == null) continue;

                    int numTriangles = trMesh.NumTriangles;
                    for (int i = 0; i < numTriangles; i++)
                    {
                        MeshTriangle triangle = trMesh.get_Triangle(i);
                        XYZ p0 = triangle.get_Vertex(0);
                        XYZ p1 = triangle.get_Vertex(1);
                        XYZ p2 = triangle.get_Vertex(2);

                        // Đổi từ Feet nội bộ sang Mét hiển thị
                        double x0 = UnitUtils.ConvertFromInternalUnits(p0.X, UnitTypeId.Meters);
                        double y0 = UnitUtils.ConvertFromInternalUnits(p0.Y, UnitTypeId.Meters);
                        double z0 = UnitUtils.ConvertFromInternalUnits(p0.Z, UnitTypeId.Meters);

                        double x1 = UnitUtils.ConvertFromInternalUnits(p1.X, UnitTypeId.Meters);
                        double y1 = UnitUtils.ConvertFromInternalUnits(p1.Y, UnitTypeId.Meters);
                        double z1 = UnitUtils.ConvertFromInternalUnits(p1.Z, UnitTypeId.Meters);

                        double x2 = UnitUtils.ConvertFromInternalUnits(p2.X, UnitTypeId.Meters);
                        double y2 = UnitUtils.ConvertFromInternalUnits(p2.Y, UnitTypeId.Meters);
                        double z2 = UnitUtils.ConvertFromInternalUnits(p2.Z, UnitTypeId.Meters);

                        mesh.Positions.Add(new Point3D(x0, z0, -y0)); // Chuyển trục Z lên trên trong WPF
                        mesh.Positions.Add(new Point3D(x1, z1, -y1));
                        mesh.Positions.Add(new Point3D(x2, z2, -y2));

                        mesh.TriangleIndices.Add(vertexIndex);
                        mesh.TriangleIndices.Add(vertexIndex + 1);
                        mesh.TriangleIndices.Add(vertexIndex + 2);

                        vertexIndex += 3;
                    }
                }

                if (mesh.CanFreeze) mesh.Freeze();
                return mesh;
            }
            catch
            {
                return null;
            }
        }

        private static System.Windows.Media.Brush DetermineBrush(string name)
        {
            string u = name.ToUpperInvariant();
            SolidColorBrush b;
            if (u.Contains("NAP") || u.Contains("NẮP") || u.Contains("GANG") || u.Contains("SONG CHAN") || u.Contains("RÁC"))
                b = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)); // Gang đen (#1E293B)
            else if (u.Contains("CO") || u.Contains("CỔ") || u.Contains("THU"))
                b = new SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199)); // Xanh dương (#0284C7)
            else if (u.Contains("LOT") || u.Contains("LÓT") || u.Contains("DA DAM") || u.Contains("ĐỆM"))
                b = new SolidColorBrush(System.Windows.Media.Color.FromRgb(202, 138, 4));  // Đệm (#CA8A04)
            else
                b = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)); // Bê tông thân ga (#64748B)

            if (b.CanFreeze) b.Freeze();
            return b;
        }

        /// <summary>
        /// Tạo Mesh 3D tham số hóa mô phỏng hố thu nước (chuẩn hình học HO THU NUOC DVB_LOAI 1)
        /// gồm: Bản bê tông lót đáy, Thân hố ga BTCT, Cổ thu nước hàm ếch / bó vỉa, Song chắn rác nắp gang, và Đầu chờ ống nối.
        /// </summary>
        public static List<(MeshGeometry3D Mesh, System.Windows.Media.Brush Brush, string PartName)> BuildParametricManholeMeshes(
            double widthM, 
            double lengthM, 
            double heightM, 
            double cdDinh, 
            double cdDay, 
            double gocPvDeg,
            double offsetX = 0.0,
            double offsetZ = 0.0,
            bool isHighlighted = false,
            bool hasWarning = false,
            double cdDayCong = 0.0)
        {
            var parts = new List<(MeshGeometry3D, System.Windows.Media.Brush, string)>();

            double w = Math.Max(0.8, widthM);
            double l = Math.Max(0.8, lengthM);
            double h = Math.Max(0.5, heightM);

            // Bảng màu thông minh thích ứng theo trạng thái Highlight / Cảnh báo / Bình thường
            SolidColorBrush brushConcrete;
            SolidColorBrush brushCollar;
            SolidColorBrush brushCover;
            SolidColorBrush brushBedding;
            SolidColorBrush brushPipe;

            if (hasWarning)
            {
                brushConcrete = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Đỏ cảnh báo #EF4444
                brushCollar = new SolidColorBrush(Color.FromRgb(220, 38, 38));   // Đỏ đậm
                brushCover = new SolidColorBrush(Color.FromRgb(153, 27, 27));    // Đỏ mận
                brushBedding = new SolidColorBrush(Color.FromRgb(185, 28, 28));  // Đỏ gạch
                brushPipe = new SolidColorBrush(Color.FromRgb(248, 113, 113));   // Đỏ hồng
            }
            else if (isHighlighted)
            {
                brushConcrete = new SolidColorBrush(Color.FromRgb(14, 165, 233)); // Xanh Cyan sáng #0EA5E9
                brushCollar = new SolidColorBrush(Color.FromRgb(56, 189, 248));   // Sky Blue #38BDF8
                brushCover = new SolidColorBrush(Color.FromRgb(15, 23, 42));      // Than kim loại #0F172A
                brushBedding = new SolidColorBrush(Color.FromRgb(245, 158, 11));  // Hổ phách viền móng #F59E0B
                brushPipe = new SolidColorBrush(Color.FromRgb(2, 132, 199));      // Xanh ống #0284C7
            }
            else
            {
                brushConcrete = new SolidColorBrush(Color.FromRgb(100, 116, 139)); // Bê tông xi măng chuẩn #64748B
                brushCollar = new SolidColorBrush(Color.FromRgb(51, 65, 85));      // Cổ bê tông tối #334155
                brushCover = new SolidColorBrush(Color.FromRgb(30, 41, 59));       // Nắp gang đen #1E293B
                brushBedding = new SolidColorBrush(Color.FromRgb(202, 138, 4));    // Đệm đá dăm vàng nâu #CA8A04
                brushPipe = new SolidColorBrush(Color.FromRgb(71, 85, 105));       // Ống cống BTCT #475569
            }

            if (brushConcrete.CanFreeze) brushConcrete.Freeze();
            if (brushCollar.CanFreeze) brushCollar.Freeze();
            if (brushCover.CanFreeze) brushCover.Freeze();
            if (brushBedding.CanFreeze) brushBedding.Freeze();
            if (brushPipe.CanFreeze) brushPipe.Freeze();

            // 1. Bê tông lót đáy ga (dày 0.15m, nhô ra 0.1m mỗi bên so với thân ga)
            double tBed = 0.15;
            var meshBed = CreateBoxMesh(w + 0.25, l + 0.25, tBed, offsetX, cdDay - tBed / 2.0, offsetZ);
            parts.Add((meshBed, brushBedding, "Bê tông lót đáy ga"));

            // 2. Thân hố ga chính (từ CĐ Đáy đến CĐ Đỉnh - 0.35m phần cổ ga và miệng thu)
            double hBody = Math.Max(0.25, h - 0.35);
            double yCenterBody = cdDay + hBody / 2.0;
            var meshBody = CreateBoxMesh(w, l, hBody, offsetX, yCenterBody, offsetZ);
            parts.Add((meshBody, brushConcrete, "Thân hố ga BTCT"));

            // 3. Cổ thu nước mưa hàm ếch / bó vỉa (thu hẹp, cao 0.25m)
            double wCollar = Math.Min(w * 0.75, 0.75);
            double lCollar = Math.Min(l * 0.75, 0.75);
            double hCollar = 0.25;
            double yCenterCollar = (cdDinh - 0.08) - hCollar / 2.0;
            var meshCollar = CreateBoxMesh(wCollar, lCollar, hCollar, offsetX, yCenterCollar, offsetZ);
            parts.Add((meshCollar, brushCollar, "Cổ hố thu nước"));

            // 4. Song chắn rác / nắp gang thu nước vỉa hè (đặt tại CĐ Đỉnh, dày 0.06m)
            double hCover = 0.06;
            double yCenterCover = cdDinh - hCover / 2.0;
            var meshCover = CreateBoxMesh(wCollar * 0.85, lCollar * 0.85, hCover, offsetX, yCenterCover, offsetZ);
            parts.Add((meshCover, brushCover, "Song chắn rác / Nắp gang"));

            // 5. Ống cống nối / đầu chờ ngang thoát nước
            double yPipeCenter = cdDayCong > 0.01 ? cdDayCong + 0.3 : cdDay + 0.35;
            double dPipe = Math.Min(0.6, h * 0.45);
            var meshPipeStub = CreateCylinderMesh(dPipe / 2.0, l + 0.5, 12, offsetX, yPipeCenter, offsetZ);
            parts.Add((meshPipeStub, brushPipe, "Ống nối cống"));

            return parts;
        }

        /// <summary>
        /// Tạo Mesh cho toàn bộ tuyến hố ga + chuỗi đường ống cống ngầm nối giữa các hố ga liên tiếp
        /// </summary>
        public static List<(MeshGeometry3D Mesh, System.Windows.Media.Brush Brush, string PartName, ManholeInspectionItem? Item)> BuildPipelineMeshes(
            IList<ManholeInspectionItem> items,
            ManholeInspectionItem? selectedItem,
            out Point3D pipelineCenter,
            out Rect3D pipelineBounds)
        {
            var result = new List<(MeshGeometry3D, System.Windows.Media.Brush, string, ManholeInspectionItem?)>();
            pipelineCenter = new Point3D(0, 0, 0);
            pipelineBounds = Rect3D.Empty;

            if (items == null || items.Count == 0) return result;

            // 1. Tính toán tâm tọa độ để chuẩn hóa về gốc (0,0) chống giật số thực
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            double minZ = double.MaxValue, maxZ = double.MinValue;

            foreach (var item in items)
            {
                if (item.ToaDoX < minX) minX = item.ToaDoX;
                if (item.ToaDoX > maxX) maxX = item.ToaDoX;
                if (item.ToaDoY < minY) minY = item.ToaDoY;
                if (item.ToaDoY > maxY) maxY = item.ToaDoY;
                if (item.CdDay < minZ) minZ = item.CdDay;
                if (item.CdDinh > maxZ) maxZ = item.CdDinh;
            }

            double originX = (minX + maxX) / 2.0;
            double originY = (minY + maxY) / 2.0;
            pipelineCenter = new Point3D(0, (minZ + maxZ) / 2.0, 0);

            var pipeBrushNormal = new SolidColorBrush(Color.FromRgb(71, 85, 105)); // Ống cống bê tông xám #475569
            var pipeBrushSelected = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // Xanh lơ khi liên kết hố đang chọn
            if (pipeBrushNormal.CanFreeze) pipeBrushNormal.Freeze();
            if (pipeBrushSelected.CanFreeze) pipeBrushSelected.Freeze();

            var manholeCenters = new List<Point3D>();

            // 2. Sinh Mesh cho từng hố ga trên tuyến
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                double localX = item.ToaDoX - originX;
                double localZ = -(item.ToaDoY - originY); // Trục CAD Northing Y quy sang -Z trong WPF 3D
                bool isSelected = selectedItem != null && (selectedItem.STT == item.STT && selectedItem.Side == item.Side);

                manholeCenters.Add(new Point3D(localX, item.CdDay, localZ));

                var parts = BuildParametricManholeMeshes(
                    widthM: 1.4,
                    lengthM: 1.4,
                    heightM: item.ChieuCaoThan,
                    cdDinh: item.CdDinh,
                    cdDay: item.CdDay,
                    gocPvDeg: item.GocPv,
                    offsetX: localX,
                    offsetZ: localZ,
                    isHighlighted: isSelected,
                    hasWarning: item.HasWarning,
                    cdDayCong: item.CdDayCong);

                foreach (var p in parts)
                {
                    result.Add((p.Mesh, p.Brush, $"{item.TenHoGa} - {p.PartName}", item));
                }
            }

            // 3. Sinh Mesh các đoạn ống cống nối giữa các hố ga liên tiếp
            for (int i = 0; i < items.Count - 1; i++)
            {
                var itemA = items[i];
                var itemB = items[i + 1];

                double xa = itemA.ToaDoX - originX;
                double za = -(itemA.ToaDoY - originY);
                double ya = itemA.CdDayCong > 0.01 ? itemA.CdDayCong + 0.3 : itemA.CdDay + 0.35;

                double xb = itemB.ToaDoX - originX;
                double zb = -(itemB.ToaDoY - originY);
                double yb = itemB.CdDayCong > 0.01 ? itemB.CdDayCong + 0.3 : itemB.CdDay + 0.35;

                Point3D pA = new Point3D(xa, ya, za);
                Point3D pB = new Point3D(xb, yb, zb);

                double dist = (pB - pA).Length;
                if (dist > 0.1 && dist < 500.0) // Bỏ qua nếu cách nhau quá xa hoặc trùng nhau
                {
                    var pipeMesh = CreatePipeBetweenPoints(pA, pB, radius: 0.3, segments: 12);
                    if (pipeMesh != null && pipeMesh.Positions.Count > 0)
                    {
                        bool isPipeConnectedToSelected = selectedItem != null && 
                            (selectedItem.STT == itemA.STT || selectedItem.STT == itemB.STT);
                        var brush = isPipeConnectedToSelected ? pipeBrushSelected : pipeBrushNormal;
                        result.Add((pipeMesh, brush, $"Ống cống: {itemA.TenHoGa} ➜ {itemB.TenHoGa}", null));
                    }
                }
            }

            // Cập nhật bounding box
            pipelineBounds = new Rect3D(
                minX - originX - 2.0, minZ - 1.0, -(maxY - originY) - 2.0,
                (maxX - minX) + 4.0, (maxZ - minZ) + 2.0, (maxY - minY) + 4.0);

            return result;
        }

        /// <summary>
        /// Tạo Mesh hình trụ tròn đại diện cho ống cống ngầm nối giữa 2 điểm 3D bất kỳ
        /// </summary>
        public static MeshGeometry3D CreatePipeBetweenPoints(Point3D p1, Point3D p2, double radius, int segments = 12)
        {
            var mesh = new MeshGeometry3D();
            Vector3D dir = p2 - p1;
            double len = dir.Length;
            if (len < 0.05) return mesh;

            dir.Normalize();
            Vector3D u = Math.Abs(dir.Y) < 0.9 ? Vector3D.CrossProduct(dir, new Vector3D(0, 1, 0)) : Vector3D.CrossProduct(dir, new Vector3D(1, 0, 0));
            u.Normalize();
            Vector3D v = Vector3D.CrossProduct(dir, u);
            v.Normalize();

            // Tạo các đỉnh vòng tròn tại p1 và p2
            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                Vector3D radial = (u * Math.Cos(angle) + v * Math.Sin(angle)) * radius;
                mesh.Positions.Add(p1 + radial);
                mesh.Positions.Add(p2 + radial);
            }

            // Nối các tam giác thân ống
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int p1_curr = i * 2;
                int p2_curr = i * 2 + 1;
                int p1_next = next * 2;
                int p2_next = next * 2 + 1;

                mesh.TriangleIndices.Add(p1_curr);
                mesh.TriangleIndices.Add(p2_curr);
                mesh.TriangleIndices.Add(p1_next);

                mesh.TriangleIndices.Add(p1_next);
                mesh.TriangleIndices.Add(p2_curr);
                mesh.TriangleIndices.Add(p2_next);
            }

            if (mesh.CanFreeze) mesh.Freeze();
            return mesh;
        }

        public static MeshGeometry3D CreateBoxMesh(double sizeX, double sizeY, double sizeZ, double cx, double cy, double cz)
        {
            var mesh = new MeshGeometry3D();
            double hx = sizeX / 2.0;
            double hy = sizeY / 2.0;
            double hz = sizeZ / 2.0;

            // 8 đỉnh của hình hộp (cy là trục cao độ Z thực tế)
            Point3D[] p = new Point3D[]
            {
                new Point3D(cx - hx, cy - hz, cz - hy), // 0
                new Point3D(cx + hx, cy - hz, cz - hy), // 1
                new Point3D(cx + hx, cy + hz, cz - hy), // 2
                new Point3D(cx - hx, cy + hz, cz - hy), // 3
                new Point3D(cx - hx, cy - hz, cz + hy), // 4
                new Point3D(cx + hx, cy - hz, cz + hy), // 5
                new Point3D(cx + hx, cy + hz, cz + hy), // 6
                new Point3D(cx - hx, cy + hz, cz + hy)  // 7
            };

            int[] indices = new int[]
            {
                // Front
                0, 2, 1, 0, 3, 2,
                // Back
                4, 5, 6, 4, 6, 7,
                // Left
                0, 7, 3, 0, 4, 7,
                // Right
                1, 2, 6, 1, 6, 5,
                // Top
                3, 6, 2, 3, 7, 6,
                // Bottom
                0, 1, 5, 0, 5, 4
            };

            foreach (var pt in p) mesh.Positions.Add(pt);
            foreach (var idx in indices) mesh.TriangleIndices.Add(idx);

            if (mesh.CanFreeze) mesh.Freeze();
            return mesh;
        }

        public static MeshGeometry3D CreateCylinderMesh(double radius, double height, int segments, double cx, double cy, double cz)
        {
            var mesh = new MeshGeometry3D();
            double hh = height / 2.0;

            // Top and bottom center
            Point3D topCenter = new Point3D(cx, cy + hh, cz);
            Point3D botCenter = new Point3D(cx, cy - hh, cz);

            int topCenterIdx = 0;
            int botCenterIdx = 1;
            mesh.Positions.Add(topCenter);
            mesh.Positions.Add(botCenter);

            int baseIdx = 2;
            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                double x = cx + radius * Math.Cos(angle);
                double z = cz + radius * Math.Sin(angle);

                mesh.Positions.Add(new Point3D(x, cy + hh, z)); // top ring (baseIdx + i*2)
                mesh.Positions.Add(new Point3D(x, cy - hh, z)); // bot ring (baseIdx + i*2 + 1)
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int t1 = baseIdx + i * 2;
                int b1 = baseIdx + i * 2 + 1;
                int t2 = baseIdx + next * 2;
                int b2 = baseIdx + next * 2 + 1;

                // Top fan
                mesh.TriangleIndices.Add(topCenterIdx);
                mesh.TriangleIndices.Add(t1);
                mesh.TriangleIndices.Add(t2);

                // Bot fan
                mesh.TriangleIndices.Add(botCenterIdx);
                mesh.TriangleIndices.Add(b2);
                mesh.TriangleIndices.Add(b1);

                // Side quad (2 triangles)
                mesh.TriangleIndices.Add(t1);
                mesh.TriangleIndices.Add(b1);
                mesh.TriangleIndices.Add(t2);

                mesh.TriangleIndices.Add(t2);
                mesh.TriangleIndices.Add(b1);
                mesh.TriangleIndices.Add(b2);
            }

            if (mesh.CanFreeze) mesh.Freeze();
            return mesh;
        }
    }
}
