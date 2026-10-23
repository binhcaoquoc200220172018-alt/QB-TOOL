using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Autodesk.Revit.DB;
using InfraBIM.CulvertTool.Models;
using InfraBIM.CulvertTool.ViewModels;

namespace InfraBIM.CulvertTool.Services
{
    /// <summary>
    /// Trích xuất hình học thực (3D Solids / Meshes) trực tiếp từ các FamilyInstance của cống trong Revit Document
    /// Giúp cửa sổ Review 3D hiển thị 100% hình học thực tế của Family người dùng vừa xuất ra
    /// </summary>
    public static class RevitCulvertMeshExtractor
    {
        /// <summary>
        /// Trích xuất hình học 3D thực tế trực tiếp từ Family Revit bằng giao dịch ảo (Virtual Rollback Transaction).
        /// Cho phép người dùng xem trước chính xác 100% hình dạng hình học cống đã vẽ trong Family
        /// ngay sau khi nạp Family và TRƯỚC KHI bấm chạy mô hình cống trong Revit.
        /// </summary>
        public static bool TryExtractPreviewGeometry(
            Document? doc,
            CulvertRowData? row,
            MainViewModel? vm,
            out List<(MeshGeometry3D Mesh, Brush Brush)> result)
        {
            result = new List<(MeshGeometry3D, Brush)>();
            if (doc == null || row == null || vm == null) return false;

            try
            {
                // 1. Chuẩn bị dữ liệu cống và tọa độ đánh giá (Clone row để bảo vệ dữ liệu gốc không bị biến đổi)
                bool useSurveyPoint = (vm.SelectedCoordSysIndex == 0);
                CulvertRowData evalRow = new CulvertRowData
                {
                    STT = row.STT,
                    LyTrinh = row.LyTrinh,
                    LoaiCong = row.LoaiCong,
                    CauKien = row.CauKien,
                    KhauDo = row.KhauDo,
                    ChieuDai = row.ChieuDai,
                    SoCua = row.SoCua,
                    DoDoc = row.DoDoc,
                    Z1 = row.Z1,
                    Z2 = row.Z2,
                    L_Ngam_San = row.L_Ngam_San,
                    L_SGC_TL = row.L_SGC_TL,
                    L_SGC_HL = row.L_SGC_HL,
                    SoHopNoi = row.SoHopNoi,
                    Dist_HN1 = row.Dist_HN1,
                    Dist_HN2 = row.Dist_HN2,
                    B_HT1 = row.B_HT1,
                    B_HT2 = row.B_HT2,
                    GhiChu = row.GhiChu,
                    X1 = row.X1,
                    Y1 = row.Y1,
                    X2 = row.X2,
                    Y2 = row.Y2,
                    AssignedProfileName = row.AssignedProfileName,
                    ActiveParamName = row.ActiveParamName,
                    ActiveParamValue = row.ActiveParamValue,
                    ParameterOverrides = row.ParameterOverrides != null ? new Dictionary<string, string>(row.ParameterOverrides, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                };

                bool isRowTechnical = row.IsTechnical || (vm.IsTechnicalCulvertMode && string.IsNullOrEmpty(row.AssignedProfileName) && string.IsNullOrEmpty(row.LoaiCong));
                if (isRowTechnical)
                {
                    evalRow.LoaiCong = "CỐNG KỸ THUẬT";
                    evalRow.GhiChu = "CỐNG KỸ THUẬT";
                }
                XYZ p1 = CoordinateService.ConvertVN2000ToRevitInternal(doc, evalRow.X1, evalRow.Y1, evalRow.Z1, useSurveyPoint);
                XYZ p2 = CoordinateService.ConvertVN2000ToRevitInternal(doc, evalRow.X2, evalRow.Y2, evalRow.Z2, useSurveyPoint);
                if (p1.DistanceTo(p2) < 0.1)
                {
                    double lenM = evalRow.ChieuDai > 0.5 ? evalRow.ChieuDai : 10.0;
                    evalRow = new CulvertRowData
                    {
                        STT = row.STT,
                        LyTrinh = row.LyTrinh,
                        LoaiCong = vm.IsTechnicalCulvertMode ? "CỐNG KỸ THUẬT" : row.LoaiCong,
                        CauKien = row.CauKien,
                        KhauDo = row.KhauDo,
                        ChieuDai = lenM,
                        SoCua = row.SoCua,
                        DoDoc = row.DoDoc,
                        Z1 = row.Z1,
                        Z2 = row.Z2,
                        L_Ngam_San = row.L_Ngam_San,
                        L_SGC_TL = row.L_SGC_TL,
                        L_SGC_HL = row.L_SGC_HL,
                        SoHopNoi = row.SoHopNoi,
                        Dist_HN1 = row.Dist_HN1,
                        Dist_HN2 = row.Dist_HN2,
                        B_HT1 = row.B_HT1,
                        B_HT2 = row.B_HT2,
                        GhiChu = vm.IsTechnicalCulvertMode ? "CỐNG KỸ THUẬT" : row.GhiChu,
                        X1 = 0,
                        Y1 = 0,
                        X2 = lenM,
                        Y2 = 0,
                        ParameterOverrides = row.ParameterOverrides
                    };
                    p1 = CoordinateService.ConvertVN2000ToRevitInternal(doc, evalRow.X1, evalRow.Y1, evalRow.Z1, useSurveyPoint);
                    p2 = CoordinateService.ConvertVN2000ToRevitInternal(doc, evalRow.X2, evalRow.Y2, evalRow.Z2, useSurveyPoint);
                }

                XYZ v = p2 - p1;
                XYZ u = v.Normalize();
                double rot = Math.Atan2(u.Y, u.X);
                double cosRot = Math.Cos(rot);
                double sinRot = Math.Sin(rot);

                // 2. Chạy trong một Transaction tạm thời để Revit dựng đầy đủ hình học, tham số & Cut Geometry
                // Sau khi trích xuất xong mesh, RollBack() ngay lập tức để giữ nguyên trạng thái tài liệu Revit
                using (var t = new Transaction(doc, "Temp Virtual Preview Extraction"))
                {
                    var failOpts = t.GetFailureHandlingOptions();
                    failOpts.SetFailuresPreprocessor(new SuppressAllWarningsFailuresPreprocessor());
                    failOpts.SetClearAfterRollback(true);
                    t.SetFailureHandlingOptions(failOpts);

                    t.Start();
                    try
                    {
                        bool isRowCastInPlace = vm.IsCastInPlaceMode ||
                            (evalRow.SoCua > 1) ||
                            (evalRow.CauKien?.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (evalRow.CauKien?.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (evalRow.LoaiCong?.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (evalRow.LoaiCong?.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (evalRow.GhiChu?.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (evalRow.GhiChu?.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) >= 0);

                        var barrelSource = (isRowTechnical && vm.TechnicalBarrelComponents.Count > 0)
                            ? vm.TechnicalBarrelComponents.ToList()
                            : (isRowCastInPlace && vm.CastInPlaceBarrelComponents.Count > 0)
                                ? vm.CastInPlaceBarrelComponents.ToList()
                                : vm.BarrelComponents.ToList();
                        var manholeSource = (isRowTechnical && vm.TechnicalManholeComponents.Count > 0)
                            ? vm.TechnicalManholeComponents.ToList()
                            : vm.ManholeComponents.ToList();

                        var curBarrel = CulvertCoreEngine.ResolveComponentsForCulvert(doc, barrelSource, evalRow);
                        var curOutlet = CulvertCoreEngine.ResolveComponentsForCulvert(doc, vm.OutletComponents.ToList(), evalRow);
                        var curApron = CulvertCoreEngine.ResolveComponentsForCulvert(doc, vm.ApronComponents.ToList(), evalRow);
                        var curManhole = CulvertCoreEngine.ResolveComponentsForCulvert(doc, manholeSource, evalRow);

                        var outCreatedOutlets = new List<FamilyInstance>();
                        var outCreatedBarrels = new List<FamilyInstance>();
                        var outCreatedBeddings = new List<FamilyInstance>();
                        var outCreatedAprons = new List<FamilyInstance>();
                        var outCreatedManholes = new List<FamilyInstance>();
                        var rowUpOutlets = new List<FamilyInstance>();
                        var rowDnOutlets = new List<FamilyInstance>();
                        var rowCipDetails = new CulvertCoreEngine.CastInPlaceDetails();

                        var curArrayMode = (vm.SelectedArrayModeIndex == 0) ? CulvertArrayMode.CenterOut : CulvertArrayMode.OneWay;
                        double curLStd = isRowCastInPlace ? vm.L_Std_CastInPlace : vm.L_Std;
                        double curKheHo = isRowCastInPlace ? vm.Khe_Ho_CastInPlace : vm.Khe_Ho;

                        CulvertCoreEngine.BuildSingleCulvertV2(
                            doc,
                            evalRow,
                            vm.BimConfig,
                            vm.CustomBimParameters,
                            vm.ParameterMappings,
                            curBarrel,
                            curOutlet,
                            curApron,
                            curManhole,
                            curLStd,
                            curKheHo,
                            vm.B_Box,
                            vm.KhoangCachTimDefault,
                            curArrayMode,
                            isRowCastInPlace,
                            useSurveyPoint,
                            vm.ComponentMaterials,
                            outCreatedOutlets,
                            outCreatedBarrels,
                            outCreatedBeddings,
                            outCreatedAprons,
                            outCreatedManholes,
                            performCutGeometry: false,
                            outUpstreamOutlets: rowUpOutlets,
                            outDownstreamOutlets: rowDnOutlets,
                            outCipDetails: rowCipDetails);

                        CulvertCoreEngine.ExecuteAllCulvertCuts(
                            doc,
                            isRowCastInPlace,
                            rowUpOutlets,
                            rowDnOutlets,
                            outCreatedBarrels,
                            outCreatedBeddings,
                            outCreatedManholes,
                            rowCipDetails,
                            outCreatedAprons);

                        try { doc.Regenerate(); } catch { }

                        var allInstances = new List<FamilyInstance>();
                        allInstances.AddRange(outCreatedOutlets);
                        allInstances.AddRange(outCreatedBarrels);
                        allInstances.AddRange(outCreatedBeddings);
                        allInstances.AddRange(outCreatedAprons);
                        allInstances.AddRange(outCreatedManholes);

                        var opt = new Options
                        {
                            DetailLevel = ViewDetailLevel.Fine,
                            ComputeReferences = false,
                            IncludeNonVisibleObjects = false
                        };

                        var expandedInstances = new List<FamilyInstance>();
                        var visitedIds = new HashSet<ElementId>();
                        foreach (var inst in allInstances)
                        {
                            CollectInstanceAndSubComponents(doc, inst, expandedInstances, visitedIds);
                        }

                        foreach (var inst in expandedInstances)
                        {
                            if (inst == null || !inst.IsValidObject) continue;
                            var geom = inst.get_Geometry(opt);
                            if (geom == null) continue;

                            Brush brush = GetBrushForFamily(inst.Symbol?.FamilyName ?? "", inst.Symbol?.Name ?? "");
                            ExtractSolidsRecursive(geom, null, p1, cosRot, sinRot, brush, result);
                        }
                    }
                    finally
                    {
                        t.RollBack();
                    }
                }

                return result.Count > 0;
            }
            catch
            {
                return false;
            }
        }

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

                var expandedInstances = new List<FamilyInstance>();
                var visitedIds = new HashSet<ElementId>();
                foreach (var inst in culvertInstances)
                {
                    CollectInstanceAndSubComponents(doc, inst, expandedInstances, visitedIds);
                }

                foreach (var inst in expandedInstances)
                {
                    var geom = inst.get_Geometry(opt);
                    if (geom == null) continue;

                    Brush brush = GetBrushForFamily(inst.Symbol?.FamilyName ?? "", inst.Symbol?.Name ?? "");
                    ExtractSolidsRecursive(geom, null, p1, cosRot, sinRot, brush, result);
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
                   upper.Contains("BT LOT") || upper.Contains("DA DAM") || upper.Contains("BTL") || upper.Contains("DDD") ||
                   upper.Contains("HO KY THUAT") || upper.Contains("HỐ KỸ THUẬT") || upper.Contains("KY THUAT") || upper.Contains("KỸ THUẬT") || upper.Contains("CKT");
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

        private static Brush GetBrushForFamily(string fn, string typeName)
        {
            string upper = $"{fn} {typeName}".ToUpperInvariant();
            SolidColorBrush brush;
            if (upper.Contains("THAN CONG") || upper.Contains("THÂN CỐNG") || upper.Contains("THAN_CONG") || upper.Contains("DOT") || upper.Contains("ĐỐT"))
                brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 68, 78)); // Bê tông xám than đậm (#3F444E) như Hình 2
            else if (upper.Contains("BE TONG LOT") || upper.Contains("BTL") || upper.Contains("BT LOT"))
                brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)); // Bê tông lót (#94A3B8)
            else if (upper.Contains("DA DAM") || upper.Contains("DDD") || upper.Contains("ĐÁ DĂM"))
                brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(202, 138, 4));  // Đá dăm đệm (#CA8A04)
            else if (upper.Contains("HOP NOI") || upper.Contains("HỘP NỐI") || upper.Contains("HO GA") || upper.Contains("HỐ GA") || upper.Contains("HN") ||
                     upper.Contains("HO KY THUAT") || upper.Contains("HỐ KỸ THUẬT") || upper.Contains("CO GIENG") || upper.Contains("CỔ GIẾNG") ||
                     upper.Contains("TAM DAN") || upper.Contains("TẤM ĐAN") || upper.Contains("NAP") || upper.Contains("NẮP") || 
                     upper.Contains("THAN GIENG") || upper.Contains("THÂN GIẾNG") || upper.Contains("CKT"))
                brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199)); // Xanh dương (#0284C7) như Hình 2
            else if (upper.Contains("CUA XA") || upper.Contains("CỬA XẢ") || upper.Contains("CX") || upper.Contains("SAN CONG") || upper.Contains("TUONG DAU") || upper.Contains("TUONG CANH"))
                brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199)); // Xanh dương (#0284C7) như Hình 2
            else if (upper.Contains("SAN GIA CO") || upper.Contains("SÂN GIA CỐ") || upper.Contains("SGC"))
                brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199)); // Xanh dương (#0284C7) như Hình 2
            else
                brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184));

            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }

        private static void CollectInstanceAndSubComponents(
            Document doc,
            FamilyInstance inst,
            List<FamilyInstance> list,
            HashSet<ElementId> visited)
        {
            if (inst == null || !inst.IsValidObject || visited.Contains(inst.Id)) return;
            visited.Add(inst.Id);
            list.Add(inst);

            try
            {
                var subIds = inst.GetSubComponentIds();
                if (subIds != null && subIds.Count > 0)
                {
                    foreach (var sId in subIds)
                    {
                        if (doc.GetElement(sId) is FamilyInstance sub && sub.IsValidObject)
                        {
                            CollectInstanceAndSubComponents(doc, sub, list, visited);
                        }
                    }
                }
            }
            catch { }
        }

        private static void ExtractSolidsRecursive(
            GeometryElement geomElem,
            Autodesk.Revit.DB.Transform? parentTransform,
            XYZ p1,
            double cosRot,
            double sinRot,
            Brush brush,
            List<(MeshGeometry3D, Brush)> list)
        {
            if (geomElem == null) return;

            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid && solid.Volume > 1e-7 && !solid.Faces.IsEmpty)
                {
                    var mesh = ConvertSolidToLocalMesh(solid, parentTransform, p1, cosRot, sinRot);
                    if (mesh != null && mesh.Positions.Count > 0)
                    {
                        list.Add((mesh, brush));
                    }
                }
                else if (obj is GeometryInstance gi)
                {
                    int countBefore = list.Count;

                    // 1. Ưu tiên 1: Lấy từ GetInstanceGeometry()
                    // Các solids trong GetInstanceGeometry() ĐÃ Ở TỌA ĐỘ DỰ ÁN (World Coordinates).
                    // Do đó parentTransform cho solids này là null (không cần nhân thêm).
                    try
                    {
                        var instGeom = gi.GetInstanceGeometry();
                        if (instGeom != null)
                        {
                            ExtractSolidsRecursive(instGeom, null, p1, cosRot, sinRot, brush, list);
                        }
                    }
                    catch { }

                    // 2. Fallback: Nếu GetInstanceGeometry() không mang lại solid nào, thử GetSymbolGeometry() với Transform cộng dồn
                    if (list.Count == countBefore)
                    {
                        try
                        {
                            Autodesk.Revit.DB.Transform totalTr = (parentTransform != null && !parentTransform.IsIdentity)
                                ? parentTransform.Multiply(gi.Transform)
                                : gi.Transform;

                            var symGeom = gi.GetSymbolGeometry();
                            if (symGeom != null)
                            {
                                ExtractSolidsRecursive(symGeom, totalTr, p1, cosRot, sinRot, brush, list);
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        private static MeshGeometry3D? ConvertSolidToLocalMesh(
            Solid solid,
            Autodesk.Revit.DB.Transform? transform,
            XYZ p1,
            double cosRot,
            double sinRot)
        {
            var mesh = new MeshGeometry3D();
            const double ft2m = 0.3048;

            bool hasTr = transform != null && !transform.IsIdentity;

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

                    if (hasTr)
                    {
                        v0 = transform!.OfPoint(v0);
                        v1 = transform!.OfPoint(v1);
                        v2 = transform!.OfPoint(v2);
                    }

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

            if (mesh.CanFreeze) mesh.Freeze();
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
