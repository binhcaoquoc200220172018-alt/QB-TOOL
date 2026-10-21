using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using InfraBIM.CulvertTool.Models;

namespace InfraBIM.CulvertTool.Services
{
    public static class CulvertCoreEngine
    {
        /// <summary>
        /// Xây dựng toàn bộ các cống ngang theo chuẩn 4 cụm cấu kiện (Thân cống, Cửa xả, Sân gia cố, Hố ga)
        /// </summary>
        public static (int SuccessCount, int ErrorCount, List<string> Logs) BuildAllCulvertsV2(
            Document doc,
            IList<CulvertRowData> culvertList,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IList<CulvertComponentItem> barrelComponents,
            IList<CulvertComponentItem> outletComponents,
            IList<CulvertComponentItem> apronComponents,
            IList<CulvertComponentItem> manholeComponents,
            double lStdM,
            double jointGapM,
            double bBoxM,
            double defaultKhoangCachTim,
            CulvertArrayMode arrayMode,
            bool isCastInPlace,
            bool useSurveyPoint = true,
            IEnumerable<CulvertMaterialItem>? materialSettings = null)
        {
            return BuildAllCulvertsV2(
                doc,
                culvertList,
                bimConfig,
                customBimParams,
                familyParameterMappings,
                barrelComponents,
                barrelComponents,
                outletComponents,
                apronComponents,
                manholeComponents,
                lStdM,
                jointGapM,
                bBoxM,
                defaultKhoangCachTim,
                arrayMode,
                isCastInPlace,
                useSurveyPoint,
                materialSettings);
        }

        public static (int SuccessCount, int ErrorCount, List<string> Logs) BuildAllCulvertsV2(
            Document doc,
            IList<CulvertRowData> culvertList,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IList<CulvertComponentItem> precastBarrelComponents,
            IList<CulvertComponentItem> castInPlaceBarrelComponents,
            IList<CulvertComponentItem> outletComponents,
            IList<CulvertComponentItem> apronComponents,
            IList<CulvertComponentItem> manholeComponents,
            double lStdM,
            double jointGapM,
            double bBoxM,
            double defaultKhoangCachTim,
            CulvertArrayMode arrayMode,
            bool isCastInPlace,
            bool useSurveyPoint = true,
            IEnumerable<CulvertMaterialItem>? materialSettings = null)
        {
            return BuildAllCulvertsV2(
                doc,
                culvertList,
                bimConfig,
                customBimParams,
                familyParameterMappings,
                precastBarrelComponents,
                castInPlaceBarrelComponents,
                outletComponents,
                apronComponents,
                manholeComponents,
                lStdM,
                jointGapM,
                arrayMode,
                lStdM,
                jointGapM,
                arrayMode,
                bBoxM,
                defaultKhoangCachTim,
                useSurveyPoint,
                materialSettings);
        }

        public static (int SuccessCount, int ErrorCount, List<string> Logs) BuildAllCulvertsV2(
            Document doc,
            IList<CulvertRowData> culvertList,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IList<CulvertComponentItem> precastBarrelComponents,
            IList<CulvertComponentItem> castInPlaceBarrelComponents,
            IList<CulvertComponentItem> outletComponents,
            IList<CulvertComponentItem> apronComponents,
            IList<CulvertComponentItem> manholeComponents,
            double lStdPrecastM,
            double jointGapPrecastM,
            CulvertArrayMode arrayModePrecast,
            double lStdCastInPlaceM,
            double jointGapCastInPlaceM,
            CulvertArrayMode arrayModeCastInPlace,
            double bBoxM,
            double defaultKhoangCachTim,
            bool useSurveyPoint = true,
            IEnumerable<CulvertMaterialItem>? materialSettings = null,
            int duplicateOption = 0,
            IEnumerable<CulvertTypeProfile>? castInPlaceProfiles = null)
        {
            var logs = new List<string>();
            int successCount = 0;
            int errorCount = 0;

            if (culvertList == null || culvertList.Count == 0)
            {
                logs.Add("Không có dữ liệu cống để tạo.");
                return (0, 0, logs);
            }

            using (var tg = new TransactionGroup(doc, "INFRA BIM - Tự Động Rải Cống Ngang"))
            {
                tg.Start();

                // 1. Kích hoạt tất cả Family Symbol được dùng trước khi rải
                using (var tAct = new Transaction(doc, "Kích hoạt Family Symbols"))
                {
                    var failOptsAct = tAct.GetFailureHandlingOptions();
                    failOptsAct.SetFailuresPreprocessor(new SuppressAllWarningsFailuresPreprocessor());
                    failOptsAct.SetClearAfterRollback(true);
                    tAct.SetFailureHandlingOptions(failOptsAct);

                    tAct.Start();

                    EnsureOutletTypesExist(doc, outletComponents);
                    EnsureApronTypesExist(doc, apronComponents);

                    var allComps = (precastBarrelComponents ?? new List<CulvertComponentItem>())
                        .Concat(castInPlaceBarrelComponents ?? new List<CulvertComponentItem>())
                        .Concat(outletComponents ?? new List<CulvertComponentItem>())
                        .Concat(apronComponents ?? new List<CulvertComponentItem>())
                        .Concat(manholeComponents ?? new List<CulvertComponentItem>());

                    foreach (var c in allComps)
                    {
                        if (c.IsActive && c.SelectedSymbol != null)
                        {
                            var sym = c.SelectedSymbol.GetFreshSymbol(doc);
                            if (sym != null)
                            {
                                ActivateSymbol(sym);
                            }
                        }
                    }
                    doc.Regenerate();
                    tAct.Commit();
                }

                foreach (var row in culvertList)
                {
                    if (!row.IsSelected) continue;

                    // Kiểm tra xem cống này đã được dựng trong mô hình Revit trước đó hay chưa
                    var existingInstances = FindExistingCulvertInstances(doc, row, culvertList, useSurveyPoint);
                    if (existingInstances != null && existingInstances.Count > 0)
                    {
                        if (duplicateOption == 1) // "Bỏ qua dòng trùng"
                        {
                            logs.Add($"⏭️ Cống STT {row.STT} ({row.LyTrinh}): Đã tồn tại trong mô hình Revit -> Bỏ qua.");
                            continue;
                        }

                        // duplicateOption == 0: "Cập nhật theo Lý trình" -> Tự động gán/cập nhật thông tin BIM & Vật liệu vào mô hình đã có
                        using (var subT = new Transaction(doc, $"Cập nhật BIM & Vật liệu Cống STT {row.STT} ({row.LyTrinh})"))
                        {
                            try
                            {
                                var failOpts = subT.GetFailureHandlingOptions();
                                failOpts.SetFailuresPreprocessor(new SuppressAllWarningsFailuresPreprocessor());
                                failOpts.SetClearAfterRollback(true);
                                subT.SetFailureHandlingOptions(failOpts);

                                subT.Start();
                                int updatedCount = UpdateExistingCulvertElements(
                                    doc,
                                    existingInstances,
                                    row,
                                    bimConfig,
                                    customBimParams,
                                    familyParameterMappings,
                                    materialSettings);
                                subT.Commit();

                                successCount++;
                                logs.Add($"🔄 Cống STT {row.STT} ({row.LyTrinh}): Đã phát hiện mô hình có sẵn trong Revit. Đã tự động cập nhật thông tin BIM & vật liệu cho {updatedCount} cấu kiện.");
                                continue;
                            }
                            catch (Exception ex)
                            {
                                subT.RollBack();
                                errorCount++;
                                logs.Add($"❌ Lỗi khi cập nhật cống STT {row.STT} ({row.LyTrinh}): {ex.Message}");
                                continue;
                            }
                        }
                    }

                    using (var subT = new Transaction(doc, $"Rải cống STT {row.STT} ({row.LyTrinh})"))
                    {
                        try
                        {
                            var failOpts = subT.GetFailureHandlingOptions();
                            failOpts.SetFailuresPreprocessor(new SuppressAllWarningsFailuresPreprocessor());
                            failOpts.SetClearAfterRollback(true);
                            subT.SetFailureHandlingOptions(failOpts);

                            subT.Start();

                            // Khớp Profile Loại cống đổ tại chỗ nếu có
                            CulvertTypeProfile? matchedProfile = null;
                            if (castInPlaceProfiles != null)
                            {
                                if (!string.IsNullOrEmpty(row.AssignedProfileName))
                                {
                                    matchedProfile = castInPlaceProfiles.FirstOrDefault(p => string.Equals(p.TypeName, row.AssignedProfileName, StringComparison.OrdinalIgnoreCase));
                                }
                                if (matchedProfile == null)
                                {
                                    string rowFull = $"{row.LoaiCong} {row.KhauDo} {row.CauKien} {row.GhiChu}".ToUpperInvariant();
                                    matchedProfile = castInPlaceProfiles.FirstOrDefault(p => 
                                        (!string.IsNullOrEmpty(p.TypeName) && rowFull.Contains(p.TypeName.ToUpperInvariant())) ||
                                        (!string.IsNullOrEmpty(row.KhauDo) && !string.IsNullOrEmpty(p.TypeName) && p.TypeName.Contains(row.KhauDo, StringComparison.OrdinalIgnoreCase)));
                                }
                            }

                            // Phân định dòng cống này thuộc loại Đúc sẵn hay Đổ tại chỗ
                            bool isRowCastInPlace = (matchedProfile != null) ||
                                (row.SoCua > 1) ||
                                (row.CauKien?.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (row.CauKien?.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (row.LoaiCong?.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (row.LoaiCong?.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (row.GhiChu?.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (row.GhiChu?.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) >= 0);

                            if (matchedProfile == null && isRowCastInPlace && castInPlaceProfiles != null)
                            {
                                matchedProfile = castInPlaceProfiles.FirstOrDefault();
                            }

                            var targetBarrelList = isRowCastInPlace 
                                ? (matchedProfile?.Components ?? castInPlaceBarrelComponents) 
                                : precastBarrelComponents;
                            double curLStd = isRowCastInPlace 
                                ? (matchedProfile?.L_Std ?? lStdCastInPlaceM) 
                                : lStdPrecastM;
                            double curJointGap = isRowCastInPlace 
                                ? (matchedProfile?.Khe_Ho ?? jointGapCastInPlaceM) 
                                : jointGapPrecastM;
                            CulvertArrayMode curArrayMode = isRowCastInPlace 
                                ? (matchedProfile != null 
                                    ? (matchedProfile.SelectedArrayModeIndex == 0 ? CulvertArrayMode.CenterOut : CulvertArrayMode.OneWay) 
                                    : arrayModeCastInPlace) 
                                : arrayModePrecast;

                            // Tự động nhận diện / bảo toàn Family tương ứng cho từng dòng cống
                            var curBarrel = ResolveComponentsForCulvert(doc, targetBarrelList, row);
                            var curOutlet = ResolveComponentsForCulvert(doc, outletComponents, row);
                            var curApron = ResolveComponentsForCulvert(doc, apronComponents, row);
                            var curManhole = ResolveComponentsForCulvert(doc, manholeComponents, row);

                            var rowOutlets = new List<FamilyInstance>();
                            var rowBarrels = new List<FamilyInstance>();
                            var rowBeddings = new List<FamilyInstance>();
                            var rowAprons = new List<FamilyInstance>();
                            var rowManholes = new List<FamilyInstance>();
                            var rowUpOutlets = new List<FamilyInstance>();
                            var rowDnOutlets = new List<FamilyInstance>();
                            var rowCipDetails = new CastInPlaceDetails();

                            BuildSingleCulvertV2(
                                doc,
                                row,
                                bimConfig,
                                customBimParams,
                                familyParameterMappings,
                                curBarrel,
                                curOutlet,
                                curApron,
                                curManhole,
                                curLStd,
                                curJointGap,
                                bBoxM,
                                defaultKhoangCachTim,
                                curArrayMode,
                                isRowCastInPlace,
                                useSurveyPoint,
                                materialSettings,
                                rowOutlets,
                                rowBarrels,
                                rowBeddings,
                                rowAprons,
                                rowManholes,
                                performCutGeometry: false,
                                outUpstreamOutlets: rowUpOutlets,
                                outDownstreamOutlets: rowDnOutlets,
                                outCipDetails: rowCipDetails);

                            subT.Commit();

                            // CHỈ THỰC HIỆN CUT GEOMETRY KHI BẤM "KHỞI TẠO VÀ THỰC HIỆN MÔ HÌNH"
                            // Mở Transaction riêng biệt để thực hiện Cut Geometry sau khi các cấu kiện đã commit hoàn tất
                            bool isTech = (row.DetermineCulvertType() == "CỐNG KỸ THUẬT" || 
                                          row.LoaiCong?.IndexOf("KỸ THUẬT", StringComparison.OrdinalIgnoreCase) >= 0 || 
                                          row.LoaiCong?.IndexOf("KY THUAT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          row.GhiChu?.IndexOf("KỸ THUẬT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          row.GhiChu?.IndexOf("KY THUAT", StringComparison.OrdinalIgnoreCase) >= 0);

                            if (isTech)
                            {
                                if (rowManholes.Count > 0 && rowBarrels.Count > 0)
                                {
                                    using (var cutT = new Transaction(doc, $"Cut Geometry Cống Kỹ Thuật STT {row.STT}"))
                                    {
                                        try
                                        {
                                            var cutFailOpts = cutT.GetFailureHandlingOptions();
                                            cutFailOpts.SetFailuresPreprocessor(new SuppressAllWarningsFailuresPreprocessor());
                                            cutFailOpts.SetClearAfterRollback(true);
                                            cutT.SetFailureHandlingOptions(cutFailOpts);

                                            cutT.Start();
                                            doc.Regenerate();

                                            var hkt1 = rowManholes.ElementAtOrDefault(0);
                                            var hkt2 = rowManholes.ElementAtOrDefault(1);
                                            var hn = rowManholes.ElementAtOrDefault(2);

                                            var firstBar = rowBarrels.FirstOrDefault();
                                            var lastBar = rowBarrels.LastOrDefault();

                                            // 1. Cut HKT1 với đốt cống đầu tiên và BTL/DDD đoạn 1
                                            if (hkt1 != null && hkt1.IsValidObject)
                                            {
                                                if (firstBar != null && firstBar.IsValidObject) TryCutGeometrySafe(doc, hkt1, firstBar);
                                                var btl1 = rowBeddings.ElementAtOrDefault(0);
                                                var ddd1 = rowBeddings.ElementAtOrDefault(1);
                                                if (btl1 != null && btl1.IsValidObject) TryCutGeometrySafe(doc, hkt1, btl1);
                                                if (ddd1 != null && ddd1.IsValidObject) TryCutGeometrySafe(doc, hkt1, ddd1);
                                            }

                                            // 2. Cut HKT2 với đốt cống cuối cùng và BTL/DDD đoạn 2
                                            if (hkt2 != null && hkt2.IsValidObject)
                                            {
                                                if (lastBar != null && lastBar.IsValidObject) TryCutGeometrySafe(doc, hkt2, lastBar);
                                                var btl2 = rowBeddings.ElementAtOrDefault(2);
                                                var ddd2 = rowBeddings.ElementAtOrDefault(3);
                                                if (btl2 != null && btl2.IsValidObject) TryCutGeometrySafe(doc, hkt2, btl2);
                                                if (ddd2 != null && ddd2.IsValidObject) TryCutGeometrySafe(doc, hkt2, ddd2);
                                            }

                                            // 3. Cut Hộp nối với 2 đốt cống liền kề trái và phải
                                            if (hn != null && hn.IsValidObject && rowBarrels.Count >= 2)
                                            {
                                                var nearestBarrels = rowBarrels
                                                    .Where(b => b != null && b.IsValidObject)
                                                    .OrderBy(b => {
                                                        var loc = b.Location as LocationPoint;
                                                        var hnLoc = hn.Location as LocationPoint;
                                                        if (loc != null && hnLoc != null) return loc.Point.DistanceTo(hnLoc.Point);
                                                        return double.MaxValue;
                                                    })
                                                    .Take(2)
                                                    .ToList();

                                                foreach (var nb in nearestBarrels)
                                                {
                                                    TryCutGeometrySafe(doc, hn, nb);
                                                }
                                            }

                                            cutT.Commit();
                                        }
                                        catch
                                        {
                                            try { if (cutT.HasStarted() && !cutT.HasEnded()) cutT.RollBack(); } catch { }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Cống ngang thông thường (Đúc sẵn & Đổ tại chỗ)
                                if (rowOutlets.Count > 0 && (rowBarrels.Count > 0 || rowBeddings.Count > 0))
                                {
                                    using (var cutT = new Transaction(doc, $"Cut Geometry Cửa xả và Cống hộp STT {row.STT}"))
                                    {
                                        try
                                        {
                                            var cutFailOpts = cutT.GetFailureHandlingOptions();
                                            cutFailOpts.SetFailuresPreprocessor(new SuppressAllWarningsFailuresPreprocessor());
                                            cutFailOpts.SetClearAfterRollback(true);
                                            cutT.SetFailureHandlingOptions(cutFailOpts);

                                            cutT.Start();

                                            ExecuteAllCulvertCuts(
                                                doc,
                                                isRowCastInPlace,
                                                rowUpOutlets,
                                                rowDnOutlets,
                                                rowBarrels,
                                                rowBeddings,
                                                rowManholes,
                                                rowCipDetails,
                                                rowAprons);

                                            cutT.Commit();
                                        }
                                        catch
                                        {
                                            try { if (cutT.HasStarted() && !cutT.HasEnded()) cutT.RollBack(); } catch { }
                                        }
                                    }
                                }
                            }
                            successCount++;
                            logs.Add($"✅ Đã tạo thành công cống STT {row.STT} ({row.LyTrinh}).");
                        }
                        catch (Exception ex)
                        {
                            subT.RollBack();
                            errorCount++;
                            string errDetail = $"❌ Lỗi khi tạo cống STT {row.STT} ({row.LyTrinh}): {ex.GetType().Name} - {ex.Message}";
                            if (ex.InnerException != null)
                            {
                                errDetail += $" -> {ex.InnerException.Message}";
                            }
                            logs.Add(errDetail);
                            try
                            {
                                string logPath = System.IO.Path.Combine(@"C:\Users\ADMIN\Desktop\PHAT TRIEN TOOL_HTKT", "culvert_build_error.log");
                                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {errDetail}\nStack Trace:\n{ex.StackTrace}\n\n");
                            }
                            catch { }
                        }
                    }
                }

                tg.Assimilate();
            }


            return (successCount, errorCount, logs);
        }

        private static void SafeRotate(Document doc, ElementId? elemId, XYZ axisOrigin, double angleRad)
        {
            if (doc == null || elemId == null || elemId == ElementId.InvalidElementId) return;
            if (Math.Abs(angleRad) < 0.0001 || Math.Abs(angleRad - 2 * Math.PI) < 0.0001) return;

            try
            {
                ElementTransformUtils.RotateElement(doc, elemId, Line.CreateBound(axisOrigin, axisOrigin + XYZ.BasisZ), angleRad);
            }
            catch
            {
                try
                {
                    if (doc.GetElement(elemId) is FamilyInstance inst && inst.Location is LocationPoint lp)
                    {
                        lp.Rotate(Line.CreateBound(axisOrigin, axisOrigin + XYZ.BasisZ), angleRad);
                    }
                }
                catch { }
            }
        }

        private static void ActivateSymbol(FamilySymbol? sym)
        {
            if (sym == null) return;
            try
            {
                if (sym.IsValidObject && !sym.IsActive)
                {
                    sym.Activate();
                }
            }
            catch { }
        }

        private static FamilyInstance CreateInstanceSafe(Document doc, XYZ pt, FamilySymbol sym)
        {
            ActivateSymbol(sym);
            try
            {
                return doc.Create.NewFamilyInstance(pt, sym, StructuralType.NonStructural);
            }
            catch
            {
                // Fallback nếu Family là Work-Plane Based hoặc Level-Hosted (ví dụ HOP NOI CONG DOC)
                Level? level = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => Math.Abs(l.Elevation - pt.Z))
                    .FirstOrDefault();

                if (level != null)
                {
                    var inst = doc.Create.NewFamilyInstance(pt, sym, level, StructuralType.NonStructural);
                    Parameter pElev = inst.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                    if (pElev != null && !pElev.IsReadOnly)
                    {
                        pElev.Set(pt.Z - level.Elevation);
                    }
                    return inst;
                }
                throw;
            }
        }

        private static FamilySymbol? TryAutoLoadFamilySymbol(Document doc, string familyKeyword, string? typeKeyword = null, string? preferredFolder = null)
        {
            try
            {
                // 1. Kiểm tra trong Document trước
                var sym = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => (s.FamilyName != null && s.FamilyName.IndexOf(familyKeyword, StringComparison.OrdinalIgnoreCase) >= 0));
                if (sym != null)
                {
                    ActivateSymbol(sym);
                    return sym;
                }

                // 2. Nếu chưa có, quét các thư mục trên đĩa
                var foldersToSearch = new List<string>();
                if (!string.IsNullOrWhiteSpace(preferredFolder) && System.IO.Directory.Exists(preferredFolder))
                    foldersToSearch.Add(preferredFolder);
                const string defaultCktFolder = @"C:\Users\ADMIN\Desktop\TEST TOOL\FAMLY REVIT_HTKT\2. CỐNG KỸ THUẬT";
                if (System.IO.Directory.Exists(defaultCktFolder) && !foldersToSearch.Contains(defaultCktFolder))
                    foldersToSearch.Add(defaultCktFolder);

                foreach (var folder in foldersToSearch)
                {
                    var rfaFiles = System.IO.Directory.GetFiles(folder, "*.rfa", System.IO.SearchOption.AllDirectories);
                    var matchedFile = rfaFiles.FirstOrDefault(f => System.IO.Path.GetFileName(f).IndexOf(familyKeyword, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (matchedFile != null && System.IO.File.Exists(matchedFile))
                    {
                        var loadOptions = new InfraBIM.CulvertTool.ViewModels.CustomFamilyLoadOptions();
                        if (doc.LoadFamily(matchedFile, loadOptions, out Family loadedFamily) && loadedFamily != null)
                        {
                            var symId = loadedFamily.GetFamilySymbolIds().FirstOrDefault();
                            if (symId != null && symId != ElementId.InvalidElementId)
                            {
                                var loadedSym = doc.GetElement(symId) as FamilySymbol;
                                if (loadedSym != null)
                                {
                                    ActivateSymbol(loadedSym);
                                    return loadedSym;
                                }
                            }
                        }
                        else
                        {
                            string baseName = System.IO.Path.GetFileNameWithoutExtension(matchedFile);
                            var existingFamily = new FilteredElementCollector(doc)
                                .OfClass(typeof(Family))
                                .Cast<Family>()
                                .FirstOrDefault(f => string.Equals(f.Name, baseName, StringComparison.OrdinalIgnoreCase));
                            if (existingFamily != null)
                            {
                                var symId = existingFamily.GetFamilySymbolIds().FirstOrDefault();
                                if (symId != null && symId != ElementId.InvalidElementId)
                                {
                                    var loadedSym = doc.GetElement(symId) as FamilySymbol;
                                    if (loadedSym != null)
                                    {
                                        ActivateSymbol(loadedSym);
                                        return loadedSym;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static FamilyInstance? CreateAdaptiveInstanceSafe(Document doc, FamilySymbol sym, IList<XYZ> points)
        {
            if (sym == null || points == null || points.Count == 0) return null;
            ActivateSymbol(sym);

            if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(sym))
            {
                try
                {
                    FamilyInstance inst = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, sym);
                    IList<ElementId> placePointIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(inst);
                    for (int i = 0; i < Math.Min(points.Count, placePointIds.Count); i++)
                    {
                        if (doc.GetElement(placePointIds[i]) is ReferencePoint refPt)
                        {
                            refPt.Position = points[i];
                        }
                    }
                    try { doc.Regenerate(); } catch { }
                    return inst;
                }
                catch
                {
                    // Fallback thử đảo ngược thứ tự điểm nếu Family định nghĩa chiều ngược lại
                    if (points.Count >= 2)
                    {
                        try
                        {
                            var reversed = points.Reverse().ToList();
                            FamilyInstance instRev = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, sym);
                            IList<ElementId> placePointIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(instRev);
                            for (int i = 0; i < Math.Min(reversed.Count, placePointIds.Count); i++)
                            {
                                if (doc.GetElement(placePointIds[i]) is ReferencePoint refPt)
                                {
                                    refPt.Position = reversed[i];
                                }
                            }
                            try { doc.Regenerate(); } catch { }
                            return instRev;
                        }
                        catch { }
                    }
                }
            }

            // Fallback nếu không phải Adaptive hoặc tạo Adaptive thất bại: Đặt theo Point-Based an toàn
            try
            {
                XYZ midPt = points[0];
                if (points.Count > 1)
                {
                    midPt = (points[0] + points[1]) * 0.5;
                }
                return CreateInstanceSafe(doc, midPt, sym);
            }
            catch { }

            return null;
        }

        private static void SetOutletVisibilitySafe(FamilyInstance inst, string catType)
        {
            string upper = catType.ToUpperInvariant();
            if (upper.Contains("SÂN GIA CỐ") || upper.Contains("SAN GIA CO") || upper.Contains("SGC"))
            {
                if (upper.Contains("BÊ TÔNG LÓT") || upper.Contains("BE TONG LOT") || upper.Contains("BTL") || upper.Contains("LÓT") || upper.Contains("LOT"))
                {
                    SetParamYesNo(inst, "CX_SGC_BTL_SH", 1);
                    SetParamYesNo(inst, "CX_SGC_SH", 0);
                    SetParamYesNo(inst, "CX_SGC_DD_SH", 0);
                }
                else if (upper.Contains("ĐÁ DĂM") || upper.Contains("DA DAM"))
                {
                    SetParamYesNo(inst, "CX_SGC_DD_SH", 1);
                    SetParamYesNo(inst, "CX_SGC_SH", 0);
                    SetParamYesNo(inst, "CX_SGC_BTL_SH", 0);
                }
                else
                {
                    SetParamYesNo(inst, "CX_SGC_SH", 1);
                    SetParamYesNo(inst, "CX_SGC_BTL_SH", 0);
                    SetParamYesNo(inst, "CX_SGC_DD_SH", 0);
                }
            }
            else // Sân cống chính (TNN_CX_SAN CONG)
            {
                if (upper.Contains("TƯỜNG ĐẦU") || upper.Contains("TUONG DAU"))
                {
                    SetParamYesNo(inst, "CX_TUONG DAU", 1);
                    SetParamYesNo(inst, "TD_BE TONG LOT", 1);
                    SetParamYesNo(inst, "TD_DA DAM DEM", 1);
                    SetParamYesNo(inst, "CX_TUONG CANH", 0);
                    SetParamYesNo(inst, "CX_SAN CONG", 0);
                    SetParamYesNo(inst, "CX_BE TONG LOT", 0);
                    SetParamYesNo(inst, "CX_DA DAM DEM", 0);
                }
                else if (upper.Contains("TƯỜNG CÁNH") || upper.Contains("TUONG CANH"))
                {
                    SetParamYesNo(inst, "CX_TUONG CANH", 1);
                    SetParamYesNo(inst, "CX_TUONG DAU", 0);
                    SetParamYesNo(inst, "CX_SAN CONG", 0);
                    SetParamYesNo(inst, "CX_BE TONG LOT", 0);
                    SetParamYesNo(inst, "CX_DA DAM DEM", 0);
                }
                else if (upper.Contains("BÊ TÔNG LÓT") || upper.Contains("BE TONG LOT") || upper.Contains("BTL"))
                {
                    SetParamYesNo(inst, "CX_BE TONG LOT", 1);
                    SetParamYesNo(inst, "CX_TUONG DAU", 0);
                    SetParamYesNo(inst, "CX_TUONG CANH", 0);
                    SetParamYesNo(inst, "CX_SAN CONG", 0);
                    SetParamYesNo(inst, "CX_DA DAM DEM", 0);
                }
                else if (upper.Contains("ĐÁ DĂM") || upper.Contains("DA DAM"))
                {
                    SetParamYesNo(inst, "CX_DA DAM DEM", 1);
                    SetParamYesNo(inst, "CX_TUONG DAU", 0);
                    SetParamYesNo(inst, "CX_TUONG CANH", 0);
                    SetParamYesNo(inst, "CX_SAN CONG", 0);
                    SetParamYesNo(inst, "CX_BE TONG LOT", 0);
                }
                else if (upper.Contains("SÂN CỐNG") || upper.Contains("SAN CONG") || upper.Contains("BẢN ĐÁY") || upper.Contains("BAN DAY"))
                {
                    SetParamYesNo(inst, "CX_SAN CONG", 1);
                    SetParamYesNo(inst, "CX_TUONG DAU", 0);
                    SetParamYesNo(inst, "CX_TUONG CANH", 0);
                    SetParamYesNo(inst, "CX_BE TONG LOT", 0);
                    SetParamYesNo(inst, "CX_DA DAM DEM", 0);
                }
            }
        }

        private static void SetParamYesNo(FamilyInstance inst, string paramName, int value)
        {
            if (inst == null) return;
            var p = inst.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Integer)
            {
                p.Set(value);
            }
            // TUYỆT ĐỐI KHÔNG can thiệp vào inst.Symbol (Type Parameter)
            // Vì các Type trong Family (như TNN_CX_SAN CONG, TNN_CX_TUONG DAU...) đã có thiết lập Yes/No cố định.
            // Việc sửa Type parameter tại runtime làm phá vỡ ràng buộc hình học của Type và gây lỗi nghiêm trọng:
            // "Can't make type TNN_CX_SAN CONG"
        }

        private static void SetParamYesNoType(FamilySymbol s, string paramName, int val)
        {
            if (s == null || !s.IsValidObject) return;
            try
            {
                var p = s.LookupParameter(paramName);
                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Integer)
                {
                    p.Set(val);
                }
            }
            catch { }
        }

        private static void EnsureOutletTypesExist(Document doc, IList<CulvertComponentItem>? outletComponents)
        {
            if (doc == null || outletComponents == null || outletComponents.Count == 0) return;

            FamilySymbol? baseSym = null;
            foreach (var c in outletComponents)
            {
                if (c.SelectedSymbol != null)
                {
                    var s = c.SelectedSymbol.GetFreshSymbol(doc);
                    if (s != null && s.IsValidObject && (s.FamilyName.Contains("TNN_CX") || s.FamilyName.Contains("SAN CONG") || s.FamilyName.Contains("CUA XA")))
                    {
                        baseSym = s;
                        break;
                    }
                }
            }

            if (baseSym == null)
            {
                baseSym = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => s != null && s.IsValidObject && (s.FamilyName.Contains("TNN_CX_SAN CONG") || s.FamilyName.Contains("SAN CONG")));
            }

            if (baseSym == null || !baseSym.IsValidObject) return;

            Family? fam = null;
            try { fam = baseSym.Family; } catch { }
            if (fam == null || !fam.IsValidObject) return;

            var existingSymbols = fam.GetFamilySymbolIds()
                .Select(id => doc.GetElement(id) as FamilySymbol)
                .Where(s => s != null && s.IsValidObject)
                .ToList();

            var neededTypes = new Dictionary<string, Action<FamilySymbol>>(StringComparer.OrdinalIgnoreCase)
            {
                ["TNN_CX_TUONG DAU"] = s =>
                {
                    SetParamYesNoType(s, "CX_TUONG DAU", 1);
                    SetParamYesNoType(s, "TD_BE TONG LOT", 1);
                    SetParamYesNoType(s, "TD_DA DAM DEM", 1);
                    SetParamYesNoType(s, "CX_TUONG CANH", 0);
                    SetParamYesNoType(s, "CX_SAN CONG", 0);
                    SetParamYesNoType(s, "CX_BE TONG LOT", 0);
                    SetParamYesNoType(s, "CX_DA DAM DEM", 0);
                },
                ["TNN_CX_TUONG CANH"] = s =>
                {
                    SetParamYesNoType(s, "CX_TUONG CANH", 1);
                    SetParamYesNoType(s, "CX_TUONG DAU", 0);
                    SetParamYesNoType(s, "CX_SAN CONG", 0);
                    SetParamYesNoType(s, "CX_BE TONG LOT", 0);
                    SetParamYesNoType(s, "CX_DA DAM DEM", 0);
                    SetParamYesNoType(s, "TD_BE TONG LOT", 0);
                    SetParamYesNoType(s, "TD_DA DAM DEM", 0);
                },
                ["TNN_CX_SAN CONG"] = s =>
                {
                    SetParamYesNoType(s, "CX_SAN CONG", 1);
                    SetParamYesNoType(s, "CX_TUONG DAU", 0);
                    SetParamYesNoType(s, "CX_TUONG CANH", 0);
                    SetParamYesNoType(s, "CX_BE TONG LOT", 0);
                    SetParamYesNoType(s, "CX_DA DAM DEM", 0);
                    SetParamYesNoType(s, "TD_BE TONG LOT", 0);
                    SetParamYesNoType(s, "TD_DA DAM DEM", 0);
                },
                ["TNN_CX_BE TONG LOT"] = s =>
                {
                    SetParamYesNoType(s, "CX_BE TONG LOT", 1);
                    SetParamYesNoType(s, "CX_TUONG DAU", 0);
                    SetParamYesNoType(s, "CX_TUONG CANH", 0);
                    SetParamYesNoType(s, "CX_SAN CONG", 0);
                    SetParamYesNoType(s, "CX_DA DAM DEM", 0);
                    SetParamYesNoType(s, "TD_BE TONG LOT", 0);
                    SetParamYesNoType(s, "TD_DA DAM DEM", 0);
                },
                ["TNN_CX_DA DAM DEM"] = s =>
                {
                    SetParamYesNoType(s, "CX_DA DAM DEM", 1);
                    SetParamYesNoType(s, "CX_TUONG DAU", 0);
                    SetParamYesNoType(s, "CX_TUONG CANH", 0);
                    SetParamYesNoType(s, "CX_SAN CONG", 0);
                    SetParamYesNoType(s, "CX_BE TONG LOT", 0);
                    SetParamYesNoType(s, "TD_BE TONG LOT", 0);
                    SetParamYesNoType(s, "TD_DA DAM DEM", 0);
                }
            };

            foreach (var kvp in neededTypes)
            {
                string typeName = kvp.Key;
                string shortName = typeName.Replace("TNN_CX_", "");
                var existing = existingSymbols.FirstOrDefault(s =>
                    s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                    (s.Name.IndexOf(shortName, StringComparison.OrdinalIgnoreCase) >= 0 &&
                     (shortName != "SAN CONG" || (!s.Name.Contains("TUONG") && !s.Name.Contains("BE TONG") && !s.Name.Contains("DA DAM")))));

                if (existing == null)
                {
                    try
                    {
                        var newSym = (FamilySymbol)baseSym.Duplicate(typeName);
                        doc.Regenerate();
                        ActivateSymbol(newSym);
                        kvp.Value(newSym);
                        existingSymbols.Add(newSym);
                    }
                    catch { }
                }
                else
                {
                    try { kvp.Value(existing); } catch { }
                }
            }

            foreach (var comp in outletComponents)
            {
                string cat = (comp.CategoryType ?? "").ToUpperInvariant();
                string targetType = string.Empty;
                if (cat.Contains("TƯỜNG ĐẦU") || cat.Contains("TUONG DAU")) targetType = "TNN_CX_TUONG DAU";
                else if (cat.Contains("TƯỜNG CÁNH") || cat.Contains("TUONG CANH")) targetType = "TNN_CX_TUONG CANH";
                else if (cat.Contains("BÊ TÔNG LÓT") || cat.Contains("BE TONG LOT") || cat.Contains("BTL") || cat.Contains("LÓT") || cat.Contains("LOT")) targetType = "TNN_CX_BE TONG LOT";
                else if (cat.Contains("ĐÁ DĂM") || cat.Contains("DA DAM") || cat.Contains("ĐỆM") || cat.Contains("DEM")) targetType = "TNN_CX_DA DAM DEM";
                else if (cat.Contains("SÂN CỐNG") || cat.Contains("SAN CONG") || cat.Contains("BẢN ĐÁY") || cat.Contains("BAN DAY")) targetType = "TNN_CX_SAN CONG";

                if (!string.IsNullOrEmpty(targetType))
                {
                    string shortTarget = targetType.Replace("TNN_CX_", "");
                    var match = existingSymbols.FirstOrDefault(s =>
                        s.Name.Equals(targetType, StringComparison.OrdinalIgnoreCase) ||
                        (s.Name.IndexOf(shortTarget, StringComparison.OrdinalIgnoreCase) >= 0 &&
                         (shortTarget != "SAN CONG" || (!s.Name.Contains("TUONG") && !s.Name.Contains("BE TONG") && !s.Name.Contains("DA DAM")))));

                    if (match != null)
                    {
                        comp.SelectedSymbol = new FamilySymbolWrapper(match);
                        ActivateSymbol(match);
                    }
                }
            }
        }

        private static void EnsureApronTypesExist(Document doc, IList<CulvertComponentItem>? apronComponents)
        {
            if (doc == null || apronComponents == null || apronComponents.Count == 0) return;

            FamilySymbol? baseSym = null;
            foreach (var c in apronComponents)
            {
                if (c.SelectedSymbol != null)
                {
                    var s = c.SelectedSymbol.GetFreshSymbol(doc);
                    if (s != null && s.IsValidObject && (s.FamilyName.Contains("SAN GIA CO") || s.FamilyName.Contains("SGC")))
                    {
                        baseSym = s;
                        break;
                    }
                }
            }

            if (baseSym == null)
            {
                baseSym = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => s != null && s.IsValidObject && (s.FamilyName.Contains("SAN GIA CO") || s.FamilyName.Contains("SGC")));
            }

            if (baseSym == null || !baseSym.IsValidObject) return;

            Family? fam = null;
            try { fam = baseSym.Family; } catch { }
            if (fam == null || !fam.IsValidObject) return;

            var existingSymbols = fam.GetFamilySymbolIds()
                .Select(id => doc.GetElement(id) as FamilySymbol)
                .Where(s => s != null && s.IsValidObject)
                .ToList();

            foreach (var comp in apronComponents)
            {
                string cat = (comp.CategoryType ?? "").ToUpperInvariant();
                string targetType = string.Empty;
                if (cat.Contains("LÓT") || cat.Contains("LOT") || cat.Contains("BTL")) targetType = "BE TONG LOT";
                else targetType = "SAN GIA CO";

                var match = existingSymbols.FirstOrDefault(s => s.Name.IndexOf(targetType, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null)
                {
                    comp.SelectedSymbol = new FamilySymbolWrapper(match);
                    ActivateSymbol(match);
                }
            }
        }

        internal static IList<CulvertComponentItem> ResolveComponentsForCulvert(
            Document doc,
            IList<CulvertComponentItem> baseComponents,
            CulvertRowData row)
        {
            if (baseComponents == null || baseComponents.Count == 0) return baseComponents ?? new List<CulvertComponentItem>();

            var list = new List<CulvertComponentItem>();
            var allSymbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(s => s != null && s.IsValidObject)
                .ToList();

            string loaiCong = row.LoaiCong ?? "";
            string cauKien = row.CauKien ?? "";
            string ghiChu = row.GhiChu ?? "";

            foreach (var baseComp in baseComponents)
            {
                var copy = new CulvertComponentItem
                {
                    IsActive = baseComp.IsActive,
                    GroupType = baseComp.GroupType,
                    CategoryType = baseComp.CategoryType,
                    SelectedSymbol = baseComp.SelectedSymbol,
                    OffsetZ = baseComp.OffsetZ,
                    Note = baseComp.Note
                };

                if (copy.IsActive)
                {
                    if (copy.SelectedSymbol != null)
                    {
                        var fresh = copy.SelectedSymbol.GetFreshSymbol(doc);
                        if (fresh != null && fresh.IsValidObject)
                        {
                            copy.SelectedSymbol.Symbol = fresh;
                            ActivateSymbol(fresh);
                        }
                        else
                        {
                            string hint = $"{copy.GroupType} {copy.CategoryType}".Trim();
                            var matchSym = ResolveFamilyForCulvert(allSymbols, loaiCong, cauKien, ghiChu, row.SoCua, row.KhauDo, hint);
                            if (matchSym != null && matchSym.IsValidObject)
                            {
                                copy.SelectedSymbol = new FamilySymbolWrapper(matchSym);
                                ActivateSymbol(matchSym);
                            }
                        }
                    }
                    else
                    {
                        string hint = $"{copy.GroupType} {copy.CategoryType}".Trim();
                        var matchSym = ResolveFamilyForCulvert(allSymbols, loaiCong, cauKien, ghiChu, row.SoCua, row.KhauDo, hint);
                        if (matchSym != null && matchSym.IsValidObject)
                        {
                            copy.SelectedSymbol = new FamilySymbolWrapper(matchSym);
                            ActivateSymbol(matchSym);
                        }
                    }
                }

                list.Add(copy);
            }

            return list;
        }

        private static FamilySymbol? ResolveFamilyForCulvert(
            IEnumerable<FamilySymbol>? symbols,
            string loaiCong,
            string cauKien,
            string ghiChu,
            int soCua,
            string khauDo,
            string partHint)
        {
            if (symbols == null) return null;

            string lcUpper = $"{loaiCong} {cauKien} {ghiChu}".Trim().ToUpperInvariant();
            string kdUpper = (khauDo ?? "").Trim().ToUpperInvariant();
            string hintUpper = (partHint ?? "").Trim().ToUpperInvariant();

            bool isDoTaiCho = soCua > 1 || lcUpper.Contains("ĐỔ TẠI CHỖ") || lcUpper.Contains("DO TAI CHO");

            // Phân loại nhóm cấu kiện tìm kiếm:
            bool isSearchingOutlet = hintUpper.Contains("CỬA XẢ") || hintUpper.Contains("CUA XA") ||
                                     hintUpper.Contains("SÂN CỐNG") || hintUpper.Contains("SAN CONG") ||
                                     hintUpper.Contains("TƯỜNG ĐẦU") || hintUpper.Contains("TUONG DAU") ||
                                     hintUpper.Contains("TƯỜNG CÁNH") || hintUpper.Contains("TUONG CANH");

            bool isSearchingApron = hintUpper.Contains("SÂN GIA CỐ") || hintUpper.Contains("SAN GIA CO") || hintUpper.Contains("SGC");

            bool isSearchingManhole = hintUpper.Contains("HỘP NỐI") || hintUpper.Contains("HOP NOI") ||
                                      hintUpper.Contains("HỐ GA") || hintUpper.Contains("HO GA") ||
                                      hintUpper.Contains("CỔ GIẾNG") || hintUpper.Contains("CO GIENG") ||
                                      hintUpper.Contains("KHUÔN HẦM") || hintUpper.Contains("KHUON HAM") ||
                                      hintUpper.Contains("NẮP ĐAN") || hintUpper.Contains("NAP DAN");

            bool isSearchingBarrel = !isSearchingOutlet && !isSearchingApron && !isSearchingManhole;

            var candidates = new List<(FamilySymbol Symbol, int Score)>();

            foreach (var sym in symbols)
            {
                if (sym == null || !sym.IsValidObject) continue;
                string fam = string.Empty;
                string name = string.Empty;
                try
                {
                    fam = sym.FamilyName?.ToUpperInvariant() ?? "";
                    name = sym.Name?.ToUpperInvariant() ?? "";
                }
                catch { continue; }
                string full = $"{fam} {name}";
                int score = 0;

                if (isSearchingOutlet)
                {
                    if (full.Contains("TNN_CH") || full.Contains("THAN CONG") || full.Contains("HOP NOI") || full.Contains("TNM_HG") || full.Contains("SAN GIA CO") || full.Contains("SGC"))
                        continue;
                    if (!full.Contains("TNN_CX") && !full.Contains("SAN CONG") && !full.Contains("CUA XA"))
                        continue;

                    score += 50;

                    if (hintUpper.Contains("TƯỜNG ĐẦU") || hintUpper.Contains("TUONG DAU"))
                    {
                        if (full.Contains("TUONG DAU")) score += 30;
                    }
                    else if (hintUpper.Contains("TƯỜNG CÁNH") || hintUpper.Contains("TUONG CANH"))
                    {
                        if (full.Contains("TUONG CANH")) score += 30;
                    }
                    else if (hintUpper.Contains("BÊ TÔNG LÓT") || hintUpper.Contains("BE TONG LOT") || hintUpper.Contains("BTL") || hintUpper.Contains("LÓT") || hintUpper.Contains("LOT"))
                    {
                        if (full.Contains("BE TONG LOT") || full.Contains("BTL")) score += 30;
                    }
                    else if (hintUpper.Contains("ĐÁ DĂM") || hintUpper.Contains("DA DAM") || hintUpper.Contains("ĐỆM") || hintUpper.Contains("DEM"))
                    {
                        if (full.Contains("DA DAM")) score += 30;
                    }
                    else if (hintUpper.Contains("SÂN CỐNG") || hintUpper.Contains("SAN CONG") || hintUpper.Contains("BẢN ĐÁY") || hintUpper.Contains("BAN DAY"))
                    {
                        if (name.Contains("SAN CONG") && !name.Contains("TUONG") && !name.Contains("BE TONG") && !name.Contains("DA DAM")) score += 30;
                    }
                }
                else if (isSearchingApron)
                {
                    if (full.Contains("TNN_CH") || full.Contains("THAN CONG") || full.Contains("HOP NOI") || full.Contains("TNM_HG"))
                        continue;
                    if (!full.Contains("SAN GIA CO") && !full.Contains("SGC"))
                        continue;

                    score += 50;

                    if (hintUpper.Contains("LÓT") || hintUpper.Contains("LOT") || hintUpper.Contains("BTL"))
                    {
                        if (full.Contains("LOT") || full.Contains("BTL")) score += 30;
                    }
                    else
                    {
                        if (!full.Contains("LOT") && !full.Contains("BTL")) score += 30;
                    }
                }
                else if (isSearchingManhole)
                {
                    if (full.Contains("TNN_CH") || full.Contains("TNN_CX") || full.Contains("SAN CONG") || full.Contains("SAN GIA CO"))
                        continue;
                    if (!full.Contains("HOP NOI") && !full.Contains("HỘP NỐI") && !full.Contains("TNM_HG") && !full.Contains("HO GA") && !full.Contains("HỐ GA") && !full.Contains("HO KY THUAT"))
                        continue;

                    score += 50;
                    if (hintUpper.Contains("HỐ KỸ THUẬT") || hintUpper.Contains("HO KY THUAT"))
                    {
                        if (full.Contains("HO KY THUAT")) score += 50;
                    }
                    else if (hintUpper.Contains("CỔ GIẾNG") && full.Contains("CO GIENG")) score += 30;
                    else if (hintUpper.Contains("KHUÔN") && full.Contains("KHUON")) score += 30;
                    else if (hintUpper.Contains("NẮP") && full.Contains("NAP")) score += 30;
                    else if (hintUpper.Contains("LÓT") && (full.Contains("LOT") || full.Contains("BTL"))) score += 30;
                    else if (hintUpper.Contains("HỘP NỐI") && (full.Contains("HOP NOI") || full.Contains("HỘP NỐI"))) score += 30;
                }
                else // isSearchingBarrel
                {
                    if (full.Contains("TNN_CX") || full.Contains("CUA XA") || full.Contains("SAN CONG") ||
                        full.Contains("SAN GIA CO") || full.Contains("SGC") ||
                        full.Contains("HOP NOI") || full.Contains("HỘP NỐI") || full.Contains("TNM_HG") || full.Contains("HO GA") || full.Contains("HO KY THUAT"))
                        continue;

                    if (!full.Contains("TNN_CH") && !full.Contains("TNN_CT") && !full.Contains("CONG HOP") && !full.Contains("THAN CONG") && !full.Contains("TNN_CKT") && !full.Contains("CKT"))
                        continue;

                    score += 50;
                    bool isCktMode = lcUpper.Contains("KỸ THUẬT") || lcUpper.Contains("KY THUAT") || hintUpper.Contains("CKT");
                    if (isCktMode && (full.Contains("TNN_CKT") || full.Contains("CKT"))) score += 40;

                    if (hintUpper.Contains("ĐÁ DĂM") || hintUpper.Contains("DA DAM") || hintUpper.Contains("CPDD"))
                    {
                        if (!full.Contains("DA DAM") && !full.Contains("ĐÁ DĂM") && !full.Contains("CPDD")) continue;
                        score += 40;
                        if (isDoTaiCho && full.Contains("2X3X2")) score += 20;
                        else if (!isDoTaiCho && !full.Contains("2X3X2")) score += 20;
                    }
                    else if (hintUpper.Contains("BTL") || hintUpper.Contains("LÓT") || hintUpper.Contains("LOT") || hintUpper.Contains("ĐỆM") || hintUpper.Contains("DEM"))
                    {
                        if (!full.Contains("BE TONG LOT") && !full.Contains("BTL") && !full.Contains("DEM CONG") && !full.Contains("LOT")) continue;
                        if (full.Contains("DA DAM")) continue;
                        score += 40;
                        if (isDoTaiCho && (full.Contains("2X3X2") || full.Contains("DEM CONG"))) score += 20;
                        else if (!isDoTaiCho && !full.Contains("2X3X2")) score += 20;
                    }
                    else
                    {
                        if (!full.Contains("THAN CONG") && !full.Contains("THÂN CỐNG") && !full.Contains("CONG HOP")) continue;
                        if (full.Contains("LOT") || full.Contains("DEM") || full.Contains("BTL") || full.Contains("DA DAM")) continue;
                        score += 40;
                        if (isDoTaiCho && full.Contains("2X3X2")) score += 20;
                        else if (!isDoTaiCho && !full.Contains("2X3X2")) score += 20;
                    }
                }

                // 2. Khớp khẩu độ hình học (KhauDo, vd: 1.5x1.5, 2x3x2...)
                if (!string.IsNullOrEmpty(kdUpper))
                {
                    string cleanKd = kdUpper.Replace("*", "X").Replace(" ", "");
                    string cleanFull = full.Replace("*", "X").Replace(" ", "");
                    if (cleanFull.Contains(cleanKd)) score += 25;
                }

                candidates.Add((sym, score));
            }

            var best = candidates.OrderByDescending(c => c.Score).FirstOrDefault();
            return best.Score > 0 ? best.Symbol : null;
        }

        /// <summary>
        /// Xây dựng 1 cụm cống ngang theo chuẩn 4 cụm cấu kiện (Thân cống, Cửa xả, Sân gia cố, Hố ga)
        /// </summary>
        public static void BuildSingleCulvertV2(
            Document doc,
            CulvertRowData data,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IList<CulvertComponentItem> barrelComponents,
            IList<CulvertComponentItem> outletComponents,
            IList<CulvertComponentItem> apronComponents,
            IList<CulvertComponentItem> manholeComponents,
            double lStdM,
            double jointGapM,
            double bBoxM,
            double defaultKhoangCachTim,
            CulvertArrayMode arrayMode,
            bool isCastInPlace,
            bool useSurveyPoint,
            IEnumerable<CulvertMaterialItem>? materialSettings = null,
            List<FamilyInstance>? outCreatedOutlets = null,
            List<FamilyInstance>? outCreatedBarrels = null,
            List<FamilyInstance>? outCreatedBeddings = null,
            List<FamilyInstance>? outCreatedAprons = null,
            List<FamilyInstance>? outCreatedManholes = null,
            bool performCutGeometry = false,
            List<FamilyInstance>? outUpstreamOutlets = null,
            List<FamilyInstance>? outDownstreamOutlets = null,
            CastInPlaceDetails? outCipDetails = null)
        {
            // 1. Chuyển đổi tọa độ VN2000 sang Revit Internal (Feet)
            XYZ p1 = CoordinateService.ConvertVN2000ToRevitInternal(doc, data.X1, data.Y1, data.Z1, useSurveyPoint);
            XYZ p2 = CoordinateService.ConvertVN2000ToRevitInternal(doc, data.X2, data.Y2, data.Z2, useSurveyPoint);

            XYZ v3D = p2 - p1;
            double totalLengthFeet = v3D.GetLength();
            if (totalLengthFeet < 0.01)
                throw new InvalidOperationException("Chiều dài cống quá nhỏ (P1 trùng P2).");

            // KIỂM TRA NẾU LÀ CỐNG KỸ THUẬT: ĐIỀU HƯỚNG SANG THUẬT TOÁN RẢI CỐNG KỸ THUẬT RIÊNG
            bool isTechnicalCulvert = data.DetermineCulvertType() == "CỐNG KỸ THUẬT" || 
                data.LoaiCong?.ToUpperInvariant().Contains("KỸ THUẬT") == true || 
                data.LoaiCong?.ToUpperInvariant().Contains("KY THUAT") == true || 
                data.LoaiCong?.ToUpperInvariant().Contains("CKT") == true ||
                data.GhiChu?.ToUpperInvariant().Contains("KỸ THUẬT") == true || 
                data.GhiChu?.ToUpperInvariant().Contains("KY THUAT") == true ||
                data.GhiChu?.ToUpperInvariant().Contains("CKT") == true ||
                (manholeComponents != null && manholeComponents.Any(c => 
                    c.GroupType?.IndexOf("Hố kỹ thuật", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.CategoryType?.IndexOf("Hố kỹ thuật", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.SelectedSymbol?.Symbol?.FamilyName?.Contains("HO KY THUAT") == true ||
                    c.SelectedSymbol?.Symbol?.FamilyName?.Contains("CKT") == true));

            if (isTechnicalCulvert)
            {
                BuildSingleTechnicalCulvert(
                    doc,
                    data,
                    bimConfig,
                    customBimParams,
                    familyParameterMappings,
                    barrelComponents,
                    manholeComponents,
                    lStdM,
                    jointGapM,
                    useSurveyPoint,
                    materialSettings,
                    outCreatedBarrels,
                    outCreatedBeddings,
                    outCreatedManholes,
                    performCutGeometry);
                return;
            }

            XYZ u = v3D.Normalize();
            double rotAngle = Math.Atan2(u.Y, u.X);

            double lStdFeet = UnitUtils.ConvertToInternalUnits(lStdM, UnitTypeId.Meters);
            double jointGapFeet = UnitUtils.ConvertToInternalUnits(jointGapM, UnitTypeId.Meters);
            double bBoxFeet = UnitUtils.ConvertToInternalUnits(bBoxM > 0 ? bBoxM : 1.50, UnitTypeId.Meters);

            // 2. Đặt Cửa xả Thượng lưu (P1) & Hạ lưu (P2)
            double rotOutletTL = rotAngle + Math.PI / 2.0;
            double rotOutletHL = rotAngle - Math.PI / 2.0;

            // 2. Xác định chiều dài sân cống (CX_L san cong) từ thiết lập tham số, Family hoặc mặc định 2.16m theo CAD
            double lSanCongM = 2.16;
            var mappingL = familyParameterMappings?.FirstOrDefault(m => 
                string.Equals(m.InternalName, "CX_L san cong", StringComparison.OrdinalIgnoreCase) ||
                m.InternalName.IndexOf("L san cong", StringComparison.OrdinalIgnoreCase) >= 0 ||
                string.Equals(m.InternalName, "L_SAN_CONG", StringComparison.OrdinalIgnoreCase));
            if (mappingL != null && !string.IsNullOrWhiteSpace(mappingL.CustomValue))
            {
                string valStrL = data.GetParamOverride(mappingL.InternalName, mappingL.CustomValue);
                if (double.TryParse(valStrL.Replace("mm", "").Replace("m", "").Trim(), out double dValL))
                {
                    lSanCongM = (Math.Abs(dValL) >= 10.0) ? dValL / 1000.0 : dValL;
                }
            }
            if (lSanCongM <= 0.1 && data.ParameterOverrides != null)
            {
                foreach (var kvp in data.ParameterOverrides)
                {
                    if (kvp.Key.IndexOf("L san cong", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        kvp.Key.IndexOf("L_SAN_CONG", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (double.TryParse(kvp.Value.Replace("mm", "").Replace("m", "").Trim(), out double dValL))
                        {
                            lSanCongM = (Math.Abs(dValL) >= 10.0) ? dValL / 1000.0 : dValL;
                            if (lSanCongM > 0.1) break;
                        }
                    }
                }
            }
            if (lSanCongM <= 0.1)
            {
                var outletSampleComp = outletComponents.FirstOrDefault(c => c.IsActive && c.SelectedSymbol != null);
                if (outletSampleComp?.SelectedSymbol != null)
                {
                    var oSym = outletSampleComp.SelectedSymbol.GetFreshSymbol(doc);
                    if (oSym != null)
                    {
                        var pL = oSym.LookupParameter("CX_L san cong") ?? oSym.LookupParameter("L_SAN_CONG") ?? oSym.LookupParameter("L");
                        if (pL != null && pL.StorageType == StorageType.Double && pL.AsDouble() > 0.1)
                        {
                            lSanCongM = UnitUtils.ConvertFromInternalUnits(pL.AsDouble(), UnitTypeId.Meters);
                        }
                    }
                }
            }
            if (lSanCongM <= 0.1)
            {
                var sampleOInst = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .FirstOrDefault(fi => fi.Symbol != null && (fi.Symbol.FamilyName.Contains("SAN CONG") || fi.Symbol.FamilyName.Contains("CUA XA")));
                if (sampleOInst != null)
                {
                    var pL = sampleOInst.LookupParameter("CX_L san cong") ?? sampleOInst.LookupParameter("L_SAN_CONG") ?? sampleOInst.LookupParameter("L");
                    if (pL != null && pL.StorageType == StorageType.Double && pL.AsDouble() > 0.1)
                    {
                        lSanCongM = UnitUtils.ConvertFromInternalUnits(pL.AsDouble(), UnitTypeId.Meters);
                    }
                }
            }
            if (lSanCongM <= 0.1)
            {
                lSanCongM = 2.16;
            }

            double lSanCongFeet = UnitUtils.ConvertToInternalUnits(lSanCongM, UnitTypeId.Meters);

            var outletInstances = outCreatedOutlets ?? new List<FamilyInstance>();
            var barrelInstances = outCreatedBarrels ?? new List<FamilyInstance>();
            var beddingInstances = outCreatedBeddings ?? new List<FamilyInstance>();
            var apronInstances = outCreatedAprons ?? new List<FamilyInstance>();
            var manholeInstances = outCreatedManholes ?? new List<FamilyInstance>();

            var upstreamOutlets = new List<FamilyInstance>();
            var downstreamOutlets = new List<FamilyInstance>();

            PlaceOutletAssembly(doc, p1, u, rotOutletTL, outletComponents, bimConfig, customBimParams, familyParameterMappings, data, isUpstream: true, lSanCongFeet, materialSettings, upstreamOutlets);
            PlaceOutletAssembly(doc, p2, u, rotOutletHL, outletComponents, bimConfig, customBimParams, familyParameterMappings, data, isUpstream: false, lSanCongFeet, materialSettings, downstreamOutlets);

            outletInstances.AddRange(upstreamOutlets);
            outletInstances.AddRange(downstreamOutlets);
            outUpstreamOutlets?.AddRange(upstreamOutlets);
            outDownstreamOutlets?.AddRange(downstreamOutlets);

            // 2b. Xác định chiều dày/chiều dài móng tường đầu (CX_L1) từ thiết lập tham số, Family hoặc mặc định 1.30m
            double cxL1M = 1.30;
            var mappingL1 = familyParameterMappings?.FirstOrDefault(m => 
                string.Equals(m.InternalName, "CX_L1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.InternalName, "L1", StringComparison.OrdinalIgnoreCase));
            if (mappingL1 != null && !string.IsNullOrWhiteSpace(mappingL1.CustomValue))
            {
                string valStrL1 = data.GetParamOverride(mappingL1.InternalName, mappingL1.CustomValue);
                if (double.TryParse(valStrL1.Replace("mm", "").Replace("m", "").Trim(), out double dValL1))
                {
                    cxL1M = (Math.Abs(dValL1) >= 10.0) ? dValL1 / 1000.0 : dValL1;
                }
            }
            if (cxL1M <= 0.05 && data.ParameterOverrides != null)
            {
                foreach (var kvp in data.ParameterOverrides)
                {
                    if (kvp.Key.IndexOf("CX_L1", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (kvp.Key.Contains("Cửa xả", StringComparison.OrdinalIgnoreCase) && kvp.Key.EndsWith("::L1", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (double.TryParse(kvp.Value.Replace("mm", "").Replace("m", "").Trim(), out double dValL1))
                        {
                            cxL1M = (Math.Abs(dValL1) >= 10.0) ? dValL1 / 1000.0 : dValL1;
                            if (cxL1M > 0.05) break;
                        }
                    }
                }
            }
            if (cxL1M <= 0.05)
            {
                var outletSampleComp = outletComponents.FirstOrDefault(c => c.IsActive && c.SelectedSymbol != null);
                if (outletSampleComp?.SelectedSymbol != null)
                {
                    var oSym = outletSampleComp.SelectedSymbol.GetFreshSymbol(doc);
                    if (oSym != null)
                    {
                        var pL1 = oSym.LookupParameter("CX_L1") ?? oSym.LookupParameter("L1");
                        if (pL1 != null && pL1.StorageType == StorageType.Double && pL1.AsDouble() > 0.05)
                        {
                            cxL1M = UnitUtils.ConvertFromInternalUnits(pL1.AsDouble(), UnitTypeId.Meters);
                        }
                    }
                }
            }
            if (cxL1M <= 0.05)
            {
                var sampleOInst = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .FirstOrDefault(fi => fi.Symbol != null && (fi.Symbol.FamilyName.Contains("SAN CONG") || fi.Symbol.FamilyName.Contains("CUA XA")));
                if (sampleOInst != null)
                {
                    var pL1 = sampleOInst.LookupParameter("CX_L1") ?? sampleOInst.LookupParameter("L1");
                    if (pL1 != null && pL1.StorageType == StorageType.Double && pL1.AsDouble() > 0.05)
                    {
                        cxL1M = UnitUtils.ConvertFromInternalUnits(pL1.AsDouble(), UnitTypeId.Meters);
                    }
                }
            }
            if (cxL1M <= 0.05)
            {
                cxL1M = 1.30;
            }

            // 3. Đặt Sân gia cố Thượng lưu & Hạ lưu:
            // Sân gia cố liền kề ngay mép ngoài của Sân cống (CX_L1 + lSanCongM), không có khoảng cách hở
            double lOuterCX_M = cxL1M + lSanCongM;
            double lOuterCX_Feet = UnitUtils.ConvertToInternalUnits(lOuterCX_M, UnitTypeId.Meters);

            XYZ uOutTL = -new XYZ(u.X, u.Y, 0).Normalize();
            XYZ uOutHL = new XYZ(u.X, u.Y, 0).Normalize();
            XYZ ptSC2_TL = p1 + uOutTL * lOuterCX_Feet;
            XYZ ptSC2_HL = p2 + uOutHL * lOuterCX_Feet;

            double lSgcTL = data.L_SGC_TL > 0.05 ? data.L_SGC_TL : 3.0;
            double lSgcHL = data.L_SGC_HL > 0.05 ? data.L_SGC_HL : 3.0;

            PlaceApronAssembly(doc, ptSC2_TL, u, apronComponents, bimConfig, customBimParams, familyParameterMappings, data, isUpstream: true, lSgcTL, materialSettings, apronInstances);
            PlaceApronAssembly(doc, ptSC2_HL, u, apronComponents, bimConfig, customBimParams, familyParameterMappings, data, isUpstream: false, lSgcHL, materialSettings, apronInstances);

            // 4. Rải thân cống và hộp nối: Offset xuống 1 đoạn L_ngam_san như trong file Excel & Hình 4
            double lNgamSanM = data.L_Ngam_San > 0 ? data.L_Ngam_San : 0.0;
            double lNgamSanFeet = UnitUtils.ConvertToInternalUnits(lNgamSanM, UnitTypeId.Meters);
            XYZ p1_Culvert = p1 - new XYZ(0, 0, lNgamSanFeet);
            XYZ p2_Culvert = p2 - new XYZ(0, 0, lNgamSanFeet);

            bool isRowCastInPlace = isCastInPlace || 
                (data.LoaiCong?.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) >= 0) ||
                (data.GhiChu?.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) >= 0);

            var sampleOutlet = upstreamOutlets.FirstOrDefault() ?? downstreamOutlets.FirstOrDefault();
            var cipDetails = outCipDetails ?? new CastInPlaceDetails();
            if (isRowCastInPlace)
            {
                BuildCastInPlaceBoxCulvert(
                    doc,
                    data,
                    bimConfig,
                    customBimParams,
                    familyParameterMappings,
                    barrelComponents,
                    p1_Culvert,
                    p2_Culvert,
                    u,
                    rotAngle,
                    totalLengthFeet,
                    lStdFeet,
                    jointGapFeet,
                    arrayMode,
                    materialSettings,
                    barrelInstances,
                    beddingInstances,
                    cipDetails,
                    outletComponents,
                    sampleOutlet);
            }
            else
            {
                BuildBranchV2(doc, data, bimConfig, customBimParams, familyParameterMappings, barrelComponents, manholeComponents,
                    p1_Culvert, p2_Culvert, u, rotAngle, totalLengthFeet, lStdFeet, jointGapFeet, bBoxFeet, arrayMode, "", materialSettings,
                    barrelInstances, beddingInstances, manholeInstances, outletComponents, sampleOutlet);
            }

            // 5. YÊU CẦU 1 & 2 & 4: Cut Geometry toàn diện giữa Cửa xả (Tường đầu), Thân cống hộp, BTL và Đá dăm đệm (Hình 1, 2, 3, 4)
            // CHỈ THỰC HIỆN KHI performCutGeometry == true (khi bấm "Khởi tạo và thực hiện mô hình" hoặc Review 3D trích xuất mesh)
            if (performCutGeometry)
            {
                ExecuteAllCulvertCuts(
                    doc,
                    isRowCastInPlace,
                    upstreamOutlets,
                    downstreamOutlets,
                    barrelInstances,
                    beddingInstances,
                    manholeInstances,
                    cipDetails,
                    apronInstances);
            }

            // Gán Mark (CN-STT), Comments và BIM_LyTrinh cho toàn bộ cấu kiện Cống Ngang vừa tạo
            var allCreatedCN = new List<FamilyInstance>();
            if (outCreatedOutlets != null) allCreatedCN.AddRange(outCreatedOutlets);
            if (upstreamOutlets != null) allCreatedCN.AddRange(upstreamOutlets);
            if (downstreamOutlets != null) allCreatedCN.AddRange(downstreamOutlets);
            allCreatedCN.AddRange(barrelInstances);
            allCreatedCN.AddRange(beddingInstances);
            allCreatedCN.AddRange(apronInstances);
            allCreatedCN.AddRange(manholeInstances);

            string cnMark = $"CN-{data.STT}";
            string cnComm = $"Cống {data.STT} - {data.LoaiCong} ({data.LyTrinh})";
            foreach (var fi in allCreatedCN)
            {
                if (fi == null || !fi.IsValidObject) continue;
                try
                {
                    var pM = fi.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    if (pM != null && !pM.IsReadOnly) pM.Set(cnMark);
                    var pC = fi.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (pC != null && !pC.IsReadOnly) pC.Set(cnComm);
                    var pL = fi.LookupParameter("BIM_LyTrinh") ?? fi.LookupParameter("Lý trình");
                    if (pL != null && !pL.IsReadOnly) pL.Set(data.LyTrinh ?? "");
                }
                catch { }
            }
        }

        private static void ApplyBimParametersToInstance(
            FamilyInstance inst,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            CulvertRowData data,
            string tenCauKien)
        {
            if (inst == null) return;
            try { BimParameterService.SetElementBimProperties(inst, bimConfig, tenCauKien, tenCauKien, null, null, null, null, null); } catch { }
            try { BimParameterService.ApplyCustomBimParameters(inst, customBimParams, data, tenCauKien); } catch { }
        }

        /// <summary>
        /// Thuật toán rải Cống Kỹ Thuật chuyên biệt theo đúng yêu cầu:
        /// 1. Định vị 2 Hố kỹ thuật tại P1 & P2, dịch vào trong tuyến đoạn = (THAN GIENG_B / 2) - THAN GIENG_B THANH, HKT2 đối xứng.
        /// 2. Định vị Hộp nối cống ở giữa theo KC_HN1 đo từ P1 đến tâm hộp nối.
        /// 3. Rải đốt cống hộp từ P1 đến thành trong hộp nối, và từ P2 đến thành trong hộp nối (đúng L điển hình, không co dãn).
        /// 4. Rải BTL & Đá dăm đệm từ P1 đến mép ngoài hộp nối, và từ P2 đến mép ngoài hộp nối.
        /// 5. Cut Geometry:
        ///    - Hố kỹ thuật 1 cut với BTL, Đá dăm đệm tại P1, và cut với đốt cống gần nhất tại P1.
        ///    - Hố kỹ thuật 2 cut với BTL, Đá dăm đệm tại P2, và cut với đốt cống gần nhất tại P2.
        ///    - Hộp nối ở giữa cut với 2 đốt cống gần nhất 2 bên trái & phải.
        /// </summary>
        private static void BuildSingleTechnicalCulvert(
            Document doc,
            CulvertRowData data,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IList<CulvertComponentItem> barrelComponents,
            IList<CulvertComponentItem> manholeComponents,
            double lStdM,
            double jointGapM,
            bool useSurveyPoint,
            IEnumerable<CulvertMaterialItem>? materialSettings = null,
            List<FamilyInstance>? outCreatedBarrels = null,
            List<FamilyInstance>? outCreatedBeddings = null,
            List<FamilyInstance>? outCreatedManholes = null,
            bool performCutGeometry = false)
        {
            // 1. Tọa độ P1 (Thượng lưu) và P2 (Hạ lưu)
            XYZ p1 = CoordinateService.ConvertVN2000ToRevitInternal(doc, data.X1, data.Y1, data.Z1, useSurveyPoint);
            XYZ p2 = CoordinateService.ConvertVN2000ToRevitInternal(doc, data.X2, data.Y2, data.Z2, useSurveyPoint);

            XYZ v3D = p2 - p1;
            double totalLengthFeet = v3D.GetLength();
            if (totalLengthFeet < 0.01)
                throw new InvalidOperationException("Chiều dài cống kỹ thuật quá nhỏ (P1 trùng P2).");

            XYZ u = v3D.Normalize();
            double rotAngle = Math.Atan2(u.Y, u.X);

            // 2. Phân giải các Family Symbols cho Cống kỹ thuật
            CulvertComponentItem? compHKT = null;
            CulvertComponentItem? compHN = null;
            FamilySymbol? symHKT = null;
            FamilySymbol? symHN = null;

            if (manholeComponents != null && manholeComponents.Count > 0)
            {
                // Slot 1: Hố kỹ thuật (ở 2 đầu tuyến)
                compHKT = manholeComponents.FirstOrDefault(c => 
                    (c.GroupType?.IndexOf("Hố", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     c.CategoryType?.IndexOf("Hố", StringComparison.OrdinalIgnoreCase) >= 0) &&
                    c.CategoryType?.IndexOf("Hộp", StringComparison.OrdinalIgnoreCase) < 0) 
                    ?? manholeComponents.FirstOrDefault();

                // Slot 2: Hộp nối cống kỹ thuật (ở giữa tuyến)
                compHN = manholeComponents.FirstOrDefault(c => 
                    c != compHKT && (
                        c.GroupType?.IndexOf("Hộp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        c.CategoryType?.IndexOf("Hộp", StringComparison.OrdinalIgnoreCase) >= 0))
                    ?? (manholeComponents.Count > 1 ? manholeComponents[1] : null);

                if (compHKT != null && compHKT.IsActive && compHKT.SelectedSymbol != null)
                {
                    symHKT = compHKT.SelectedSymbol.GetFreshSymbol(doc);
                }
                if (compHN != null && compHN.IsActive && compHN.SelectedSymbol != null)
                {
                    symHN = compHN.SelectedSymbol.GetFreshSymbol(doc);
                }
            }

            CulvertComponentItem? compBarrel = null;
            CulvertComponentItem? compBTL = null;
            CulvertComponentItem? compDDD = null;
            FamilySymbol? symBarrel = null;
            FamilySymbol? symBTL = null;
            FamilySymbol? symDDD = null;

            double offsetZBtlM = -0.10;
            double offsetZDddM = -0.20;

            if (barrelComponents != null)
            {
                foreach (var c in barrelComponents)
                {
                    if (!c.IsActive || c.SelectedSymbol == null) continue;
                    var sym = c.SelectedSymbol.GetFreshSymbol(doc);
                    if (sym == null) continue;
                    string cat = c.CategoryType?.ToUpperInvariant() ?? "";
                    string fn = sym.FamilyName?.ToUpperInvariant() ?? "";

                    if (cat.Contains("BÊ TÔNG LÓT") || cat.Contains("BE TONG LOT") || cat.Contains("BTL") || fn.Contains("BE TONG LOT"))
                    {
                        compBTL = c;
                        symBTL = sym;
                        offsetZBtlM = c.OffsetZ;
                    }
                    else if (cat.Contains("ĐÁ DĂM") || cat.Contains("DA DAM") || cat.Contains("DDD") || fn.Contains("DA DAM"))
                    {
                        compDDD = c;
                        symDDD = sym;
                        offsetZDddM = c.OffsetZ;
                    }
                    else
                    {
                        compBarrel = c;
                        symBarrel = sym;
                    }
                }
            }

            // Fallback tìm kiếm trong dự án nếu Symbol chưa được gán
            var allProjectSymbols = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().ToList();
            if (symHKT == null)
            {
                symHKT = allProjectSymbols.FirstOrDefault(s => (s.FamilyName?.Contains("TNN_CKT_HO KY THUAT") == true || s.FamilyName?.Contains("HO KY THUAT") == true));
            }
            if (symHN == null)
            {
                symHN = allProjectSymbols.FirstOrDefault(s => (s.FamilyName?.Contains("HOP NOI") == true || s.Name?.Contains("HOP NOI") == true));
            }
            if (symBarrel == null)
            {
                symBarrel = allProjectSymbols.FirstOrDefault(s => (s.FamilyName?.Contains("TNN_CKT_THAN CONG") == true))
                            ?? allProjectSymbols.FirstOrDefault(s => (s.FamilyName?.Contains("THAN CONG") == true && !s.FamilyName.Contains("2X3X2")));
            }
            if (symBTL == null)
            {
                symBTL = allProjectSymbols.FirstOrDefault(s => (s.FamilyName?.Contains("TNN_CKT_BE TONG LOT") == true))
                         ?? allProjectSymbols.FirstOrDefault(s => (s.FamilyName?.Contains("BE TONG LOT") == true));
            }
            if (symDDD == null)
            {
                symDDD = allProjectSymbols.FirstOrDefault(s => (s.FamilyName?.Contains("TNN_CKT_DA DAM DEM") == true))
                         ?? allProjectSymbols.FirstOrDefault(s => (s.FamilyName?.Contains("DA DAM DEM") == true));
            }

            // AUTO-LOAD TỰ ĐỘNG NẾU TRONG DỰ ÁN CHƯA CÓ SYMBOL
            const string cktFolder = @"C:\Users\ADMIN\Desktop\TEST TOOL\FAMLY REVIT_HTKT\2. CỐNG KỸ THUẬT";
            if (symHKT == null)
            {
                symHKT = TryAutoLoadFamilySymbol(doc, "HO KY THUAT", null, cktFolder);
            }
            if (symBarrel == null)
            {
                symBarrel = TryAutoLoadFamilySymbol(doc, "TNN_CKT_THAN CONG", null, cktFolder)
                            ?? TryAutoLoadFamilySymbol(doc, "THAN CONG", null, cktFolder);
            }
            if (symBTL == null)
            {
                symBTL = TryAutoLoadFamilySymbol(doc, "TNN_CKT_BE TONG LOT", null, cktFolder)
                         ?? TryAutoLoadFamilySymbol(doc, "BE TONG LOT", null, cktFolder);
            }
            if (symDDD == null)
            {
                symDDD = TryAutoLoadFamilySymbol(doc, "TNN_CKT_DA DAM DEM", null, cktFolder)
                         ?? TryAutoLoadFamilySymbol(doc, "DA DAM DEM", null, cktFolder);
            }
            if (symHN == null)
            {
                symHN = symHKT ?? TryAutoLoadFamilySymbol(doc, "HOP NOI", null, cktFolder);
            }

            if (symHKT != null) ActivateSymbol(symHKT);
            if (symHN != null) ActivateSymbol(symHN);
            if (symBarrel != null) ActivateSymbol(symBarrel);
            if (symBTL != null) ActivateSymbol(symBTL);
            if (symDDD != null) ActivateSymbol(symDDD);

            // 3. Tính độ dịch của Hố kỹ thuật: (THAN GIENG_B / 2) - THAN GIENG_B THANH
            double thanGiengBM = 1.40;
            double thanGiengBThanhM = 0.20;

            if (familyParameterMappings != null)
            {
                var mapB = familyParameterMappings.FirstOrDefault(m => 
                    string.Equals(m.InternalName, "THAN GIENG_B", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.InternalName, "B_GIENG", StringComparison.OrdinalIgnoreCase));
                if (mapB != null && !string.IsNullOrWhiteSpace(mapB.CustomValue))
                {
                    string v = data.GetParamOverride(mapB.InternalName, mapB.CustomValue);
                    if (double.TryParse(v.Replace("mm", "").Replace("m", "").Trim(), out double dVal) && dVal > 0.05)
                    {
                        thanGiengBM = (Math.Abs(dVal) >= 10.0) ? dVal / 1000.0 : dVal;
                    }
                }
                var mapT = familyParameterMappings.FirstOrDefault(m => 
                    string.Equals(m.InternalName, "THAN GIENG_B THANH", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.InternalName, "B_THANH", StringComparison.OrdinalIgnoreCase));
                if (mapT != null && !string.IsNullOrWhiteSpace(mapT.CustomValue))
                {
                    string v = data.GetParamOverride(mapT.InternalName, mapT.CustomValue);
                    if (double.TryParse(v.Replace("mm", "").Replace("m", "").Trim(), out double dVal) && dVal > 0.01)
                    {
                        thanGiengBThanhM = (Math.Abs(dVal) >= 10.0) ? dVal / 1000.0 : dVal;
                    }
                }
            }

            if (symHKT != null)
            {
                var pB = symHKT.LookupParameter("THAN GIENG_B") ?? symHKT.LookupParameter("B_GIENG") ?? symHKT.LookupParameter("B");
                if (pB != null && pB.StorageType == StorageType.Double && pB.AsDouble() > 0.1 && thanGiengBM <= 0.1)
                {
                    thanGiengBM = UnitUtils.ConvertFromInternalUnits(pB.AsDouble(), UnitTypeId.Meters);
                }
                var pThanh = symHKT.LookupParameter("THAN GIENG_B THANH") ?? symHKT.LookupParameter("B_THANH") ?? symHKT.LookupParameter("THANH");
                if (pThanh != null && pThanh.StorageType == StorageType.Double && pThanh.AsDouble() > 0.01 && thanGiengBThanhM <= 0.01)
                {
                    thanGiengBThanhM = UnitUtils.ConvertFromInternalUnits(pThanh.AsDouble(), UnitTypeId.Meters);
                }
            }

            double deltaDM = (thanGiengBM / 2.0) - thanGiengBThanhM;
            if (deltaDM <= 0) deltaDM = 0.50; // default 0.50m
            double deltaDFeet = UnitUtils.ConvertToInternalUnits(deltaDM, UnitTypeId.Meters);
            double thanGiengBFeet = UnitUtils.ConvertToInternalUnits(thanGiengBM, UnitTypeId.Meters);

            // 4. Đặt Hố kỹ thuật 1 (tại P1, dịch ra ngoài theo mũi tên Hình 1: -u * deltaD)
            // và Hố kỹ thuật 2 (tại P2, dịch ra ngoài theo mũi tên Hình 1: +u * deltaD, đối xứng)
            FamilyInstance? instHKT1 = null;
            FamilyInstance? instHKT2 = null;
            XYZ ptHKT1 = p1 - u * deltaDFeet;
            XYZ ptHKT2 = p2 + u * deltaDFeet;

            if (symHKT != null)
            {
                try
                {
                    instHKT1 = CreateInstanceSafe(doc, ptHKT1, symHKT);
                    if (instHKT1 != null)
                    {
                        SafeRotate(doc, instHKT1.Id, ptHKT1, rotAngle);
                        try { BimParameterService.SetElementBimProperties(instHKT1, bimConfig, "Hố kỹ thuật 1", "Hố kỹ thuật", null, null, ptHKT1.X, ptHKT1.Y, ptHKT1.Z); } catch { }
                        try { BimParameterService.ApplyFamilyMappedParameters(instHKT1, familyParameterMappings, data, compHKT?.CategoryType ?? "Hố kỹ thuật"); } catch { }
                        string? hktMat = materialSettings?.FirstOrDefault(m => m.CategoryType == compHKT?.CategoryType)?.MaterialName;
                        try { BimParameterService.ApplyCustomBimParameters(instHKT1, customBimParams, data, compHKT?.CategoryType ?? "Hố kỹ thuật", 1, "P1", hktMat, doc, bimConfig.AutoCreateSharedParameters); } catch { }
                        try { TryApplyMaterial(doc, instHKT1, materialSettings, compHKT?.CategoryType ?? "Hố kỹ thuật"); } catch { }
                        outCreatedManholes?.Add(instHKT1);
                    }
                }
                catch { }

                try
                {
                    instHKT2 = CreateInstanceSafe(doc, ptHKT2, symHKT);
                    if (instHKT2 != null)
                    {
                        // Đặt đối xứng: xoay 180 độ so với hướng tuyến
                        SafeRotate(doc, instHKT2.Id, ptHKT2, rotAngle + Math.PI);
                        try { BimParameterService.SetElementBimProperties(instHKT2, bimConfig, "Hố kỹ thuật 2", "Hố kỹ thuật", null, null, ptHKT2.X, ptHKT2.Y, ptHKT2.Z); } catch { }
                        try { BimParameterService.ApplyFamilyMappedParameters(instHKT2, familyParameterMappings, data, compHKT?.CategoryType ?? "Hố kỹ thuật"); } catch { }
                        string? hktMat2 = materialSettings?.FirstOrDefault(m => m.CategoryType == compHKT?.CategoryType)?.MaterialName;
                        try { BimParameterService.ApplyCustomBimParameters(instHKT2, customBimParams, data, compHKT?.CategoryType ?? "Hố kỹ thuật", 2, "P2", hktMat2, doc, bimConfig.AutoCreateSharedParameters); } catch { }
                        try { TryApplyMaterial(doc, instHKT2, materialSettings, compHKT?.CategoryType ?? "Hố kỹ thuật"); } catch { }
                        outCreatedManholes?.Add(instHKT2);
                    }
                }
                catch { }
            }

            // 5. Đặt Hộp nối cống kỹ thuật (ở giữa tuyến)
            FamilyInstance? instHN = null;
            double kcHnM = (data.KC_HN1 > 0.1) ? data.KC_HN1 : (UnitUtils.ConvertFromInternalUnits(totalLengthFeet, UnitTypeId.Meters) / 2.0);
            double distHnFeet = UnitUtils.ConvertToInternalUnits(kcHnM, UnitTypeId.Meters);
            XYZ ptHN = p1 + u * distHnFeet;

            double bHnM = 0;
            double tThanhM = 0;

            // 1. Kiểm tra mapping Tab 02 hoặc data
            if (data.B_HT1 > 0.1) bHnM = data.B_HT1;
            if (familyParameterMappings != null)
            {
                foreach (var m in familyParameterMappings)
                {
                    string mName = (m.InternalName ?? "").ToUpperInvariant();
                    if (mName == "HOP NOI CONG_B" || mName == "THAN GIENG_B" || mName == "B_BOX" || mName == "B_HT1")
                    {
                        string valStr = data.GetParamOverride(m.InternalName, m.CustomValue);
                        if (double.TryParse(valStr.Replace("mm", "").Replace("m", "").Trim(), out double d) && d > 0.1)
                        {
                            bHnM = (Math.Abs(d) >= 10.0) ? d / 1000.0 : d;
                        }
                    }
                    if (mName == "HOP NOI CONG_T" || mName == "THAN GIENG_B THANH" || mName == "T" || mName == "T_THAN")
                    {
                        string valStr = data.GetParamOverride(m.InternalName, m.CustomValue);
                        if (double.TryParse(valStr.Replace("mm", "").Replace("m", "").Trim(), out double d) && d > 0.01)
                        {
                            tThanhM = (Math.Abs(d) >= 10.0) ? d / 1000.0 : d;
                        }
                    }
                }
            }

            // 2. Tra cứu từ symHN nếu chưa có
            if (bHnM <= 0.1 && symHN != null)
            {
                var pB = symHN.LookupParameter("HOP NOI CONG_B") ?? symHN.LookupParameter("THAN GIENG_B") ?? symHN.LookupParameter("B_BOX") ?? symHN.LookupParameter("B_HT1") ?? symHN.LookupParameter("B");
                if (pB != null && pB.StorageType == StorageType.Double && pB.AsDouble() > 0.1)
                {
                    bHnM = UnitUtils.ConvertFromInternalUnits(pB.AsDouble(), UnitTypeId.Meters);
                }
            }
            if (tThanhM <= 0.01 && symHN != null)
            {
                var pT = symHN.LookupParameter("HOP NOI CONG_T") ?? symHN.LookupParameter("THAN GIENG_B THANH") ?? symHN.LookupParameter("T") ?? symHN.LookupParameter("T_THAN");
                if (pT != null && pT.StorageType == StorageType.Double && pT.AsDouble() > 0.01)
                {
                    tThanhM = UnitUtils.ConvertFromInternalUnits(pT.AsDouble(), UnitTypeId.Meters);
                }
            }

            // 3. Fallback chuẩn theo loại Family:
            if (bHnM <= 0.1)
            {
                bHnM = (symHN?.FamilyName?.Contains("HO KY THUAT") == true) ? 1.90 : 2.10;
            }
            if (tThanhM <= 0.01)
            {
                tThanhM = (symHN?.FamilyName?.Contains("HO KY THUAT") == true) ? 0.20 : 0.25;
            }

            double bHnFeet = UnitUtils.ConvertToInternalUnits(bHnM, UnitTypeId.Meters);
            double tThanhFeet = UnitUtils.ConvertToInternalUnits(tThanhM, UnitTypeId.Meters);

            if (symHN != null)
            {
                try
                {
                    instHN = CreateInstanceSafe(doc, ptHN, symHN);
                    if (instHN != null)
                    {
                        SafeRotate(doc, instHN.Id, ptHN, rotAngle);
                    try { BimParameterService.SetElementBimProperties(instHN, bimConfig, "Hộp nối cống", "Hộp nối cống", null, null, ptHN.X, ptHN.Y, ptHN.Z); } catch { }
                    try { BimParameterService.ApplyFamilyMappedParameters(instHN, familyParameterMappings, data, compHN?.CategoryType ?? "Hộp nối cống"); } catch { }
                    string? hnMat = materialSettings?.FirstOrDefault(m => m.CategoryType == compHN?.CategoryType)?.MaterialName;
                    try { BimParameterService.ApplyCustomBimParameters(instHN, customBimParams, data, compHN?.CategoryType ?? "Hộp nối cống", 1, "HN", hnMat, doc, bimConfig.AutoCreateSharedParameters); } catch { }
                    try { TryApplyMaterial(doc, instHN, materialSettings, compHN?.CategoryType ?? "Hộp nối cống"); } catch { }
                    outCreatedManholes?.Add(instHN);

                    // Tra cứu lại kích thước thực tế từ instHN sau khi gán tham số
                    var pActualB = instHN.LookupParameter("HOP NOI CONG_B") ?? instHN.LookupParameter("THAN GIENG_B") ?? instHN.LookupParameter("B_BOX") ?? instHN.LookupParameter("B_HT1");
                    if (pActualB != null && pActualB.StorageType == StorageType.Double && pActualB.AsDouble() > 0.1)
                    {
                        bHnFeet = pActualB.AsDouble();
                    }
                    var pActualT = instHN.LookupParameter("HOP NOI CONG_T") ?? instHN.LookupParameter("THAN GIENG_B THANH") ?? instHN.LookupParameter("T");
                    if (pActualT != null && pActualT.StorageType == StorageType.Double && pActualT.AsDouble() > 0.01)
                    {
                        tThanhFeet = pActualT.AsDouble();
                    }
                }
            }
            catch { }
        }

            // Mép ngoài và mép thành trong của Hộp nối
            XYZ ptHN_Ngoai1 = ptHN - u * (bHnFeet / 2.0);
            XYZ ptHN_Trong1 = ptHN - u * (bHnFeet / 2.0 - tThanhFeet);

            XYZ ptHN_Trong2 = ptHN + u * (bHnFeet / 2.0 - tThanhFeet);
            XYZ ptHN_Ngoai2 = ptHN + u * (bHnFeet / 2.0);

            // 6. Rải Đốt Cống Hộp Kỹ Thuật (Hình 4):
            // Rải đều với bước chuẩn (lStdFeet + jointGapFeet), chiều dài mỗi đốt đúng L điển hình (không co dãn).
            // Đốt cống cuối cùng tiến về phía hộp nối và đâm xuyên vào khoang hộp nối đúng L điển hình.
            double lStdFeet = UnitUtils.ConvertToInternalUnits(lStdM > 0.1 ? lStdM : 1.0, UnitTypeId.Meters);
            double jointGapFeet = UnitUtils.ConvertToInternalUnits(jointGapM >= 0 ? jointGapM : 0.01, UnitTypeId.Meters);
            double stepFeet = lStdFeet + jointGapFeet;

            bool hasVaiKeGlobal = CheckIfVaiKeEnabled(familyParameterMappings, data);

            FamilyInstance? firstBarrel1 = null;
            FamilyInstance? lastBarrel1 = null;
            FamilyInstance? firstBarrel2 = null;
            FamilyInstance? lastBarrel2 = null;

            if (symBarrel != null)
            {
                // Đoạn 1: Rải từ P1 tiến về phía Hộp nối
                var barrels1 = new List<FamilyInstance>();
                double maxDist1Feet = (ptHN_Trong1 - p1).GetLength();
                int barrelIdx1 = 0;

                while (true)
                {
                    XYZ bStart = p1 + u * (barrelIdx1 * stepFeet);
                    if ((bStart - p1).GetLength() >= maxDist1Feet && barrelIdx1 > 0)
                    {
                        break;
                    }
                    XYZ bEnd = bStart + u * lStdFeet;

                    var bInst = CreateAdaptiveInstanceSafe(doc, symBarrel, new List<XYZ> { bStart, bEnd });
                    if (bInst != null)
                    {
                        string[] vaiKeParams = new[] { 
                            "VAI", "CO VAI KE", "CO_VAI_KE", "VAI KE", "VAI_KE", 
                            "CO VAI", "CO_VAI", "CO TAI", "CO_TAI", "TAI",
                            "TAI CONG", "TAI_CONG", "CỐNG HỘP CÓ TAI", "CONG HOP CO TAI"
                        };
                        foreach (var vkName in vaiKeParams)
                        {
                            var pInst = bInst.LookupParameter(vkName);
                            if (pInst != null && !pInst.IsReadOnly && pInst.StorageType == StorageType.Integer)
                            {
                                pInst.Set(hasVaiKeGlobal ? 1 : 0);
                            }
                        }

                        string tenCK = $"Đốt cống CKT Đ1-{barrelIdx1 + 1}";
                        try { BimParameterService.SetElementBimProperties(bInst, bimConfig, tenCK, "Thân cống", null, null, bStart.X, bStart.Y, bStart.Z); } catch { }
                        try { BimParameterService.ApplyFamilyMappedParameters(bInst, familyParameterMappings, data, compBarrel?.CategoryType ?? "Thân cống"); } catch { }
                        string? cktMat1 = materialSettings?.FirstOrDefault(m => m.CategoryType == compBarrel?.CategoryType)?.MaterialName;
                        try { BimParameterService.ApplyCustomBimParameters(bInst, customBimParams, data, "Thân cống", barrelIdx1 + 1, "Đ1", cktMat1, doc, bimConfig.AutoCreateSharedParameters); } catch { }
                        try { TryApplyMaterial(doc, bInst, materialSettings, compBarrel?.CategoryType ?? "Thân cống"); } catch { }

                        barrels1.Add(bInst);
                        outCreatedBarrels?.Add(bInst);
                    }

                    barrelIdx1++;
                    if (barrelIdx1 > 200 || (bStart - p1).GetLength() >= (ptHN - p1).GetLength())
                        break;
                }
                firstBarrel1 = barrels1.FirstOrDefault();
                lastBarrel1 = barrels1.LastOrDefault();

                // Đoạn 2: Rải từ P2 tiến về phía Hộp nối
                var barrels2 = new List<FamilyInstance>();
                double maxDist2Feet = (ptHN_Trong2 - p2).GetLength();
                int barrelIdx2 = 0;

                while (true)
                {
                    XYZ bStart = p2 - u * (barrelIdx2 * stepFeet);
                    if ((bStart - p2).GetLength() >= maxDist2Feet && barrelIdx2 > 0)
                    {
                        break;
                    }
                    XYZ bEnd = bStart - u * lStdFeet;

                    var bInst = CreateAdaptiveInstanceSafe(doc, symBarrel, new List<XYZ> { bEnd, bStart });
                    if (bInst != null)
                    {
                        string[] vaiKeParams = new[] { 
                            "VAI", "CO VAI KE", "CO_VAI_KE", "VAI KE", "VAI_KE", 
                            "CO VAI", "CO_VAI", "CO TAI", "CO_TAI", "TAI",
                            "TAI CONG", "TAI_CONG", "CỐNG HỘP CÓ TAI", "CONG HOP CO TAI"
                        };
                        foreach (var vkName in vaiKeParams)
                        {
                            var pInst = bInst.LookupParameter(vkName);
                            if (pInst != null && !pInst.IsReadOnly && pInst.StorageType == StorageType.Integer)
                            {
                                pInst.Set(hasVaiKeGlobal ? 1 : 0);
                            }
                        }

                        string tenCK = $"Đốt cống CKT Đ2-{barrelIdx2 + 1}";
                        try { BimParameterService.SetElementBimProperties(bInst, bimConfig, tenCK, "Thân cống", null, null, bEnd.X, bEnd.Y, bEnd.Z); } catch { }
                        try { BimParameterService.ApplyFamilyMappedParameters(bInst, familyParameterMappings, data, compBarrel?.CategoryType ?? "Thân cống"); } catch { }
                        string? cktMat2 = materialSettings?.FirstOrDefault(m => m.CategoryType == compBarrel?.CategoryType)?.MaterialName;
                        try { BimParameterService.ApplyCustomBimParameters(bInst, customBimParams, data, "Thân cống", barrelIdx2 + 1, "Đ2", cktMat2, doc, bimConfig.AutoCreateSharedParameters); } catch { }
                        try { TryApplyMaterial(doc, bInst, materialSettings, compBarrel?.CategoryType ?? "Thân cống"); } catch { }

                        barrels2.Add(bInst);
                        outCreatedBarrels?.Add(bInst);
                    }

                    barrelIdx2++;
                    if (barrelIdx2 > 200 || (bStart - p2).GetLength() >= (p2 - ptHN).GetLength())
                        break;
                }
                firstBarrel2 = barrels2.FirstOrDefault();
                lastBarrel2 = barrels2.LastOrDefault();
            }

            // 6.1 Tự động nhận diện hướng cống đâm vào và điều khiển tham số Ẩn Trái / Ẩn Phải cho Hố kỹ thuật và Hộp nối
            AutoConfigureManholePenetrationVisibility(doc, instHKT1, ptHKT1, rotAngle, new[] { firstBarrel1 }, data, compHKT?.CategoryType ?? "Hố kỹ thuật", isMiddleJointBox: false);
            AutoConfigureManholePenetrationVisibility(doc, instHKT2, ptHKT2, rotAngle + Math.PI, new[] { firstBarrel2 }, data, compHKT?.CategoryType ?? "Hố kỹ thuật", isMiddleJointBox: false);
            AutoConfigureManholePenetrationVisibility(doc, instHN, ptHN, rotAngle, null, data, compHN?.CategoryType ?? "Hộp nối cống", isMiddleJointBox: true);

            // 7. Rải Bê tông lót & Đá dăm đệm (Hình 2 & Yêu cầu 3):
            // Điểm 1 adaptive đặt tại mép ngoài Hố kỹ thuật (ptHKT1_mepNgoai) đến mép ngoài Hộp nối (ptHN_Ngoai1).
            // Phía bên kia đặt từ mép ngoài Hộp nối (ptHN_Ngoai2) đến mép ngoài Hố kỹ thuật 2 (ptHKT2_mepNgoai).
            double offsetZBtlFeet = UnitUtils.ConvertToInternalUnits(offsetZBtlM, UnitTypeId.Meters);
            double offsetZDddFeet = UnitUtils.ConvertToInternalUnits(offsetZDddM, UnitTypeId.Meters);

            XYZ ptHKT1_mepNgoai = ptHKT1 + u * (thanGiengBFeet / 2.0);
            XYZ ptHKT2_mepNgoai = ptHKT2 - u * (thanGiengBFeet / 2.0);

            // Đoạn 1: ptHKT1_mepNgoai -> ptHN_Ngoai1
            FamilyInstance? instBTL1 = null;
            FamilyInstance? instDDD1 = null;
            if (symBTL != null)
            {
                instBTL1 = CreateAdaptiveInstanceSafe(doc, symBTL, new List<XYZ> { 
                    ptHKT1_mepNgoai + XYZ.BasisZ * offsetZBtlFeet, 
                    ptHN_Ngoai1 + XYZ.BasisZ * offsetZBtlFeet 
                });
                if (instBTL1 != null)
                {
                    try { BimParameterService.ApplyFamilyMappedParameters(instBTL1, familyParameterMappings, data, compBTL?.CategoryType ?? "Bê tông lót"); } catch { }
                    outCreatedBeddings?.Add(instBTL1);
                }
            }
            if (symDDD != null)
            {
                instDDD1 = CreateAdaptiveInstanceSafe(doc, symDDD, new List<XYZ> { 
                    ptHKT1_mepNgoai + XYZ.BasisZ * offsetZDddFeet, 
                    ptHN_Ngoai1 + XYZ.BasisZ * offsetZDddFeet 
                });
                if (instDDD1 != null)
                {
                    try { BimParameterService.ApplyFamilyMappedParameters(instDDD1, familyParameterMappings, data, compDDD?.CategoryType ?? "Đá dăm đệm"); } catch { }
                    outCreatedBeddings?.Add(instDDD1);
                }
            }

            // Đoạn 2: ptHKT2_mepNgoai -> ptHN_Ngoai2 (Điểm 1 adaptive tại mép ngoài Hố kỹ thuật)
            FamilyInstance? instBTL2 = null;
            FamilyInstance? instDDD2 = null;
            if (symBTL != null)
            {
                instBTL2 = CreateAdaptiveInstanceSafe(doc, symBTL, new List<XYZ> { 
                    ptHKT2_mepNgoai + XYZ.BasisZ * offsetZBtlFeet, 
                    ptHN_Ngoai2 + XYZ.BasisZ * offsetZBtlFeet 
                });
                if (instBTL2 != null)
                {
                    try { BimParameterService.ApplyFamilyMappedParameters(instBTL2, familyParameterMappings, data, compBTL?.CategoryType ?? "Bê tông lót"); } catch { }
                    outCreatedBeddings?.Add(instBTL2);
                }
            }
            if (symDDD != null)
            {
                instDDD2 = CreateAdaptiveInstanceSafe(doc, symDDD, new List<XYZ> { 
                    ptHKT2_mepNgoai + XYZ.BasisZ * offsetZDddFeet, 
                    ptHN_Ngoai2 + XYZ.BasisZ * offsetZDddFeet 
                });
                if (instDDD2 != null)
                {
                    try { BimParameterService.ApplyFamilyMappedParameters(instDDD2, familyParameterMappings, data, compDDD?.CategoryType ?? "Đá dăm đệm"); } catch { }
                    outCreatedBeddings?.Add(instDDD2);
                }
            }

            // 8. Cut Geometry toàn bộ cấu kiện khi performCutGeometry == true
            if (performCutGeometry)
            {
                try { doc.Regenerate(); } catch { }

                // Cut Geometry tại điểm 1: Hố kỹ thuật 1 cắt BTL và Đá dăm đệm
                if (instHKT1 != null)
                {
                    if (instBTL1 != null) TryCutGeometrySafe(doc, instHKT1, instBTL1);
                    if (instDDD1 != null) TryCutGeometrySafe(doc, instHKT1, instDDD1);
                }

                // Cut Geometry tại điểm 2: Hố kỹ thuật 2 cắt BTL và Đá dăm đệm
                if (instHKT2 != null)
                {
                    if (instBTL2 != null) TryCutGeometrySafe(doc, instHKT2, instBTL2);
                    if (instDDD2 != null) TryCutGeometrySafe(doc, instHKT2, instDDD2);
                }

                // Hố kỹ thuật 1 cut với đốt cống gần nhất
                if (instHKT1 != null && firstBarrel1 != null)
                {
                    TryCutGeometrySafe(doc, instHKT1, firstBarrel1);
                }
                // Hố kỹ thuật 2 cut với đốt cống gần nhất
                if (instHKT2 != null && firstBarrel2 != null)
                {
                    TryCutGeometrySafe(doc, instHKT2, firstBarrel2);
                }
                // Hộp nối ở giữa cut với 2 đốt cống gần nhất trái và phải, và cut với BTL / Đá dăm đệm
                if (instHN != null)
                {
                    if (lastBarrel1 != null) TryCutGeometrySafe(doc, instHN, lastBarrel1);
                    if (lastBarrel2 != null) TryCutGeometrySafe(doc, instHN, lastBarrel2);
                    if (instBTL1 != null) TryCutGeometrySafe(doc, instHN, instBTL1);
                    if (instDDD1 != null) TryCutGeometrySafe(doc, instHN, instDDD1);
                    if (instBTL2 != null) TryCutGeometrySafe(doc, instHN, instBTL2);
                    if (instDDD2 != null) TryCutGeometrySafe(doc, instHN, instDDD2);
                }
            }

            // Gán Mark (CKT-STT), Comments và BIM_LyTrinh cho toàn bộ cấu kiện CKT vừa tạo
            var allCreatedCKT = new List<FamilyInstance>();
            if (outCreatedBarrels != null) allCreatedCKT.AddRange(outCreatedBarrels);
            if (outCreatedBeddings != null) allCreatedCKT.AddRange(outCreatedBeddings);
            if (outCreatedManholes != null) allCreatedCKT.AddRange(outCreatedManholes);

            string cktMark = $"CKT-{data.STT}";
            string cktComm = $"Cống kỹ thuật {data.STT} ({data.LyTrinh})";
            foreach (var fi in allCreatedCKT)
            {
                if (fi == null || !fi.IsValidObject) continue;
                try
                {
                    var pM = fi.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    if (pM != null && !pM.IsReadOnly) pM.Set(cktMark);
                    var pC = fi.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (pC != null && !pC.IsReadOnly) pC.Set(cktComm);
                    var pL = fi.LookupParameter("BIM_LyTrinh") ?? fi.LookupParameter("Lý trình");
                    if (pL != null && !pL.IsReadOnly) pL.Set(data.LyTrinh ?? "");
                }
                catch { }
            }
        }

        /// <summary>
        /// Tự động nhận diện hướng cống hộp đâm vào Hố kỹ thuật và điều khiển các tham số ẩn/hiện thành (ẨN TRÁI / ẨN PHẢI)
        /// Hỗ trợ cả Instance Parameters và Subcomponents (nested families như HO GA_THAN GIENG)
        /// </summary>
        private static void AutoConfigureManholePenetrationVisibility(
            Document doc,
            FamilyInstance? manholeInst,
            XYZ ptManhole,
            double manholeRotAngle,
            IEnumerable<FamilyInstance?>? barrels,
            CulvertRowData data,
            string categoryName,
            bool isMiddleJointBox = false)
        {
            if (manholeInst == null || !manholeInst.IsValidObject) return;

            bool hasPenetrationLeft = false;
            bool hasPenetrationRight = false;

            if (isMiddleJointBox)
            {
                // Hộp nối cống ở giữa tuyến kỹ thuật luôn có cống hộp đâm xuyên qua cả 2 bên Trái và Phải
                hasPenetrationLeft = true;
                hasPenetrationRight = true;
            }
            else
            {
                // Đối với Hố kỹ thuật ở 2 đầu tuyến (HKT1 và HKT2):
                // Tính toán hướng vector của cống gần nhất chiếu lên hệ trục tọa độ cục bộ của Hố kỹ thuật
                XYZ uLocalX = new XYZ(Math.Cos(manholeRotAngle), Math.Sin(manholeRotAngle), 0.0);

                if (barrels != null)
                {
                    foreach (var b in barrels)
                    {
                        if (b == null || !b.IsValidObject) continue;
                        XYZ? bPt = null;
                        if (AdaptiveComponentInstanceUtils.IsAdaptiveComponentInstance(b))
                        {
                            var ptIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(b);
                            if (ptIds != null && ptIds.Count > 0)
                            {
                                if (doc.GetElement(ptIds[0]) is ReferencePoint rp0)
                                {
                                    bPt = rp0.Position;
                                }
                            }
                        }
                        if (bPt == null && b.Location is LocationPoint lp)
                        {
                            bPt = lp.Point;
                        }

                        if (bPt != null)
                        {
                            XYZ toBarrel = bPt - ptManhole;
                            double dotX = toBarrel.DotProduct(uLocalX);
                            if (dotX > 0.05)
                            {
                                hasPenetrationRight = true;
                            }
                            else if (dotX < -0.05)
                            {
                                hasPenetrationLeft = true;
                            }
                        }
                    }
                }

                // Nếu barrels rỗng hoặc không đo được tọa độ, mặc định cống đâm vào phía trục X cục bộ
                if (!hasPenetrationLeft && !hasPenetrationRight)
                {
                    hasPenetrationRight = true;
                }
            }

            ApplyOpeningVisibilityParams(doc, manholeInst, hasPenetrationLeft, hasPenetrationRight, data, categoryName);
        }

        private static void ApplyOpeningVisibilityParams(
            Document doc,
            FamilyInstance inst,
            bool hideLeft,
            bool hideRight,
            CulvertRowData rowData,
            string categoryName)
        {
            if (inst == null || !inst.IsValidObject) return;

            bool? userOverrideLeft = GetUserYesNoOverride(rowData, categoryName, "ẨN TRÁI", "AN TRAI", "ẨN_TRÁI", "AN_TRAI", "TRÁI", "TRAI");
            bool? userOverrideRight = GetUserYesNoOverride(rowData, categoryName, "ẨN PHẢI", "AN PHAI", "ẨN_PHẢI", "AN_PHAI", "PHẢI", "PHAI");

            bool finalHideLeft = userOverrideLeft ?? hideLeft;
            bool finalHideRight = userOverrideRight ?? hideRight;

            // 1. Áp dụng trực tiếp trên FamilyInstance inst
            SetInstanceOrSymbolVisibility(inst, finalHideLeft, finalHideRight);

            // 2. Áp dụng cho các nested / sub-components (ví dụ HO GA_THAN GIENG lồng bên trong)
            try
            {
                var subIds = inst.GetSubComponentIds();
                if (subIds != null && subIds.Count > 0)
                {
                    foreach (var sId in subIds)
                    {
                        if (doc.GetElement(sId) is FamilyInstance subInst && subInst.IsValidObject)
                        {
                            SetInstanceOrSymbolVisibility(subInst, finalHideLeft, finalHideRight);
                        }
                    }
                }
            }
            catch { }
        }

        private static bool? GetUserYesNoOverride(CulvertRowData rowData, string? categoryName, params string[] paramNames)
        {
            if (rowData == null || rowData.ParameterOverrides == null) return null;
            foreach (var name in paramNames)
            {
                string val = rowData.GetParamOverride(categoryName, name, string.Empty);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    string trimmed = val.Trim().ToLowerInvariant();
                    if (trimmed == "1" || trimmed == "true" || trimmed == "có" || trimmed == "co" || trimmed == "yes")
                        return true;
                    if (trimmed == "0" || trimmed == "false" || trimmed == "không" || trimmed == "khong" || trimmed == "no")
                        return false;
                }
            }
            return null;
        }

        private static void SetInstanceOrSymbolVisibility(FamilyInstance inst, bool hideLeft, bool hideRight)
        {
            if (inst == null || !inst.IsValidObject) return;

            string[] leftParamNames = new[] {
                "ẨN TRÁI", "AN TRAI", "ẨN_TRÁI", "AN_TRAI", "TRÁI", "TRAI",
                "ẨN VÁCH TRÁI", "ẨN THÀNH TRÁI", "CỬA TRÁI", "CUA TRAI", "CỬA_TRÁI", "CUA_TRAI",
                "MỞ TRÁI", "MO TRAI", "MỞ_TRÁI", "MO_TRAI", "HIDE_LEFT", "OPEN_LEFT"
            };

            string[] rightParamNames = new[] {
                "ẨN PHẢI", "AN PHAI", "ẨN_PHẢI", "AN_PHAI", "PHẢI", "PHAI",
                "ẨN VÁCH PHẢI", "ẨN THÀNH PHẢI", "CỬA PHẢI", "CUA PHAI", "CỬA_PHẢI", "CUA_PHAI",
                "MỞ PHẢI", "MO PHAI", "MỞ_PHẢI", "MO_PHAI", "HIDE_RIGHT", "OPEN_RIGHT"
            };

            foreach (var lpName in leftParamNames)
            {
                var p = inst.LookupParameter(lpName) ?? inst.Symbol?.LookupParameter(lpName);
                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Integer)
                {
                    p.Set(hideLeft ? 1 : 0);
                    break;
                }
            }

            foreach (var rpName in rightParamNames)
            {
                var p = inst.LookupParameter(rpName) ?? inst.Symbol?.LookupParameter(rpName);
                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Integer)
                {
                    p.Set(hideRight ? 1 : 0);
                    break;
                }
            }
        }

        private static void PlaceOutletAssembly(
            Document doc,
            XYZ ptBase,
            XYZ uCulvert,
            double rotAngle,
            IList<CulvertComponentItem> outletComponents,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            CulvertRowData data,
            bool isUpstream,
            double lSanCongFeet,
            IEnumerable<CulvertMaterialItem>? materialSettings,
            List<FamilyInstance>? createdOutlets = null)
        {
            if (outletComponents == null || outletComponents.Count == 0) return;

            // Điểm 1 adaptive đặt ngay đúng điểm trong Excel (ptBase)
            // Điểm 2 adaptive:
            // - Cửa xả 1 (thượng lưu): hướng từ P1 về P2 (+uCulvert)
            // - Cửa xả 2 (hạ lưu): hướng từ P2 về P1 (-uCulvert) (Yêu cầu 1)
            XYZ uDirNorm = new XYZ(uCulvert.X, uCulvert.Y, 0).Normalize();
            XYZ uDir = isUpstream ? uDirNorm : -uDirNorm;

            XYZ pA = ptBase;
            XYZ pB = ptBase + uDir * lSanCongFeet;

            foreach (var comp in outletComponents)
            {
                if (!comp.IsActive || comp.SelectedSymbol == null) continue;

                var sym = comp.SelectedSymbol.GetFreshSymbol(doc);
                if (sym == null) continue;

                string catUpper = (comp.CategoryType ?? "").ToUpperInvariant();
                FamilySymbol targetSym = sym;

                // Tự động nhận diện FamilySymbol tương ứng cho 5 bộ phận Cửa xả
                Family? symFam = null;
                try { if (sym.IsValidObject) symFam = sym.Family; } catch { }

                if (symFam != null && symFam.IsValidObject)
                {
                    string targetTypeName = string.Empty;
                    if (catUpper.Contains("TƯỜNG ĐẦU") || catUpper.Contains("TUONG DAU")) targetTypeName = "TUONG DAU";
                    else if (catUpper.Contains("TƯỜNG CÁNH") || catUpper.Contains("TUONG CANH")) targetTypeName = "TUONG CANH";
                    else if (catUpper.Contains("BÊ TÔNG LÓT") || catUpper.Contains("BE TONG LOT") || catUpper.Contains("BTL") || catUpper.Contains("LÓT") || catUpper.Contains("LOT")) targetTypeName = "BE TONG LOT";
                    else if (catUpper.Contains("ĐÁ DĂM") || catUpper.Contains("DA DAM") || catUpper.Contains("ĐỆM") || catUpper.Contains("DEM")) targetTypeName = "DA DAM DEM";
                    else if (catUpper.Contains("SÂN CỐNG") || catUpper.Contains("SAN CONG") || catUpper.Contains("BẢN ĐÁY") || catUpper.Contains("BAN DAY")) targetTypeName = "SAN CONG";

                    if (!string.IsNullOrEmpty(targetTypeName))
                    {
                        try
                        {
                            foreach (ElementId symId in symFam.GetFamilySymbolIds())
                            {
                                if (doc.GetElement(symId) is FamilySymbol fs && fs.IsValidObject)
                                {
                                    bool isMatch = false;
                                    if (targetTypeName == "SAN CONG")
                                    {
                                        isMatch = fs.Name.IndexOf("SAN CONG", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                                  !fs.Name.Contains("TUONG") && !fs.Name.Contains("BE TONG") && !fs.Name.Contains("DA DAM");
                                    }
                                    else
                                    {
                                        isMatch = fs.Name.IndexOf(targetTypeName, StringComparison.OrdinalIgnoreCase) >= 0;
                                    }
                                    if (isMatch)
                                    {
                                        targetSym = fs;
                                        break;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                ActivateSymbol(targetSym);

                double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
                XYZ offZ = new XYZ(0, 0, offFeetZ);
                XYZ pA_off = pA + offZ;
                XYZ pB_off = pB + offZ;

                FamilyInstance? inst = null;
                try
                {
                    if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(targetSym))
                    {
                        inst = CreateAdaptiveInstanceSafe(doc, targetSym, new[] { pA_off, pB_off });
                    }
                    else
                    {
                        inst = CreateInstanceSafe(doc, pA_off, targetSym);
                        double rotOut = Math.Atan2(uDir.Y, uDir.X);
                        SafeRotate(doc, inst.Id, pA_off, rotOut);
                    }
                }
                catch { }

                if (inst == null) continue;
                createdOutlets?.Add(inst);

                try
                {
                    var pGx = inst.LookupParameter("CH_GX");
                    if (pGx != null && !pGx.IsReadOnly && pGx.StorageType == StorageType.Double)
                    {
                        pGx.Set(Math.PI / 2.0);
                    }
                    var pGxC = inst.LookupParameter("A_GOC XIENG");
                    if (pGxC != null && !pGxC.IsReadOnly && pGxC.StorageType == StorageType.Double)
                    {
                        pGxC.Set(0.0);
                    }
                    var pLsc = inst.LookupParameter("CX_L san cong") ?? inst.LookupParameter("L_SAN_CONG");
                    if (pLsc != null && !pLsc.IsReadOnly && pLsc.StorageType == StorageType.Double && lSanCongFeet > 0.1)
                    {
                        pLsc.Set(lSanCongFeet);
                    }
                }
                catch { }

                try { SetOutletVisibilitySafe(inst, comp.CategoryType ?? ""); } catch { }

                string suffix = isUpstream ? "TL" : "HL";
                string tenCK = $"{comp.CategoryType}_{suffix}";
                try { BimParameterService.SetElementBimProperties(inst, bimConfig, tenCK, isUpstream ? "CỬA XẢ THƯỢNG LƯU" : "CỬA XẢ HẠ LƯU", null, null, pA_off.X, pA_off.Y, pA_off.Z); } catch { }
                try { BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, data, comp.CategoryType ?? "Cửa xả"); } catch { }
                string? outletMat = materialSettings?.FirstOrDefault(m => m.CategoryType == comp.CategoryType)?.MaterialName;
                try { BimParameterService.ApplyCustomBimParameters(inst, customBimParams, data, comp.CategoryType ?? "Cửa xả", null, suffix, outletMat, doc, bimConfig.AutoCreateSharedParameters); } catch { }
                try { TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType ?? ""); } catch { }
            }
        }

        private static void PlaceApronAssembly(
            Document doc,
            XYZ ptSC2,
            XYZ uCulvert,
            IList<CulvertComponentItem> apronComponents,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            CulvertRowData data,
            bool isUpstream,
            double lSgcM,
            IEnumerable<CulvertMaterialItem>? materialSettings,
            List<FamilyInstance>? createdAprons = null)
        {
            if (apronComponents == null || apronComponents.Count == 0) return;

            XYZ uOut = isUpstream
                ? -new XYZ(uCulvert.X, uCulvert.Y, 0).Normalize()
                : new XYZ(uCulvert.X, uCulvert.Y, 0).Normalize();

            // Chiều dài thiết kế sân gia cố từ Excel hoặc mặc định 3.0m
            double lSgcFeet = UnitUtils.ConvertToInternalUnits(lSgcM, UnitTypeId.Meters);

            XYZ pA = ptSC2;
            XYZ pB = ptSC2 + uOut * lSgcFeet;

            foreach (var comp in apronComponents)
            {
                if (!comp.IsActive || comp.SelectedSymbol == null) continue;

                var sym = comp.SelectedSymbol.GetFreshSymbol(doc);
                if (sym == null) continue;

                string catUpper = (comp.CategoryType ?? "").ToUpperInvariant();
                FamilySymbol targetSym = sym;

                Family? symFam = null;
                try { if (sym.IsValidObject) symFam = sym.Family; } catch { }

                if (symFam != null && symFam.IsValidObject)
                {
                    string targetTypeName = string.Empty;
                    if (catUpper.Contains("LÓT") || catUpper.Contains("LOT") || catUpper.Contains("BTL")) targetTypeName = "BE TONG LOT";
                    else targetTypeName = "SAN GIA CO";

                    try
                    {
                        foreach (ElementId symId in symFam.GetFamilySymbolIds())
                        {
                            if (doc.GetElement(symId) is FamilySymbol fs && fs.IsValidObject && fs.Name.IndexOf(targetTypeName, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                targetSym = fs;
                                break;
                            }
                        }
                    }
                    catch { }
                }

                ActivateSymbol(targetSym);

                double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
                XYZ offZ = new XYZ(0, 0, offFeetZ);
                XYZ pA_off = pA + offZ;
                XYZ pB_off = pB + offZ;

                FamilyInstance? inst = null;
                try
                {
                    if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(targetSym))
                    {
                        inst = CreateAdaptiveInstanceSafe(doc, targetSym, new[] { pA_off, pB_off });
                    }
                    else
                    {
                        inst = CreateInstanceSafe(doc, pA_off, targetSym);
                        double rotOut = Math.Atan2(uOut.Y, uOut.X);
                        SafeRotate(doc, inst.Id, pA_off, rotOut);
                    }
                }
                catch { }

                if (inst == null) continue;
                if (createdAprons != null) createdAprons.Add(inst);

                try
                {
                    var pGxC = inst.LookupParameter("CH_SGC_GX");
                    if (pGxC != null && !pGxC.IsReadOnly && pGxC.StorageType == StorageType.Double)
                    {
                        pGxC.Set(0.0);
                    }
                }
                catch { }

                try { SetOutletVisibilitySafe(inst, comp.CategoryType ?? ""); } catch { }

                string suffix = isUpstream ? "TL" : "HL";
                string tenCK = $"{comp.CategoryType}_{suffix}";
                try { BimParameterService.SetElementBimProperties(inst, bimConfig, tenCK, isUpstream ? "SÂN GIA CỐ THƯỢNG LƯU" : "SÂN GIA CỐ HẠ LƯU", null, null, pA_off.X, pA_off.Y, pA_off.Z); } catch { }
                try { BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, data, comp.CategoryType ?? "Sân gia cố"); } catch { }
                string? apronMat = materialSettings?.FirstOrDefault(m => m.CategoryType == comp.CategoryType)?.MaterialName;
                try { BimParameterService.ApplyCustomBimParameters(inst, customBimParams, data, comp.CategoryType ?? "Sân gia cố", null, suffix, apronMat, doc, bimConfig.AutoCreateSharedParameters); } catch { }
                try { TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType ?? ""); } catch { }
            }
        }

        private static bool BoundingBoxesIntersect(Element? elemA, Element? elemB, double toleranceFeet = 0.5)
        {
            if (elemA == null || elemB == null || !elemA.IsValidObject || !elemB.IsValidObject) return false;
            try
            {
                var bbA = elemA.get_BoundingBox(null);
                var bbB = elemB.get_BoundingBox(null);
                if (bbA == null || bbB == null) return true; // nếu không lấy được bbox thì cho phép thử

                if (bbA.Max.X + toleranceFeet < bbB.Min.X || bbB.Max.X + toleranceFeet < bbA.Min.X) return false;
                if (bbA.Max.Y + toleranceFeet < bbB.Min.Y || bbB.Max.Y + toleranceFeet < bbA.Min.Y) return false;
                if (bbA.Max.Z + toleranceFeet < bbB.Min.Z || bbB.Max.Z + toleranceFeet < bbA.Min.Z) return false;
                return true;
            }
            catch
            {
                return true;
            }
        }

        internal static bool TryCutHeadwallWithCulvertVoid(Document doc, Element? tuongDau, Element? culvertBarrel)
        {
            if (doc == null || tuongDau == null || culvertBarrel == null || !tuongDau.IsValidObject || !culvertBarrel.IsValidObject) return false;
            if (tuongDau.Id == culvertBarrel.Id) return false;

            // 1. Kiểm tra nếu đã cut trước đó
            try
            {
                if (InstanceVoidCutUtils.InstanceVoidCutExists(tuongDau, culvertBarrel)) return true;
            }
            catch { }

            // 2. Kiểm tra va chạm BoundingBox
            if (!BoundingBoxesIntersect(tuongDau, culvertBarrel, 0.5))
            {
                return false;
            }

            // 3. Unjoin nếu đã Join
            try
            {
                if (JoinGeometryUtils.AreElementsJoined(doc, tuongDau, culvertBarrel))
                {
                    JoinGeometryUtils.UnjoinGeometry(doc, tuongDau, culvertBarrel);
                }
            }
            catch { }

            // 4. Thử InstanceVoidCut (Void trong cống hộp cắt Tường đầu) trong SubTransaction cô lập
            bool voidSuccess = false;
            try
            {
                if (InstanceVoidCutUtils.CanBeCutWithVoid(tuongDau))
                {
                    using (var st = new SubTransaction(doc))
                    {
                        st.Start();
                        try
                        {
                            InstanceVoidCutUtils.AddInstanceVoidCut(doc, tuongDau, culvertBarrel);
                            doc.Regenerate();
                            st.Commit();
                            voidSuccess = true;
                        }
                        catch
                        {
                            if (st.HasStarted() && !st.HasEnded()) st.RollBack();
                        }
                    }
                }
            }
            catch { }

            // 5. Nếu tuongDau chứa các cấu kiện con (nested subcomponents), thử cut từng cấu kiện con
            if (!voidSuccess && tuongDau is FamilyInstance fi)
            {
                try
                {
                    var subIds = fi.GetSubComponentIds();
                    if (subIds != null && subIds.Count > 0)
                    {
                        foreach (var sId in subIds)
                        {
                            var subElem = doc.GetElement(sId);
                            if (subElem != null && subElem.IsValidObject && BoundingBoxesIntersect(subElem, culvertBarrel, 0.5))
                            {
                                try
                                {
                                    if (InstanceVoidCutUtils.CanBeCutWithVoid(subElem) && !InstanceVoidCutUtils.InstanceVoidCutExists(subElem, culvertBarrel))
                                    {
                                        using (var st = new SubTransaction(doc))
                                        {
                                            st.Start();
                                            try
                                            {
                                                InstanceVoidCutUtils.AddInstanceVoidCut(doc, subElem, culvertBarrel);
                                                doc.Regenerate();
                                                st.Commit();
                                                voidSuccess = true;
                                            }
                                            catch
                                            {
                                                if (st.HasStarted() && !st.HasEnded()) st.RollBack();
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch { }
            }

            // 6. Nếu InstanceVoidCut không khả thi hoặc không cắt được, fallback sang SolidSolidCutUtils hoặc JoinGeometryUtils
            if (!voidSuccess)
            {
                return TryCutSolidSolid(doc, tuongDau, culvertBarrel);
            }

            return voidSuccess;
        }

        internal static bool TryCutSolidSolid(Document doc, Element? elemA, Element? elemB)
        {
            if (doc == null || elemA == null || elemB == null || !elemA.IsValidObject || !elemB.IsValidObject) return false;
            if (elemA.Id == elemB.Id) return false;

            // 1. Kiểm tra nếu 2 đối tượng đã được Cut trước đó
            try
            {
                var cuttingSolidsA = SolidSolidCutUtils.GetCuttingSolids(elemA);
                if (cuttingSolidsA != null && cuttingSolidsA.Contains(elemB.Id)) return true;
                var cuttingSolidsB = SolidSolidCutUtils.GetCuttingSolids(elemB);
                if (cuttingSolidsB != null && cuttingSolidsB.Contains(elemA.Id)) return true;
            }
            catch { }

            // 2. Kiểm tra va chạm BoundingBox
            if (!BoundingBoxesIntersect(elemA, elemB, 0.5))
            {
                return false;
            }

            // 3. Unjoin các đối tượng nếu đã Join trước đó
            try
            {
                if (JoinGeometryUtils.AreElementsJoined(doc, elemA, elemB))
                {
                    JoinGeometryUtils.UnjoinGeometry(doc, elemA, elemB);
                }
            }
            catch { }

            bool cutSuccess = false;

            // 4. Thử SolidSolidCutUtils elemA bị cắt bởi elemB
            try
            {
                if (SolidSolidCutUtils.CanElementCutElement(elemB, elemA, out _))
                {
                    using (var st = new SubTransaction(doc))
                    {
                        st.Start();
                        try
                        {
                            SolidSolidCutUtils.AddCutBetweenSolids(doc, elemA, elemB);
                            doc.Regenerate();
                            st.Commit();
                            cutSuccess = true;
                        }
                        catch
                        {
                            if (st.HasStarted() && !st.HasEnded()) st.RollBack();
                        }
                    }
                }
            }
            catch { }

            // 5. Thử chiều ngược lại elemB bị cắt bởi elemA
            if (!cutSuccess)
            {
                try
                {
                    if (SolidSolidCutUtils.CanElementCutElement(elemA, elemB, out _))
                    {
                        using (var st = new SubTransaction(doc))
                        {
                            st.Start();
                            try
                            {
                                SolidSolidCutUtils.AddCutBetweenSolids(doc, elemB, elemA);
                                doc.Regenerate();
                                st.Commit();
                                cutSuccess = true;
                            }
                            catch
                            {
                                if (st.HasStarted() && !st.HasEnded()) st.RollBack();
                            }
                        }
                    }
                }
                catch { }
            }

            // 6. Fallback JoinGeometryUtils & SwitchJoinOrder
            if (!cutSuccess)
            {
                try
                {
                    using (var st = new SubTransaction(doc))
                    {
                        st.Start();
                        try
                        {
                            if (!JoinGeometryUtils.AreElementsJoined(doc, elemA, elemB))
                            {
                                JoinGeometryUtils.JoinGeometry(doc, elemA, elemB);
                            }
                            if (JoinGeometryUtils.AreElementsJoined(doc, elemA, elemB))
                            {
                                try
                                {
                                    if (!JoinGeometryUtils.IsCuttingElementInJoin(doc, elemB, elemA))
                                    {
                                        JoinGeometryUtils.SwitchJoinOrder(doc, elemA, elemB);
                                    }
                                }
                                catch { }
                            }
                            doc.Regenerate();
                            st.Commit();
                            cutSuccess = true;
                        }
                        catch
                        {
                            if (st.HasStarted() && !st.HasEnded()) st.RollBack();
                        }
                    }
                }
                catch { }
            }

            return cutSuccess;
        }

        internal static bool TryCutGeometrySafe(Document doc, Element? eToCut, Element? eCutter)
        {
            return TryCutSolidSolid(doc, eToCut, eCutter);
        }

        internal static bool IsVaiKeOrTaiParamName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string n = name.Trim().ToUpperInvariant();
            return n == "VAI" || n == "TAI" ||
                   n == "CO VAI" || n == "CO_VAI" ||
                   n == "CO VAI KE" || n == "CO_VAI_KE" ||
                   n == "CO TAI" || n == "CO_TAI" ||
                   n == "TAI CONG" || n == "TAI_CONG" ||
                   n == "CONG HOP CO TAI" || n == "CỐNG HỘP CÓ TAI" ||
                   n.Contains("VAI KE") || n.Contains("VAI_KE") ||
                   n.Contains("VAI KÊ") || n.Contains("VAI_KÊ") ||
                   n.Contains("TAI CỐNG") || n.Contains("TAI_CONG") ||
                   n.Contains("CÓ TAI") || n.Contains("CO TAI");
        }

        private static bool CheckIfVaiKeEnabled(
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            CulvertRowData data)
        {
            if (familyParameterMappings != null)
            {
                var item = familyParameterMappings.FirstOrDefault(m =>
                    IsVaiKeOrTaiParamName(m.InternalName) || IsVaiKeOrTaiParamName(m.DisplayName));

                if (item != null)
                {
                    // Nếu người dùng BỎ TICK (IsSelected == false) => chắc chắn TẮT vai kê / tai cống
                    if (!item.IsSelected)
                    {
                        return false;
                    }

                    string val = data.GetParamOverride(item.InternalName, item.CustomValue);
                    val = (val ?? "").Trim().ToLowerInvariant();
                    if (val == "0" || val == "no" || val == "false" || val == "không" || val == "khong" || val == "tắt" || val == "tat")
                    {
                        return false;
                    }
                    if (val == "1" || val == "yes" || val == "true" || val == "có" || val == "co" || val == "bật" || val == "bat")
                    {
                        return true;
                    }
                    return item.IsSelected;
                }
            }

            if (data.ParameterOverrides != null)
            {
                foreach (var kvp in data.ParameterOverrides)
                {
                    if (IsVaiKeOrTaiParamName(kvp.Key))
                    {
                        string val = (kvp.Value ?? "").Trim().ToLowerInvariant();
                        if (val == "0" || val == "no" || val == "false" || val == "không" || val == "khong" || val == "tắt" || val == "tat")
                        {
                            return false;
                        }
                        if (val == "1" || val == "yes" || val == "true" || val == "có" || val == "co" || val == "bật" || val == "bat")
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static XYZ? GetElementCenterPoint(Element? elem)
        {
            if (elem == null || !elem.IsValidObject) return null;
            try
            {
                if (elem.Location is LocationPoint lp) return lp.Point;
                if (elem.Location is LocationCurve lc) return (lc.Curve.GetEndPoint(0) + lc.Curve.GetEndPoint(1)) * 0.5;
                if (elem is FamilyInstance fi && AdaptiveComponentInstanceUtils.IsAdaptiveComponentInstance(fi))
                {
                    var adpIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(fi);
                    if (adpIds != null && adpIds.Count > 0)
                    {
                        XYZ sum = XYZ.Zero;
                        int count = 0;
                        foreach (var pid in adpIds)
                        {
                            if (fi.Document.GetElement(pid) is ReferencePoint rp)
                            {
                                sum += rp.Position;
                                count++;
                            }
                        }
                        if (count > 0) return sum / count;
                    }
                }
                var bb = elem.get_BoundingBox(null);
                if (bb != null) return (bb.Min + bb.Max) * 0.5;
            }
            catch { }
            return null;
        }

        private static void CutSubComponentsSolidSafe(Document doc, Element? elemA, Element? elemB)
        {
            if (doc == null || elemA == null || elemB == null || !elemA.IsValidObject || !elemB.IsValidObject) return;
            try
            {
                if (elemA is FamilyInstance fiA)
                {
                    var subIds = fiA.GetSubComponentIds();
                    if (subIds != null && subIds.Count > 0)
                    {
                        foreach (var sId in subIds)
                        {
                            var subElem = doc.GetElement(sId);
                            if (subElem != null && subElem.IsValidObject && BoundingBoxesIntersect(subElem, elemB, 0.5))
                            {
                                TryCutSolidSolid(doc, subElem, elemB);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static void CutSubComponentsSafe(Document doc, Element? elemA, Element? elemB)
        {
            CutSubComponentsSolidSafe(doc, elemA, elemB);
        }

        private static bool IsBtlInstance(Element? elem)
        {
            if (elem == null || !elem.IsValidObject) return false;
            string fam = "";
            string type = "";
            if (elem is FamilyInstance fi && fi.Symbol != null)
            {
                fam = (fi.Symbol.FamilyName ?? "").ToUpperInvariant();
                type = (fi.Symbol.Name ?? "").ToUpperInvariant();
            }
            string full = $"{fam} {type}";
            return full.Contains("BE TONG LOT") || full.Contains("BÊ TÔNG LÓT") || full.Contains("BTL") ||
                   (full.Contains("LOT") && !full.Contains("DA DAM") && !full.Contains("CPDD"));
        }

        private static bool IsDddInstance(Element? elem)
        {
            if (elem == null || !elem.IsValidObject) return false;
            string fam = "";
            string type = "";
            if (elem is FamilyInstance fi && fi.Symbol != null)
            {
                fam = (fi.Symbol.FamilyName ?? "").ToUpperInvariant();
                type = (fi.Symbol.Name ?? "").ToUpperInvariant();
            }
            string full = $"{fam} {type}";
            return (full.Contains("DA DAM") || full.Contains("ĐÁ DĂM") || full.Contains("DEM") || full.Contains("ĐỆM") || full.Contains("CPDD")) &&
                   !full.Contains("TUONG");
        }

        private static bool IsTuongDauInstance(Element? elem)
        {
            if (elem == null || !elem.IsValidObject) return false;
            string fam = "";
            string type = "";
            if (elem is FamilyInstance fi && fi.Symbol != null)
            {
                fam = (fi.Symbol.FamilyName ?? "").ToUpperInvariant();
                type = (fi.Symbol.Name ?? "").ToUpperInvariant();
            }
            string full = $"{fam} {type}";
            return full.Contains("TUONG DAU") || full.Contains("TƯỜNG ĐẦU");
        }

        private static bool IsHeadwallOrApronInstance(Element? elem)
        {
            if (elem == null || !elem.IsValidObject) return false;
            string fam = "";
            string type = "";
            if (elem is FamilyInstance fi && fi.Symbol != null)
            {
                fam = (fi.Symbol.FamilyName ?? "").ToUpperInvariant();
                type = (fi.Symbol.Name ?? "").ToUpperInvariant();
            }
            string full = $"{fam} {type}";
            return full.Contains("TUONG DAU") || full.Contains("TƯỜNG ĐẦU") ||
                   full.Contains("SAN CONG") || full.Contains("SÂN CỐNG") ||
                   full.Contains("CUA XA") || full.Contains("CỬA XẢ") ||
                   full.Contains("TUONG CANH") || full.Contains("TƯỜNG CÁNH");
        }

        private static List<Element> GetElementWithSubcomponents(Document doc, Element? elem)
        {
            var res = new List<Element>();
            if (elem == null || !elem.IsValidObject) return res;
            res.Add(elem);
            if (elem is FamilyInstance fi)
            {
                try
                {
                    var subIds = fi.GetSubComponentIds();
                    if (subIds != null && subIds.Count > 0)
                    {
                        foreach (var id in subIds)
                        {
                            var subElem = doc.GetElement(id);
                            if (subElem != null && subElem.IsValidObject)
                            {
                                res.Add(subElem);
                            }
                        }
                    }
                }
                catch { }
            }
            return res;
        }

        internal static void CutOrJoinElementsWithSubcomponents(Document doc, Element? elemA, Element? elemB)
        {
            if (doc == null || elemA == null || elemB == null || !elemA.IsValidObject || !elemB.IsValidObject) return;
            if (elemA.Id == elemB.Id) return;

            var targetsA = GetElementWithSubcomponents(doc, elemA);
            var targetsB = GetElementWithSubcomponents(doc, elemB);

            foreach (var a in targetsA)
            {
                foreach (var b in targetsB)
                {
                    if (a.Id == b.Id) continue;
                    if (!BoundingBoxesIntersect(a, b, 0.5)) continue;

                    bool cutDone = false;
                    try
                    {
                        cutDone = TryCutHeadwallWithCulvertVoid(doc, a, b);
                    }
                    catch { }

                    if (!cutDone)
                    {
                        try
                        {
                            cutDone = TryCutSolidSolid(doc, a, b);
                        }
                        catch { }
                    }

                    if (!cutDone)
                    {
                        try
                        {
                            cutDone = TryCutSolidSolid(doc, b, a);
                        }
                        catch { }
                    }
                }
            }
        }

        public class CastInPlaceDetails
        {
            public FamilyInstance? BarrelLeft { get; set; }
            public FamilyInstance? BarrelRight { get; set; }
            public List<FamilyInstance> MidBarrels { get; set; } = new();

            public FamilyInstance? BtlLeft { get; set; }
            public FamilyInstance? DddLeft { get; set; }
            public FamilyInstance? BtlRight { get; set; }
            public FamilyInstance? DddRight { get; set; }
            public List<FamilyInstance> MidBtls { get; set; } = new();
            public List<FamilyInstance> MidDdds { get; set; } = new();
        }

        internal static void ExecuteAllCulvertCuts(
            Document doc,
            bool isRowCastInPlace,
            List<FamilyInstance> upOutlets,
            List<FamilyInstance> dnOutlets,
            List<FamilyInstance> barrels,
            List<FamilyInstance> beddings,
            List<FamilyInstance> manholes,
            CastInPlaceDetails? cipDetails = null,
            List<FamilyInstance>? aprons = null)
        {
            try { doc.Regenerate(); } catch { }

            var dddTL = upOutlets.FirstOrDefault(IsDddInstance);
            var btlTL = upOutlets.FirstOrDefault(IsBtlInstance);

            var dddHL = dnOutlets.FirstOrDefault(IsDddInstance);
            var btlHL = dnOutlets.FirstOrDefault(IsBtlInstance);

            var firstBar = cipDetails?.BarrelLeft ?? barrels.FirstOrDefault();
            var lastBar = cipDetails?.BarrelRight ?? barrels.LastOrDefault();

            // 1. Cắt TƯỜNG ĐẦU / SÂN CỐNG CỬA XẢ với CỐNG HỘP tiếp xúc (ở cả 2 đầu thượng lưu và hạ lưu)
            var upHeadwallsAndAprons = upOutlets.Concat(aprons ?? Enumerable.Empty<FamilyInstance>())
                .Where(e => e != null && e.IsValidObject && (IsHeadwallOrApronInstance(e) || (firstBar != null && BoundingBoxesIntersect(e, firstBar, 1.0))))
                .Distinct()
                .ToList();

            if (firstBar != null)
            {
                foreach (var hw in upHeadwallsAndAprons)
                {
                    CutOrJoinElementsWithSubcomponents(doc, hw, firstBar);
                }
            }

            var dnHeadwallsAndAprons = dnOutlets.Concat(aprons ?? Enumerable.Empty<FamilyInstance>())
                .Where(e => e != null && e.IsValidObject && (IsHeadwallOrApronInstance(e) || (lastBar != null && BoundingBoxesIntersect(e, lastBar, 1.0))))
                .Distinct()
                .ToList();

            if (lastBar != null)
            {
                foreach (var hw in dnHeadwallsAndAprons)
                {
                    CutOrJoinElementsWithSubcomponents(doc, hw, lastBar);
                }
            }

            if (isRowCastInPlace)
            {
                var btlLeft = cipDetails?.BtlLeft ?? beddings.FirstOrDefault(IsBtlInstance);
                var dddLeft = cipDetails?.DddLeft ?? beddings.FirstOrDefault(IsDddInstance);
                var btlRight = cipDetails?.BtlRight ?? beddings.LastOrDefault(IsBtlInstance);
                var dddRight = cipDetails?.DddRight ?? beddings.LastOrDefault(IsDddInstance);

                // 2. YÊU CẦU 2: Cắt phần ĐÁ DĂM ĐỆM của TƯỜNG ĐẦU CỬA XẢ với lớp BTL & ĐÁ DĂM ĐỆM của CỐNG HỘP ĐỔ TẠI CHỖ
                // Thượng lưu (Liên ngoài trái):
                if (dddTL != null && dddTL.IsValidObject)
                {
                    if (btlLeft != null && btlLeft.IsValidObject)
                    {
                        TryCutSolidSolid(doc, dddTL, btlLeft);
                        CutSubComponentsSolidSafe(doc, dddTL, btlLeft);
                    }
                    if (dddLeft != null && dddLeft.IsValidObject)
                    {
                        TryCutSolidSolid(doc, dddTL, dddLeft);
                        CutSubComponentsSolidSafe(doc, dddTL, dddLeft);
                    }
                }

                // Hạ lưu (Liên ngoài phải đối xứng):
                if (dddHL != null && dddHL.IsValidObject)
                {
                    if (btlRight != null && btlRight.IsValidObject)
                    {
                        TryCutSolidSolid(doc, dddHL, btlRight);
                        CutSubComponentsSolidSafe(doc, dddHL, btlRight);
                    }
                    if (dddRight != null && dddRight.IsValidObject)
                    {
                        TryCutSolidSolid(doc, dddHL, dddRight);
                        CutSubComponentsSolidSafe(doc, dddHL, dddRight);
                    }
                }

                // Cắt cống hộp liên ngoài với lớp BTL
                if (firstBar != null && btlLeft != null && btlLeft.IsValidObject)
                {
                    TryCutSolidSolid(doc, btlLeft, firstBar);
                    CutSubComponentsSolidSafe(doc, btlLeft, firstBar);
                }
                if (lastBar != null && btlRight != null && btlRight.IsValidObject)
                {
                    TryCutSolidSolid(doc, btlRight, lastBar);
                    CutSubComponentsSolidSafe(doc, btlRight, lastBar);
                }

                // 3. YÊU CẦU 3: Đối với LIÊN TRONG: cut cống hộp đổ tại chỗ với lớp BTL, cut cống hộp đổ tại chỗ với đá dăm đệm
                if (cipDetails?.MidBarrels != null && cipDetails.MidBarrels.Count > 0)
                {
                    for (int i = 0; i < cipDetails.MidBarrels.Count; i++)
                    {
                        var bar = cipDetails.MidBarrels[i];
                        if (bar == null || !bar.IsValidObject) continue;

                        FamilyInstance? btl = (i < cipDetails.MidBtls.Count) ? cipDetails.MidBtls[i] : null;
                        FamilyInstance? ddd = (i < cipDetails.MidDdds.Count) ? cipDetails.MidDdds[i] : null;

                        if (btl != null && btl.IsValidObject)
                        {
                            TryCutSolidSolid(doc, btl, bar);
                            CutSubComponentsSolidSafe(doc, btl, bar);
                        }

                        if (ddd != null && ddd.IsValidObject)
                        {
                            TryCutSolidSolid(doc, ddd, bar);
                            CutSubComponentsSolidSafe(doc, ddd, bar);
                        }
                    }
                }
                else
                {
                    var midBars = barrels.Skip(1).Take(Math.Max(0, barrels.Count - 2)).ToList();
                    foreach (var bar in midBars)
                    {
                        if (bar == null || !bar.IsValidObject) continue;
                        XYZ? bCenter = GetElementCenterPoint(bar);

                        var btlInst = beddings
                            .Where(b => b != null && b.IsValidObject && IsBtlInstance(b))
                            .OrderBy(b => {
                                XYZ? c = GetElementCenterPoint(b);
                                return (bCenter != null && c != null) ? bCenter.DistanceTo(c) : double.MaxValue;
                            })
                            .FirstOrDefault();

                        var dddInst = beddings
                            .Where(b => b != null && b.IsValidObject && IsDddInstance(b))
                            .OrderBy(b => {
                                XYZ? c = GetElementCenterPoint(b);
                                return (bCenter != null && c != null) ? bCenter.DistanceTo(c) : double.MaxValue;
                            })
                            .FirstOrDefault();

                        if (btlInst != null && btlInst.IsValidObject)
                        {
                            TryCutSolidSolid(doc, btlInst, bar);
                            CutSubComponentsSolidSafe(doc, btlInst, bar);
                        }
                        if (dddInst != null && dddInst.IsValidObject)
                        {
                            TryCutSolidSolid(doc, dddInst, bar);
                            CutSubComponentsSolidSafe(doc, dddInst, bar);
                        }
                    }
                }
            }
            else
            {
                // Cống đúc sẵn: Cut BTL & đá dăm đệm cống hộp với BTL & đá dăm đệm của Cửa xả
                var firstBed = beddings.FirstOrDefault();
                var lastBed = beddings.LastOrDefault();

                if (firstBed != null && firstBed.IsValidObject)
                {
                    if (dddTL != null && dddTL.IsValidObject) TryCutSolidSolid(doc, dddTL, firstBed);
                    if (btlTL != null && btlTL.IsValidObject) TryCutSolidSolid(doc, btlTL, firstBed);
                }

                if (lastBed != null && lastBed.IsValidObject && lastBed.Id != firstBed?.Id)
                {
                    if (dddHL != null && dddHL.IsValidObject) TryCutSolidSolid(doc, dddHL, lastBed);
                    if (btlHL != null && btlHL.IsValidObject) TryCutSolidSolid(doc, btlHL, lastBed);
                }
            }

            // Cống kỹ thuật: Cut Hố kỹ thuật và Hộp nối
            if (manholes != null && manholes.Count > 0)
            {
                var hkt1 = manholes.FirstOrDefault(m => m != null && m.IsValidObject && (m.Symbol?.FamilyName?.Contains("HO KY THUAT") == true || m.Symbol?.FamilyName?.Contains("HỐ KỸ THUẬT") == true));
                var hkt2 = manholes.LastOrDefault(m => m != null && m.IsValidObject && (m.Symbol?.FamilyName?.Contains("HO KY THUAT") == true || m.Symbol?.FamilyName?.Contains("HỐ KỸ THUẬT") == true));
                var hn = manholes.FirstOrDefault(m => m != null && m.IsValidObject && (m.Symbol?.FamilyName?.Contains("HOP NOI") == true || m.Symbol?.FamilyName?.Contains("HỘP NỐI") == true));

                if (hkt1 != null && firstBar != null) TryCutSolidSolid(doc, hkt1, firstBar);
                if (hkt2 != null && lastBar != null) TryCutSolidSolid(doc, hkt2, lastBar);

                var firstBed = beddings.FirstOrDefault();
                var lastBed = beddings.LastOrDefault();
                if (hkt1 != null && firstBed != null) TryCutSolidSolid(doc, hkt1, firstBed);
                if (hkt2 != null && lastBed != null) TryCutSolidSolid(doc, hkt2, lastBed);

                if (hn != null)
                {
                    foreach (var bar in barrels)
                    {
                        if (bar != null && bar.IsValidObject && BoundingBoxesIntersect(hn, bar, 0.5))
                        {
                            TryCutSolidSolid(doc, hn, bar);
                        }
                    }
                }
            }

            try { doc.Regenerate(); } catch { }
        }

        /// <summary>
        /// Xác định khoảng thụt vào mép sau của cấu kiện đá dăm đệm tường đầu sân cống (CX_TD_DD_W)
        /// Điểm 1 trên Excel thụt lùi đoạn CX_TD_DD_W (P1 - u * CX_TD_DD_W),
        /// <summary>
        /// YÊU CẦU 1 & 2: Xác định độ thụt mép ngoài tường đầu cửa xả của lớp BTL và đá dăm đệm:
        /// Điểm 1 trên Excel thụt tới đoạn CX_TD_DD_W (P1 + u * CX_TD_DD_W).
        /// Điểm 2 trên Excel thụt tới đoạn CX_TD_DD_W (P2 - u * CX_TD_DD_W).
        /// Áp dụng cho cả BTL và Đá dăm đệm của Cống hộp đúc sẵn và Cống hộp đổ tại chỗ.
        /// TUYỆT ĐỐI chỉ lấy theo biến CX_TD_DD_W (chiều dọc cống, thường là 1000mm = 1.0m),
        /// KHÔNG lấy theo CX_TD_DD_B (bề rộng ngang cống = 3500mm = 3.5m).
        /// </summary>
        public static double ResolveCxTdDdW(
            Document doc,
            CulvertRowData data,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IList<CulvertComponentItem>? outletComponents,
            FamilyInstance? sampleOutletInst = null)
        {
            double cxTdDdW_M = 0.0;

            // 1. Kiểm tra trong data.ParameterOverrides với key chứa "CX_TD_DD_W" (KHÔNG lấy CX_TD_DD_B)
            if (data.ParameterOverrides != null)
            {
                foreach (var kvp in data.ParameterOverrides)
                {
                    if (kvp.Key.IndexOf("CX_TD_DD_W", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (double.TryParse(kvp.Value.Replace("mm", "").Replace("m", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
                        {
                            cxTdDdW_M = (Math.Abs(dVal) >= 10.0) ? dVal / 1000.0 : dVal;
                            if (cxTdDdW_M > 0.05) return cxTdDdW_M;
                        }
                    }
                }
            }

            // 2. Kiểm tra trong familyParameterMappings với InternalName "CX_TD_DD_W"
            var mapping = familyParameterMappings?.FirstOrDefault(m => 
                string.Equals(m.InternalName, "CX_TD_DD_W", StringComparison.OrdinalIgnoreCase));
            if (mapping != null && !string.IsNullOrWhiteSpace(mapping.CustomValue))
            {
                string valStr = data.GetParamOverride(mapping.ProfileName, mapping.CategoryName, mapping.InternalName, mapping.CustomValue);
                if (double.TryParse(valStr.Replace("mm", "").Replace("m", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
                {
                    cxTdDdW_M = (Math.Abs(dVal) >= 10.0) ? dVal / 1000.0 : dVal;
                    if (cxTdDdW_M > 0.05) return cxTdDdW_M;
                }
            }

            // 3. Kiểm tra từ sampleOutletInst hoặc FilteredElementCollector trong Revit
            if (sampleOutletInst != null)
            {
                var p = sampleOutletInst.LookupParameter("CX_TD_DD_W") ?? sampleOutletInst.Symbol?.LookupParameter("CX_TD_DD_W");
                if (p != null && p.StorageType == StorageType.Double && p.AsDouble() > 0.05)
                {
                    return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Meters);
                }
            }

            // 4. Kiểm tra từ outletComponents symbol trong Revit
            if (outletComponents != null)
            {
                foreach (var oc in outletComponents)
                {
                    if (oc.IsActive && oc.SelectedSymbol != null)
                    {
                        var oSym = oc.SelectedSymbol.GetFreshSymbol(doc);
                        if (oSym != null)
                        {
                            var p = oSym.LookupParameter("CX_TD_DD_W");
                            if (p != null && p.StorageType == StorageType.Double && p.AsDouble() > 0.05)
                            {
                                return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Meters);
                            }
                        }
                    }
                }
            }

            // 5. Kiểm tra từ FilteredElementCollector tìm FamilyInstance Tường đầu / Cửa xả
            var sampleOInst = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .FirstOrDefault(fi => fi.Symbol != null && (fi.Symbol.FamilyName.Contains("TUONG DAU") || fi.Symbol.FamilyName.Contains("SAN CONG") || fi.Symbol.FamilyName.Contains("CUA XA")));
            if (sampleOInst != null)
            {
                var p = sampleOInst.LookupParameter("CX_TD_DD_W") ?? sampleOInst.Symbol?.LookupParameter("CX_TD_DD_W");
                if (p != null && p.StorageType == StorageType.Double && p.AsDouble() > 0.05)
                {
                    return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Meters);
                }
            }

            // 6. Mặc định chuẩn CAD: CX_TD_DD_W = 1000mm = 1.0m
            return 1.0;
        }

        private static void EnsureDedicatedSymbol(FamilyInstance inst, int stt, string? catName = null)
        {
            if (inst?.Symbol == null) return;
            try
            {
                string cleanCat = string.IsNullOrEmpty(catName) ? "" : "_" + System.Text.RegularExpressions.Regex.Replace(catName, @"[^\w\d]", "");
                string suffix = $"_{stt}{cleanCat}";
                string curName = inst.Symbol.Name;
                if (!curName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    string symName = $"{curName}{suffix}";
                    var existingSym = inst.Symbol.Family.GetFamilySymbolIds()
                        .Select(id => inst.Document.GetElement(id) as FamilySymbol)
                        .FirstOrDefault(s => s != null && string.Equals(s.Name, symName, StringComparison.OrdinalIgnoreCase));
                    if (existingSym != null)
                    {
                        if (!existingSym.IsActive) existingSym.Activate();
                        inst.Symbol = existingSym;
                    }
                    else
                    {
                        var newSym = inst.Symbol.Duplicate(symName) as FamilySymbol;
                        if (newSym != null)
                        {
                            if (!newSym.IsActive) newSym.Activate();
                            inst.Symbol = newSym;
                        }
                    }
                }
            }
            catch { }
        }

        private static void ApplyBeddingDirectDimensions(FamilyInstance inst, CulvertRowData rowData, string categoryType, string? profileName = null)
        {
            if (inst == null || rowData == null) return;
            bool isDdd = categoryType.IndexOf("đá dăm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         categoryType.IndexOf("da dam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         categoryType.IndexOf("dem", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isBtl = categoryType.IndexOf("bê tông lót", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         categoryType.IndexOf("be tong lot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         categoryType.IndexOf("btl", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         categoryType.IndexOf("lot", StringComparison.OrdinalIgnoreCase) >= 0;

            string prof = !string.IsNullOrEmpty(profileName)
                ? profileName
                : (!string.IsNullOrEmpty(rowData.AssignedProfileName)
                    ? rowData.AssignedProfileName
                    : (rowData.IsCastInPlace ? "Cống đổ tại chỗ" : (rowData.IsTechnical ? "Cống kỹ thuật" : "Cống đúc sẵn")));

            if (isDdd)
            {
                string bDddStr = rowData.GetParamOverride(prof, "Đá dăm đệm thân cống", "B_DDD", string.Empty);
                if (string.IsNullOrWhiteSpace(bDddStr)) bDddStr = rowData.GetParamOverride(prof, categoryType, "B_DDD", string.Empty);
                if (string.IsNullOrWhiteSpace(bDddStr)) bDddStr = rowData.GetParamOverride(prof, null, "B_DDD", string.Empty);
                if (!string.IsNullOrWhiteSpace(bDddStr) && double.TryParse(bDddStr.Replace("mm", "").Replace("m", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double bVal) && bVal > 0)
                {
                    double internalVal = (Math.Abs(bVal) >= 10.0)
                        ? UnitUtils.ConvertToInternalUnits(bVal, UnitTypeId.Millimeters)
                        : UnitUtils.ConvertToInternalUnits(bVal, UnitTypeId.Meters);

                    var pB = inst.LookupParameter("B_DDD") ?? inst.LookupParameter("B") ?? inst.LookupParameter("B_DEM") ?? inst.LookupParameter("Width");
                    if (pB != null && !pB.IsReadOnly && pB.StorageType == StorageType.Double) pB.Set(internalVal);
                    else if (inst.Symbol != null)
                    {
                        EnsureDedicatedSymbol(inst, rowData.STT, categoryType);
                        var pSymB = inst.Symbol.LookupParameter("B_DDD") ?? inst.Symbol.LookupParameter("B") ?? inst.Symbol.LookupParameter("B_DEM") ?? inst.Symbol.LookupParameter("Width");
                        if (pSymB != null && !pSymB.IsReadOnly && pSymB.StorageType == StorageType.Double) pSymB.Set(internalVal);
                    }
                }

                string hDddStr = rowData.GetParamOverride(prof, "Đá dăm đệm thân cống", "H_DDD", string.Empty);
                if (string.IsNullOrWhiteSpace(hDddStr)) hDddStr = rowData.GetParamOverride(prof, categoryType, "H_DDD", string.Empty);
                if (string.IsNullOrWhiteSpace(hDddStr)) hDddStr = rowData.GetParamOverride(prof, null, "H_DDD", string.Empty);
                if (!string.IsNullOrWhiteSpace(hDddStr) && double.TryParse(hDddStr.Replace("mm", "").Replace("m", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double hVal) && hVal > 0)
                {
                    double internalVal = (Math.Abs(hVal) >= 10.0)
                        ? UnitUtils.ConvertToInternalUnits(hVal, UnitTypeId.Millimeters)
                        : UnitUtils.ConvertToInternalUnits(hVal, UnitTypeId.Meters);

                    var pH = inst.LookupParameter("H_DDD") ?? inst.LookupParameter("H") ?? inst.LookupParameter("H_DEM") ?? inst.LookupParameter("Height");
                    if (pH != null && !pH.IsReadOnly && pH.StorageType == StorageType.Double) pH.Set(internalVal);
                    else if (inst.Symbol != null)
                    {
                        EnsureDedicatedSymbol(inst, rowData.STT, categoryType);
                        var pSymH = inst.Symbol.LookupParameter("H_DDD") ?? inst.Symbol.LookupParameter("H") ?? inst.Symbol.LookupParameter("H_DEM") ?? inst.Symbol.LookupParameter("Height");
                        if (pSymH != null && !pSymH.IsReadOnly && pSymH.StorageType == StorageType.Double) pSymH.Set(internalVal);
                    }
                }
            }
            else if (isBtl)
            {
                string bBtlStr = rowData.GetParamOverride(prof, "Bê tông lót thân cống", "B_BTL", string.Empty);
                if (string.IsNullOrWhiteSpace(bBtlStr)) bBtlStr = rowData.GetParamOverride(prof, categoryType, "B_BTL", string.Empty);
                if (string.IsNullOrWhiteSpace(bBtlStr)) bBtlStr = rowData.GetParamOverride(prof, null, "B_BTL", string.Empty);
                if (string.IsNullOrWhiteSpace(bBtlStr))
                {
                    string bDddStr = rowData.GetParamOverride(prof, "Đá dăm đệm thân cống", "B_DDD", string.Empty);
                    if (string.IsNullOrWhiteSpace(bDddStr)) bDddStr = rowData.GetParamOverride(prof, categoryType, "B_DDD", string.Empty);
                    if (string.IsNullOrWhiteSpace(bDddStr)) bDddStr = rowData.GetParamOverride(prof, null, "B_DDD", string.Empty);
                    if (!string.IsNullOrWhiteSpace(bDddStr) && double.TryParse(bDddStr.Replace("mm", "").Replace("m", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double bDddVal) && bDddVal > 0)
                    {
                        double bBtlVal = (Math.Abs(bDddVal) >= 10.0) ? (bDddVal - 200.0) : (bDddVal - 0.20);
                        bBtlStr = bBtlVal.ToString(CultureInfo.InvariantCulture);
                    }
                }

                if (!string.IsNullOrWhiteSpace(bBtlStr) && double.TryParse(bBtlStr.Replace("mm", "").Replace("m", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double bVal) && bVal > 0)
                {
                    double internalVal = (Math.Abs(bVal) >= 10.0)
                        ? UnitUtils.ConvertToInternalUnits(bVal, UnitTypeId.Millimeters)
                        : UnitUtils.ConvertToInternalUnits(bVal, UnitTypeId.Meters);

                    var pB = inst.LookupParameter("B_BTL") ?? inst.LookupParameter("B") ?? inst.LookupParameter("B_LOT") ?? inst.LookupParameter("Width");
                    if (pB != null && !pB.IsReadOnly && pB.StorageType == StorageType.Double) pB.Set(internalVal);
                    else if (inst.Symbol != null)
                    {
                        EnsureDedicatedSymbol(inst, rowData.STT, categoryType);
                        var pSymB = inst.Symbol.LookupParameter("B_BTL") ?? inst.Symbol.LookupParameter("B") ?? inst.Symbol.LookupParameter("B_LOT") ?? inst.Symbol.LookupParameter("Width");
                        if (pSymB != null && !pSymB.IsReadOnly && pSymB.StorageType == StorageType.Double) pSymB.Set(internalVal);
                    }
                }

                string hBtlStr = rowData.GetParamOverride(prof, "Bê tông lót thân cống", "H_BTL", string.Empty);
                if (string.IsNullOrWhiteSpace(hBtlStr)) hBtlStr = rowData.GetParamOverride(prof, categoryType, "H_BTL", string.Empty);
                if (string.IsNullOrWhiteSpace(hBtlStr)) hBtlStr = rowData.GetParamOverride(prof, null, "H_BTL", string.Empty);
                if (!string.IsNullOrWhiteSpace(hBtlStr) && double.TryParse(hBtlStr.Replace("mm", "").Replace("m", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double hVal) && hVal > 0)
                {
                    double internalVal = (Math.Abs(hVal) >= 10.0)
                        ? UnitUtils.ConvertToInternalUnits(hVal, UnitTypeId.Millimeters)
                        : UnitUtils.ConvertToInternalUnits(hVal, UnitTypeId.Meters);

                    var pH = inst.LookupParameter("H_BTL") ?? inst.LookupParameter("H") ?? inst.LookupParameter("H_LOT") ?? inst.LookupParameter("Height");
                    if (pH != null && !pH.IsReadOnly && pH.StorageType == StorageType.Double) pH.Set(internalVal);
                    else if (inst.Symbol != null)
                    {
                        EnsureDedicatedSymbol(inst, rowData.STT, categoryType);
                        var pSymH = inst.Symbol.LookupParameter("H_BTL") ?? inst.Symbol.LookupParameter("H") ?? inst.Symbol.LookupParameter("H_LOT") ?? inst.Symbol.LookupParameter("Height");
                        if (pSymH != null && !pSymH.IsReadOnly && pSymH.StorageType == StorageType.Double) pSymH.Set(internalVal);
                    }
                }
            }
        }

        private static FamilyInstance? PlaceBeddingInstance(
            Document doc,
            CulvertComponentItem? comp,
            XYZ pStart,
            XYZ pEnd,
            double rotAngle,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            CulvertRowData rowData,
            string componentName,
            List<FamilyInstance>? createdBeddings,
            IEnumerable<CulvertMaterialItem>? materialSettings = null)
        {
            if (comp == null || !comp.IsActive || comp.SelectedSymbol == null) return null;
            double lenFeet = (pEnd - pStart).GetLength();
            if (lenFeet <= 0.01) return null;

            var sym = comp.SelectedSymbol.GetFreshSymbol(doc);
            if (sym == null) return null;
            ActivateSymbol(sym);

            double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
            XYZ offZ = new XYZ(0, 0, offFeetZ);
            XYZ ptA = pStart + offZ;
            XYZ ptB = pEnd + offZ;

            FamilyInstance? inst = null;
            try
            {
                if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(sym))
                {
                    inst = CreateAdaptiveInstanceSafe(doc, sym, new[] { ptA, ptB });
                }
                else
                {
                    XYZ ptMid = (ptA + ptB) * 0.5;
                    inst = CreateInstanceSafe(doc, ptMid, sym);
                    SafeRotate(doc, inst.Id, ptMid, rotAngle);

                    string[] possibleLenParams = new[] { "L", "Length", "ChieuDai", "L_dot_chuan", "L_dot_bu", "Chiều dài", "L_DOT", "CH_L" };
                    foreach (var pName in possibleLenParams)
                    {
                        Parameter p = inst.LookupParameter(pName);
                        if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                        {
                            p.Set(lenFeet);
                            break;
                        }
                    }
                }
            }
            catch { }

            if (inst == null) return null;
            createdBeddings?.Add(inst);

            try { BimParameterService.SetElementBimProperties(inst, bimConfig, componentName, comp.CategoryType ?? "", null, null, null, null, null); } catch { }
            try { BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, rowData, comp.CategoryType ?? ""); } catch { }
            try { ApplyBeddingDirectDimensions(inst, rowData, comp.CategoryType ?? componentName, comp.ProfileName); } catch { }
            try { BimParameterService.ApplyCustomBimParameters(inst, customBimParams, rowData, "Thân cống"); } catch { }
            try { TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType ?? ""); } catch { }

            return inst;
        }

        private static FamilyInstance? PlaceSingleBarrelComponent(
            Document doc,
            CulvertComponentItem comp,
            XYZ ptStart,
            XYZ ptEnd,
            double lenFeet,
            double angle,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            CulvertRowData rowData,
            string tenCauKien,
            double? zDau,
            double? zCuoi,
            IEnumerable<CulvertMaterialItem>? materialSettings = null,
            List<FamilyInstance>? createdBarrels = null)
        {
            if (comp == null || !comp.IsActive || comp.SelectedSymbol == null) return null;
            var sym = comp.SelectedSymbol.GetFreshSymbol(doc);
            if (sym == null) return null;
            ActivateSymbol(sym);

            double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
            XYZ offZ = new XYZ(0, 0, offFeetZ);
            XYZ ptA = ptStart + offZ;
            XYZ ptB = ptEnd + offZ;

            FamilyInstance? inst = null;
            try
            {
                if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(sym))
                {
                    inst = CreateAdaptiveInstanceSafe(doc, sym, new[] { ptA, ptB });
                }
                else
                {
                    XYZ ptMid = (ptA + ptB) * 0.5;
                    inst = CreateInstanceSafe(doc, ptMid, sym);
                    SafeRotate(doc, inst.Id, ptMid, angle);
                }
            }
            catch { }

            if (inst == null) return null;
            createdBarrels?.Add(inst);

            try
            {
                string[] possibleLenParams = new[] { "L", "Length", "ChieuDai", "L_dot_chuan", "L_dot_bu", "Chiều dài", "L_DOT", "CH_L" };
                foreach (var pName in possibleLenParams)
                {
                    Parameter p = inst.LookupParameter(pName);
                    if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                    {
                        p.Set(lenFeet);
                        break;
                    }
                }
            }
            catch { }

            try { BimParameterService.SetElementBimProperties(inst, bimConfig, tenCauKien, comp.CategoryType, zDau, zCuoi, null, null, null); } catch { }
            try { BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, rowData, comp.CategoryType); } catch { }
            try { BimParameterService.ApplyCustomBimParameters(inst, customBimParams, rowData, "Thân cống"); } catch { }
            try { TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType); } catch { }

            return inst;
        }

        /// <summary>
        /// Dựng Cống Hộp Đổ Tại Chỗ chuyên biệt theo Yêu cầu 3 & 4 (Hình 2, 3, 4):
        /// - Tách biệt 3 cấu kiện: Cống hộp (liên ngoài trái), Cống hộp (liên trong), Cống hộp (liên ngoài phải)
        /// - Áp dụng tham số Tab 02 độc lập cho từng liên
        /// - Rải BTL & Đá dăm đệm theo đúng hình 3 & 4:
        ///   + Liên ngoài trái: Đá dăm từ mép ngoài tường đầu đến Điểm 2; BTL từ mép ngoài tường đầu đến Điểm 2 lùi lại biến B. Cut geometry giữa cống hộp và BTL.
        ///   + Liên trong: Đá dăm & BTL từ mép ngoài cống đến mép ngoài cống. Cut geometry giữa cống hộp và BTL.
        ///   + Liên ngoài phải: Đá dăm từ Điểm 2 đến mép ngoài tường đầu HL; BTL từ Điểm 2 tiến vào biến B đến mép ngoài tường đầu HL. Cut geometry giữa cống hộp và BTL.
        /// </summary>
        private static void BuildCastInPlaceBoxCulvert(
            Document doc,
            CulvertRowData data,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IList<CulvertComponentItem> barrelComponents,
            XYZ p1,
            XYZ p2,
            XYZ u,
            double rotAngle,
            double totalLengthFeet,
            double lStdFeet,
            double jointGapFeet,
            CulvertArrayMode arrayMode,
            IEnumerable<CulvertMaterialItem>? materialSettings = null,
            List<FamilyInstance>? createdBarrels = null,
            List<FamilyInstance>? createdBeddings = null,
            CastInPlaceDetails? outDetails = null,
            IList<CulvertComponentItem>? outletComponents = null,
            FamilyInstance? sampleOutletInst = null)
        {
            if (barrelComponents == null || barrelComponents.Count == 0 || totalLengthFeet <= 0.01) return;

            var compLeft = barrelComponents.FirstOrDefault(c => c.CategoryType == "Cống hộp (liên ngoài trái)")
                           ?? barrelComponents.FirstOrDefault(c => c.CategoryType?.IndexOf("ngoài trái", StringComparison.OrdinalIgnoreCase) >= 0)
                           ?? barrelComponents.FirstOrDefault(c => c.CategoryType?.IndexOf("trái", StringComparison.OrdinalIgnoreCase) >= 0)
                           ?? barrelComponents.FirstOrDefault(c => !IsBeddingComponent(c));

            var compMid = barrelComponents.FirstOrDefault(c => c.CategoryType == "Cống hộp (liên trong)")
                          ?? barrelComponents.FirstOrDefault(c => c.CategoryType?.IndexOf("trong", StringComparison.OrdinalIgnoreCase) >= 0)
                          ?? compLeft;

            var compRight = barrelComponents.FirstOrDefault(c => c.CategoryType == "Cống hộp (liên ngoài phải)")
                            ?? barrelComponents.FirstOrDefault(c => c.CategoryType?.IndexOf("ngoài phải", StringComparison.OrdinalIgnoreCase) >= 0)
                            ?? barrelComponents.FirstOrDefault(c => c.CategoryType?.IndexOf("phải", StringComparison.OrdinalIgnoreCase) >= 0)
                            ?? compLeft;

            var compBtl = barrelComponents.FirstOrDefault(c => c.CategoryType == "Bê tông lót thân cống" ||
                                                               c.CategoryType?.IndexOf("bê tông lót", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                               c.CategoryType?.IndexOf("btl", StringComparison.OrdinalIgnoreCase) >= 0);

            var compDdd = barrelComponents.FirstOrDefault(c => c.CategoryType == "Đá dăm đệm thân cống" ||
                                                               c.CategoryType?.IndexOf("đá dăm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                               c.CategoryType?.IndexOf("đệm", StringComparison.OrdinalIgnoreCase) >= 0);

            // Xác định biến B (chiều dài vát đáy notch/vai kê của family TNN_CH_THAN CONG_2x3x2)
            double bParamM = 0.40;
            var sampleBarrelComp = compLeft ?? compMid ?? compRight;
            if (sampleBarrelComp?.SelectedSymbol != null)
            {
                var sym = sampleBarrelComp.SelectedSymbol.GetFreshSymbol(doc);
                if (sym != null)
                {
                    var pB = sym.LookupParameter("B") ?? sym.LookupParameter("B_BOX") ?? sym.LookupParameter("CH_B");
                    if (pB != null && pB.StorageType == StorageType.Double && pB.AsDouble() > 0.05)
                    {
                        bParamM = UnitUtils.ConvertFromInternalUnits(pB.AsDouble(), UnitTypeId.Meters);
                    }
                }
            }
            var mappingB = familyParameterMappings?.FirstOrDefault(m => 
                string.Equals(m.InternalName, "B", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.InternalName, "CH_B", StringComparison.OrdinalIgnoreCase));
            if (mappingB != null && !string.IsNullOrWhiteSpace(mappingB.CustomValue))
            {
                string valStrB = data.GetParamOverride(mappingB.ProfileName, mappingB.CategoryName, mappingB.InternalName, mappingB.CustomValue);
                if (double.TryParse(valStrB.Replace("mm", "").Replace("m", "").Trim(), out double dValB))
                {
                    bParamM = (Math.Abs(dValB) >= 10.0) ? dValB / 1000.0 : dValB;
                }
            }
            if (bParamM <= 0.05) bParamM = 0.40;
            double bParamFeet = UnitUtils.ConvertToInternalUnits(bParamM, UnitTypeId.Meters);

            // YÊU CẦU 2 (Hình 4 & 5):
            // Xác định vị trí bắt đầu BTL và đá dăm đệm tại Cửa xả 1 (cách vô: p1 + u * CX_TD_DD_W) và Cửa xả 2 (cách vô: p2 - u * CX_TD_DD_W)
            double cxTdDdW_M = ResolveCxTdDdW(doc, data, familyParameterMappings, outletComponents, sampleOutletInst);
            double cxTdDdW_Feet = UnitUtils.ConvertToInternalUnits(cxTdDdW_M, UnitTypeId.Meters);

            XYZ pFootingBackTL = p1 + u * cxTdDdW_Feet;
            XYZ pFootingBackHL = p2 - u * cxTdDdW_Feet;

            double L = totalLengthFeet;
            double pitch = lStdFeet + jointGapFeet;

            // Tính toán danh sách phân đoạn (distStart, distEnd, compType)
            // compType: 0 = Left, 1 = Mid, 2 = Right
            var segments = new List<(double start, double end, int type)>();

            if (L <= lStdFeet + 0.1)
            {
                // Chỉ có 1 đốt duy nhất
                segments.Add((0.0, L, 0));
            }
            else if (L <= 2 * lStdFeet + jointGapFeet + 0.1)
            {
                // Có 2 đốt: Liên ngoài trái và Liên ngoài phải
                double lSeg = (L - jointGapFeet) / 2.0;
                segments.Add((0.0, lSeg, 0));
                segments.Add((lSeg + jointGapFeet, L, 2));
            }
            else
            {
                // >= 3 đốt: Liên ngoài trái, các liên trong, Liên ngoài phải
                if (arrayMode == CulvertArrayMode.CenterOut)
                {
                    int nMid = (int)Math.Floor((L - 2 * lStdFeet - jointGapFeet) / pitch);
                    if (nMid < 1) nMid = 1;
                    double midTotal = nMid * lStdFeet + (nMid - 1) * jointGapFeet;
                    double rem = L - midTotal - 2 * jointGapFeet;
                    double lEdge = rem / 2.0;

                    double minEdgeFeet = UnitUtils.ConvertToInternalUnits(1.0, UnitTypeId.Meters);
                    if (lEdge < minEdgeFeet && nMid > 1)
                    {
                        nMid--;
                        midTotal = nMid * lStdFeet + (nMid - 1) * jointGapFeet;
                        rem = L - midTotal - 2 * jointGapFeet;
                        lEdge = rem / 2.0;
                    }

                    // Đốt 0: Liên ngoài trái
                    segments.Add((0.0, lEdge, 0));
                    double cur = lEdge + jointGapFeet;

                    // Các đốt giữa: Liên trong
                    for (int m = 0; m < nMid; m++)
                    {
                        segments.Add((cur, cur + lStdFeet, 1));
                        cur += lStdFeet + jointGapFeet;
                    }

                    // Đốt cuối: Liên ngoài phải
                    segments.Add((cur, L, 2));
                }
                else
                {
                    // OneWay / StartToTarget: Đốt đầu và các đốt giữa dùng L chuẩn, đốt cuối nhận phần bù trừ
                    int totalN = (int)Math.Floor((L + jointGapFeet) / pitch);
                    if (totalN < 2) totalN = 2;

                    double cur = 0.0;
                    // Đốt 0: Liên ngoài trái
                    segments.Add((cur, cur + lStdFeet, 0));
                    cur += lStdFeet + jointGapFeet;

                    // Các đốt giữa: Liên trong
                    for (int m = 1; m < totalN - 1; m++)
                    {
                        segments.Add((cur, cur + lStdFeet, 1));
                        cur += lStdFeet + jointGapFeet;
                    }

                    // Đốt cuối: Liên ngoài phải
                    if (L - cur > 0.01)
                    {
                        segments.Add((cur, L, 2));
                    }
                    else if (segments.Count > 0)
                    {
                        var lastSeg = segments[segments.Count - 1];
                        segments.RemoveAt(segments.Count - 1);
                        segments.Add((lastSeg.start, L, 2));
                    }
                }
            }

            int N = segments.Count;
            FamilyInstance? barrelLeft = null;
            FamilyInstance? barrelRight = null;
            var barrelMidList = new List<FamilyInstance>();

            FamilyInstance? btlLeft = null;
            FamilyInstance? dddLeft = null;
            FamilyInstance? btlRight = null;
            FamilyInstance? dddRight = null;
            var btlMidList = new List<FamilyInstance>();
            var dddMidList = new List<FamilyInstance>();

            for (int i = 0; i < N; i++)
            {
                var seg = segments[i];
                XYZ ptA = p1 + u * seg.start;
                XYZ ptB = p1 + u * seg.end;
                double segLen = (ptB - ptA).GetLength();

                CulvertComponentItem? curBarrelComp = (seg.type == 0) ? (compLeft ?? compMid ?? compRight)
                                                    : (seg.type == 2) ? (compRight ?? compMid ?? compLeft)
                                                    : (compMid ?? compLeft ?? compRight);

                if (curBarrelComp == null) continue;

                string barrelName = (seg.type == 0) ? "Cống hộp (liên ngoài trái)"
                                  : (seg.type == 2) ? "Cống hộp (liên ngoài phải)"
                                  : $"Cống hộp (liên trong)_{i}";

                // 1. Đặt Thân cống hộp
                var barrelInst = PlaceSingleBarrelComponent(
                    doc,
                    curBarrelComp,
                    ptA,
                    ptB,
                    segLen,
                    rotAngle,
                    bimConfig,
                    customBimParams,
                    familyParameterMappings,
                    data,
                    barrelName,
                    (i == 0) ? data.Z1 : null,
                    (i == N - 1) ? data.Z2 : null,
                    materialSettings,
                    createdBarrels);

                if (seg.type == 0) barrelLeft = barrelInst;
                else if (seg.type == 2) barrelRight = barrelInst;
                else if (barrelInst != null) barrelMidList.Add(barrelInst);

                // 2. Đặt BTL và Đá dăm đệm theo đúng Yêu cầu 2 (Hình 1 mới & CAD 145):
                if (N == 1)
                {
                    // Cống chỉ có 1 đốt: BTL & Đá dăm từ mép sau tường đầu TL đến mép sau tường đầu HL
                    XYZ pBedA = pFootingBackTL;
                    XYZ pBedB = pFootingBackHL;
                    btlLeft = PlaceBeddingInstance(doc, compBtl, pBedA, pBedB, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, "Bê tông lót thân cống", createdBeddings, materialSettings);
                    dddLeft = PlaceBeddingInstance(doc, compDdd, pBedA, pBedB, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, "Đá dăm đệm thân cống", createdBeddings, materialSettings);
                }
                else if (seg.type == 0)
                {
                    // LIÊN NGOÀI TRÁI (Hình 1 & CAD 145):
                    // BTL & Đá dăm: Điểm bắt đầu luôn bắt đầu từ mép sau của cấu kiện đá dăm đệm của tường đầu sân cống (pFootingBackTL),
                    // và điểm cuối sẽ là mép ngoài của cống hộp đổ tại chỗ (ptB - Điểm số 2 trên hình)
                    XYZ pBedA = pFootingBackTL;
                    XYZ pBedB = ptB;
                    btlLeft = PlaceBeddingInstance(doc, compBtl, pBedA, pBedB, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, "Bê tông lót liên ngoài trái", createdBeddings, materialSettings);
                    dddLeft = PlaceBeddingInstance(doc, compDdd, pBedA, pBedB, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, "Đá dăm đệm liên ngoài trái", createdBeddings, materialSettings);
                }
                else if (seg.type == 2)
                {
                    // LIÊN NGOÀI PHẢI (Hình 1 đối xứng & CAD 145):
                    // BTL & Đá dăm: Điểm bắt đầu là mép ngoài của cống hộp đổ tại chỗ (ptA - Điểm số 2 đối xứng),
                    // và điểm cuối là mép sau cấu kiện đá dăm đệm của tường đầu sân cống HL (pFootingBackHL)
                    XYZ pBedA = ptA;
                    XYZ pBedB = pFootingBackHL;
                    btlRight = PlaceBeddingInstance(doc, compBtl, pBedA, pBedB, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, "Bê tông lót liên ngoài phải", createdBeddings, materialSettings);
                    dddRight = PlaceBeddingInstance(doc, compDdd, pBedA, pBedB, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, "Đá dăm đệm liên ngoài phải", createdBeddings, materialSettings);
                }
                else
                {
                    // LIÊN TRONG (Hình 4):
                    // BTL & Đá dăm: Rải từ mép ngoài cống đến mép ngoài cống (ptA đến ptB)
                    XYZ pBedA = ptA;
                    XYZ pBedB = ptB;
                    var btl = PlaceBeddingInstance(doc, compBtl, pBedA, pBedB, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, $"Bê tông lót liên trong {i}", createdBeddings, materialSettings);
                    var ddd = PlaceBeddingInstance(doc, compDdd, pBedA, pBedB, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, $"Đá dăm đệm liên trong {i}", createdBeddings, materialSettings);
                    if (btl != null) btlMidList.Add(btl);
                    if (ddd != null) dddMidList.Add(ddd);
                }
            }

            if (outDetails != null)
            {
                outDetails.BarrelLeft = barrelLeft;
                outDetails.BarrelRight = barrelRight;
                outDetails.MidBarrels = barrelMidList;
                outDetails.BtlLeft = btlLeft;
                outDetails.DddLeft = dddLeft;
                outDetails.BtlRight = btlRight;
                outDetails.DddRight = dddRight;
                outDetails.MidBtls = btlMidList;
                outDetails.MidDdds = dddMidList;
            }
        }

        private static void BuildBranchV2(
            Document doc,
            CulvertRowData data,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IList<CulvertComponentItem> barrelComponents,
            IList<CulvertComponentItem> manholeComponents,
            XYZ p1,
            XYZ p2,
            XYZ u,
            double rotAngle,
            double totalLengthFeet,
            double lStdFeet,
            double jointGapFeet,
            double bBoxFeet,
            CulvertArrayMode arrayMode,
            string branchSuffix,
            IEnumerable<CulvertMaterialItem>? materialSettings = null,
            List<FamilyInstance>? createdBarrels = null,
            List<FamilyInstance>? createdBeddings = null,
            List<FamilyInstance>? createdManholes = null,
            IList<CulvertComponentItem>? outletComponents = null,
            FamilyInstance? sampleOutletInst = null)
        {
            var segmentedBarrelComps = barrelComponents.Where(c => !IsBeddingComponent(c)).ToList();
            var continuousBeddingComps = barrelComponents.Where(c => IsBeddingComponent(c)).ToList();

            bool isVaiKeGlobal = CheckIfVaiKeEnabled(familyParameterMappings, data);

            // Bề rộng và chiều dày thành Hộp nối thực tế (HOP NOI CONG DOC)
            double actualManholeB_M = 2.10;
            double manholeWallT_M = 0.25;
            var manholeComp = manholeComponents.FirstOrDefault(c => c.IsActive && c.SelectedSymbol != null);
            if (manholeComp?.SelectedSymbol != null)
            {
                var mSym = manholeComp.SelectedSymbol.GetFreshSymbol(doc);
                if (mSym != null)
                {
                    var pB = mSym.LookupParameter("HOP NOI CONG_B") ?? mSym.LookupParameter("B_BOX") ?? mSym.LookupParameter("B");
                    if (pB != null && pB.StorageType == StorageType.Double && pB.AsDouble() > 0.1)
                    {
                        actualManholeB_M = UnitUtils.ConvertFromInternalUnits(pB.AsDouble(), UnitTypeId.Meters);
                    }
                    var pT = mSym.LookupParameter("HOP NOI CONG_T") ?? mSym.LookupParameter("T") ?? mSym.LookupParameter("T_THAN");
                    if (pT != null && pT.StorageType == StorageType.Double && pT.AsDouble() > 0.05)
                    {
                        manholeWallT_M = UnitUtils.ConvertFromInternalUnits(pT.AsDouble(), UnitTypeId.Meters);
                    }
                }
            }
            if (actualManholeB_M <= 0.1)
            {
                var sampleMInst = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .FirstOrDefault(fi => fi.Symbol != null && fi.Symbol.FamilyName.Contains("HOP NOI"));
                if (sampleMInst != null)
                {
                    var pB = sampleMInst.LookupParameter("HOP NOI CONG_B") ?? sampleMInst.LookupParameter("B_BOX") ?? sampleMInst.LookupParameter("B");
                    if (pB != null && pB.StorageType == StorageType.Double && pB.AsDouble() > 0.1)
                    {
                        actualManholeB_M = UnitUtils.ConvertFromInternalUnits(pB.AsDouble(), UnitTypeId.Meters);
                    }
                    var pT = sampleMInst.LookupParameter("HOP NOI CONG_T") ?? sampleMInst.LookupParameter("T") ?? sampleMInst.LookupParameter("T_THAN");
                    if (pT != null && pT.StorageType == StorageType.Double && pT.AsDouble() > 0.05)
                    {
                        manholeWallT_M = UnitUtils.ConvertFromInternalUnits(pT.AsDouble(), UnitTypeId.Meters);
                    }
                }
            }
            if (actualManholeB_M <= 0.1) actualManholeB_M = 2.10;
            if (manholeWallT_M <= 0.05) manholeWallT_M = 0.25;

            double wallTFeet = UnitUtils.ConvertToInternalUnits(manholeWallT_M, UnitTypeId.Meters);

            // Cao độ Z bắt đầu và kết thúc của cống hộp (đã trừ L_Ngam_San)
            double lNgamSanM = data.L_Ngam_San > 0 ? data.L_Ngam_San : 0.0;
            double zDauCulvert = data.Z1 - lNgamSanM;
            double zCuoiCulvert = data.Z2 - lNgamSanM;

            // YÊU CẦU 1 (Hình 1 & 2):
            // Lớp BTL và đá dăm đệm bắt đầu từ mép tường đầu cách vô trong thân cống:
            // Cửa xả 1: từ Điểm 1 cách vô trong 1 đoạn đúng bằng CX_TD_DD_W (p1 + u * CX_TD_DD_W)
            // Cửa xả 2: từ Điểm 2 cách vô trong 1 đoạn đúng bằng CX_TD_DD_W (p2 - u * CX_TD_DD_W)
            double cxTdDdW_M = ResolveCxTdDdW(doc, data, familyParameterMappings, outletComponents, sampleOutletInst);
            double cxTdDdW_Feet = UnitUtils.ConvertToInternalUnits(cxTdDdW_M, UnitTypeId.Meters);
            XYZ pBedStart = p1 + u * cxTdDdW_Feet;
            XYZ pBedEnd = p2 - u * cxTdDdW_Feet;

            if (data.SoHopNoi == 0)
            {
                // TH1: Không có hộp nối
                // Rải từ P1 đến P2, đốt cuối co dãn bù trừ sát P2
                if (totalLengthFeet > 0)
                {
                    LayAdaptiveSegmentsBetweenPoints(doc, segmentedBarrelComps, p1, p2, lStdFeet, jointGapFeet, u, rotAngle, arrayMode,
                        bimConfig, customBimParams, familyParameterMappings, data, zDauCulvert, zCuoiCulvert, 1, branchSuffix, materialSettings,
                        createdBarrels, isReverseBuffer: false, isVaiKeGlobal: isVaiKeGlobal, noVaiKeAtStart: true, noVaiKeAtEnd: true);

                    PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, pBedStart, pBedEnd, rotAngle,
                        bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings, createdBeddings);
                }
            }
            else if (data.SoHopNoi == 1)
            {
                // TH3: 1 hộp nối
                // Đoạn 1: Rải từ P1 đến mép thành trong hộp nối (đốt bù trừ co dãn sát mép thành trong hộp nối)
                // Đoạn 2: Rải từ mép thành trong hộp nối đến P2 (đốt bù trừ co dãn sát mép thành trong hộp nối)
                double b1M = (data.B_HT1 > 1.8) ? data.B_HT1 : actualManholeB_M;
                double distHN1M = data.KC_HN1;
                if (distHN1M <= (b1M / 2.0) + 0.2)
                {
                    double totalLenM = UnitUtils.ConvertFromInternalUnits(totalLengthFeet, UnitTypeId.Meters);
                    distHN1M = totalLenM > 12.0 ? 5.18 : Math.Max(2.0, b1M + 1.0);
                }

                double distHN1Feet = UnitUtils.ConvertToInternalUnits(distHN1M, UnitTypeId.Meters);
                double b1Feet = UnitUtils.ConvertToInternalUnits(b1M, UnitTypeId.Meters);
                XYZ pHN1 = p1 + u * distHN1Feet;

                if (string.IsNullOrEmpty(branchSuffix))
                {
                    PlaceManholeAssembly(doc, pHN1, rotAngle, manholeComponents, bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings, createdManholes);
                }

                double rOut1Feet = b1Feet / 2.0;
                double rIn1Feet = Math.Max(0.1, rOut1Feet - wallTFeet);

                XYZ pHN1_in_up = pHN1 - u * rIn1Feet;
                XYZ pHN1_in_dn = pHN1 + u * rIn1Feet;
                XYZ pHN1_out_up = pHN1 - u * rOut1Feet;
                XYZ pHN1_out_dn = pHN1 + u * rOut1Feet;

                // Đoạn 1: P1 -> mép thành trong Hộp 1 (đốt bù trừ co dãn khóa cứng tại pHN1_in_up)
                // Vùng đầu (Cửa xả 1 -> Hộp nối 1): KHÔNG có vai kê
                int dotCount1 = LayAdaptiveSegmentsBetweenPoints(doc, segmentedBarrelComps, p1, pHN1_in_up, lStdFeet, jointGapFeet, u, rotAngle, arrayMode,
                    bimConfig, customBimParams, familyParameterMappings, data, zDauCulvert, null, 1, branchSuffix, materialSettings,
                    createdBarrels, isReverseBuffer: false, isVaiKeGlobal: false, noVaiKeAtStart: true, noVaiKeAtEnd: true);

                // YÊU CẦU 1 (Hình 1): BTL & đá dăm Đoạn 1:
                // Điểm 1 đặt đúng tại tọa độ Điểm 1 trong Excel cách vô CX_TD_DD_W (pBedStart).
                // Điểm 2 đặt tại mép ngoài cùng của hộp nối cống (pHN1_out_up).
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, pBedStart, pHN1_out_up, rotAngle,
                    bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings, createdBeddings);

                // Đoạn 2: Từ mép thành trong Hộp 1 đến P2 (đốt bù trừ co dãn bắt đầu từ mép thành trong: isReverseBuffer = true)
                LayAdaptiveSegmentsBetweenPoints(doc, segmentedBarrelComps, pHN1_in_dn, p2, lStdFeet, jointGapFeet, u, rotAngle, arrayMode,
                    bimConfig, customBimParams, familyParameterMappings, data, null, zCuoiCulvert, dotCount1 + 1, branchSuffix, materialSettings,
                    createdBarrels, isReverseBuffer: true, isVaiKeGlobal: isVaiKeGlobal, noVaiKeAtStart: true, noVaiKeAtEnd: true);

                // YÊU CẦU 1 (Hình 2): BTL & đá dăm Đoạn 2:
                // Điểm 1 đặt tại mép ngoài cùng của hộp nối cống (pHN1_out_dn).
                // Điểm 2 đặt tại tọa độ Điểm 2 trong Excel cách vô CX_TD_DD_W (pBedEnd).
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, pHN1_out_dn, pBedEnd, rotAngle,
                    bimConfig, customBimParams, familyParameterMappings, data, 2, materialSettings, createdBeddings);
            }
            else // data.SoHopNoi >= 2
            {
                // TH2: 2 hộp nối
                // Đoạn 1: Từ P1 đến mép thành trong hộp nối 1 (đốt bù trừ co dãn sát mép thành trong Hộp 1)
                // Đoạn 2: Từ mép thành trong hộp nối 1 đến mép thành trong hộp nối 2 (đốt bù trừ co dãn sát mép thành trong Hộp 2)
                // Đoạn 3: Từ mép thành trong hộp nối 2 đến P2 (đốt bù trừ co dãn bắt đầu từ mép thành trong Hộp 2: isReverseBuffer = true)
                double b1M = (data.B_HT1 > 1.8) ? data.B_HT1 : actualManholeB_M;
                double b2M = (data.B_HT2 > 1.8) ? data.B_HT2 : actualManholeB_M;
                double distHN1M = data.KC_HN1;
                double distHN2M = data.KC_HN2;

                double totalLenM = UnitUtils.ConvertFromInternalUnits(totalLengthFeet, UnitTypeId.Meters);
                if (distHN1M <= (b1M / 2.0) + 0.2)
                {
                    distHN1M = totalLenM > 12.0 ? 5.18 : Math.Max(2.0, b1M + 1.0);
                }
                if (distHN2M <= (b2M / 2.0) + 0.2)
                {
                    distHN2M = totalLenM > 12.0 ? 5.01 : Math.Max(2.0, b2M + 1.0);
                }

                double distHN1Feet = UnitUtils.ConvertToInternalUnits(distHN1M, UnitTypeId.Meters);
                double distHN2Feet = UnitUtils.ConvertToInternalUnits(distHN2M, UnitTypeId.Meters);
                double b1Feet = UnitUtils.ConvertToInternalUnits(b1M, UnitTypeId.Meters);
                double b2Feet = UnitUtils.ConvertToInternalUnits(b2M, UnitTypeId.Meters);

                XYZ pHN1 = p1 + u * distHN1Feet;
                XYZ pHN2 = p2 - u * distHN2Feet;

                if (string.IsNullOrEmpty(branchSuffix))
                {
                    PlaceManholeAssembly(doc, pHN1, rotAngle, manholeComponents, bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings, createdManholes);
                    PlaceManholeAssembly(doc, pHN2, rotAngle, manholeComponents, bimConfig, customBimParams, familyParameterMappings, data, 2, materialSettings, createdManholes);
                }

                double rOut1Feet = b1Feet / 2.0;
                double rIn1Feet = Math.Max(0.1, rOut1Feet - wallTFeet);
                double rOut2Feet = b2Feet / 2.0;
                double rIn2Feet = Math.Max(0.1, rOut2Feet - wallTFeet);

                XYZ pHN1_in_up = pHN1 - u * rIn1Feet;
                XYZ pHN1_in_dn = pHN1 + u * rIn1Feet;
                XYZ pHN1_out_up = pHN1 - u * rOut1Feet;
                XYZ pHN1_out_dn = pHN1 + u * rOut1Feet;

                XYZ pHN2_in_up = pHN2 - u * rIn2Feet;
                XYZ pHN2_in_dn = pHN2 + u * rIn2Feet;
                XYZ pHN2_out_up = pHN2 - u * rOut2Feet;
                XYZ pHN2_out_dn = pHN2 + u * rOut2Feet;

                // Đoạn 1: Từ P1 đến mép thành trong hộp nối 1 (đốt bù trừ co dãn khóa cứng tại pHN1_in_up)
                // Vùng đầu (Cửa xả 1 -> Hộp nối 1) KHÔNG có vai kê
                int dotCount1 = LayAdaptiveSegmentsBetweenPoints(doc, segmentedBarrelComps, p1, pHN1_in_up, lStdFeet, jointGapFeet, u, rotAngle, arrayMode,
                    bimConfig, customBimParams, familyParameterMappings, data, zDauCulvert, null, 1, branchSuffix, materialSettings,
                    createdBarrels, isReverseBuffer: false, isVaiKeGlobal: false, noVaiKeAtStart: true, noVaiKeAtEnd: true);

                // YÊU CẦU 1: BTL & đá dăm Đoạn 1: từ pBedStart đến pHN1_out_up
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, pBedStart, pHN1_out_up, rotAngle,
                    bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings, createdBeddings);

                // Đoạn 2: Từ mép thành trong hộp nối 1 đến mép thành trong hộp nối 2 (đốt bù trừ co dãn khóa cứng tại pHN2_in_up)
                // Vùng giữa (giữa 2 hộp nối): CÓ vai kê
                int dotCount2 = LayAdaptiveSegmentsBetweenPoints(doc, segmentedBarrelComps, pHN1_in_dn, pHN2_in_up, lStdFeet, jointGapFeet, u, rotAngle, arrayMode,
                    bimConfig, customBimParams, familyParameterMappings, data, null, null, dotCount1 + 1, branchSuffix, materialSettings,
                    createdBarrels, isReverseBuffer: false, isVaiKeGlobal: isVaiKeGlobal, noVaiKeAtStart: true, noVaiKeAtEnd: true);

                // BTL & đá dăm Đoạn 2: giữa 2 hộp nối (pHN1_out_dn đến pHN2_out_up)
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, pHN1_out_dn, pHN2_out_up, rotAngle,
                    bimConfig, customBimParams, familyParameterMappings, data, 2, materialSettings, createdBeddings);

                // Đoạn 3: Từ mép thành trong hộp nối 2 đến P2 (đốt bù trừ co dãn bắt đầu từ mép thành trong Hộp 2: isReverseBuffer = true)
                // Vùng cuối (Hộp nối 2 -> Cửa xả 2) KHÔNG có vai kê
                LayAdaptiveSegmentsBetweenPoints(doc, segmentedBarrelComps, pHN2_in_dn, p2, lStdFeet, jointGapFeet, u, rotAngle, arrayMode,
                    bimConfig, customBimParams, familyParameterMappings, data, null, zCuoiCulvert, dotCount2 + 1, branchSuffix, materialSettings,
                    createdBarrels, isReverseBuffer: true, isVaiKeGlobal: false, noVaiKeAtStart: true, noVaiKeAtEnd: true);

                // YÊU CẦU 1: BTL & đá dăm Đoạn 3: từ pHN2_out_dn đến pBedEnd
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, pHN2_out_dn, pBedEnd, rotAngle,
                    bimConfig, customBimParams, familyParameterMappings, data, 3, materialSettings, createdBeddings);
            }
        }

        private static bool IsBeddingComponent(CulvertComponentItem comp)
        {
            string cat = (comp.CategoryType ?? "").ToUpperInvariant();
            string fam = (comp.SelectedSymbol?.FamilyName ?? "").ToUpperInvariant();
            string name = (comp.SelectedSymbol?.TypeName ?? "").ToUpperInvariant();
            string full = $"{cat} {fam} {name}";

            return full.Contains("BE TONG LOT") || full.Contains("BÊ TÔNG LÓT") || full.Contains("BTL") ||
                   full.Contains("DA DAM") || full.Contains("ĐÁ DĂM") || full.Contains("CPDD") ||
                   full.Contains("DEM CONG") || full.Contains("ĐỆM CỐNG") || full.Contains("DEM");
        }

        private static void PlaceContinuousBeddingForSegment(
            Document doc,
            IList<CulvertComponentItem> beddingComponents,
            XYZ pSegStart,
            XYZ pSegEnd,
            double rotAngle,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            CulvertRowData rowData,
            int segIndex,
            IEnumerable<CulvertMaterialItem>? materialSettings,
            List<FamilyInstance>? createdBeddings = null)
        {
            if (beddingComponents == null || beddingComponents.Count == 0) return;

            double lenSegFeet = (pSegEnd - pSegStart).GetLength();
            if (lenSegFeet <= 0.01) return;

            string[] possibleLenParams = new[] { "L", "Length", "ChieuDai", "L_dot_chuan", "L_dot_bu", "Chiều dài", "L_DOT", "CH_L" };

            foreach (var comp in beddingComponents)
            {
                if (!comp.IsActive || comp.SelectedSymbol == null) continue;

                var sym = comp.SelectedSymbol.GetFreshSymbol(doc);
                if (sym == null) continue;

                ActivateSymbol(sym);

                double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
                XYZ offZ = new XYZ(0, 0, offFeetZ);
                XYZ ptA = pSegStart + offZ;
                XYZ ptB = pSegEnd + offZ;

                FamilyInstance? inst = null;
                try
                {
                    if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(sym))
                    {
                        inst = CreateAdaptiveInstanceSafe(doc, sym, new[] { ptA, ptB });
                    }
                    else
                    {
                        XYZ ptMid = (ptA + ptB) * 0.5;
                        inst = CreateInstanceSafe(doc, ptMid, sym);
                        SafeRotate(doc, inst.Id, ptMid, rotAngle);

                        foreach (var pName in possibleLenParams)
                        {
                            Parameter p = inst.LookupParameter(pName);
                            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                            {
                                p.Set(lenSegFeet);
                                break;
                            }
                        }
                    }
                }
                catch { }

                if (inst == null) continue;
                createdBeddings?.Add(inst);

                string tenCauKien = $"{comp.CategoryType}_DOAN_{segIndex}";
                try { BimParameterService.SetElementBimProperties(inst, bimConfig, tenCauKien, comp.CategoryType ?? "", null, null, null, null, null); } catch { }
                try { BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, rowData, comp.CategoryType ?? ""); } catch { }
                string? cipMat = materialSettings?.FirstOrDefault(m => m.CategoryType == comp.CategoryType)?.MaterialName;
                try { BimParameterService.ApplyCustomBimParameters(inst, customBimParams, rowData, comp.CategoryType ?? "Thân cống", segIndex, null, cipMat, doc, bimConfig.AutoCreateSharedParameters); } catch { }
                try { TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType ?? ""); } catch { }
            }
        }

        private static void PlaceManholeAssembly(
            Document doc,
            XYZ ptPlace,
            double rotAngle,
            IList<CulvertComponentItem> manholeComponents,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            CulvertRowData data,
            int manholeIndex,
            IEnumerable<CulvertMaterialItem>? materialSettings,
            List<FamilyInstance>? createdManholes = null)
        {
            if (manholeComponents == null || manholeComponents.Count == 0) return;

            foreach (var comp in manholeComponents)
            {
                if (!comp.IsActive || comp.SelectedSymbol == null) continue;

                var sym = comp.SelectedSymbol.GetFreshSymbol(doc);
                if (sym == null) continue;

                ActivateSymbol(sym);

                double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
                XYZ ptComp = ptPlace + new XYZ(0, 0, offFeetZ);

                FamilyInstance? inst = null;
                try
                {
                    inst = CreateInstanceSafe(doc, ptComp, sym);
                    SafeRotate(doc, inst.Id, ptComp, rotAngle);
                }
                catch { }

                if (inst == null) continue;
                if (createdManholes != null) createdManholes.Add(inst);

                string tenCK = $"{comp.CategoryType}_{manholeIndex}";
                try { BimParameterService.SetElementBimProperties(inst, bimConfig, tenCK, $"HỐ GA {manholeIndex}", null, null, ptComp.X, ptComp.Y, ptComp.Z); } catch { }
                try { BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, data, "Hố ga"); } catch { }
                string? mhMat = materialSettings?.FirstOrDefault(m => m.CategoryType == comp.CategoryType)?.MaterialName;
                try { BimParameterService.ApplyCustomBimParameters(inst, customBimParams, data, comp.CategoryType ?? "Hố ga", manholeIndex, "HG", mhMat, doc, bimConfig.AutoCreateSharedParameters); } catch { }
                try { TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType ?? ""); } catch { }
            }
        }

        private static int LayAdaptiveSegmentsBetweenPoints(
            Document doc,
            IList<CulvertComponentItem> barrelComponents,
            XYZ pStart,
            XYZ pEnd,
            double lStd,
            double jointGap,
            XYZ u,
            double angle,
            CulvertArrayMode mode,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            CulvertRowData rowData,
            double? zDauM,
            double? zCuoiM,
            int startDotIdx,
            string branchSuffix,
            IEnumerable<CulvertMaterialItem>? materialSettings = null,
            List<FamilyInstance>? createdBarrels = null,
            bool isReverseBuffer = false,
            bool isVaiKeGlobal = false,
            bool noVaiKeAtStart = true,
            bool noVaiKeAtEnd = true)
        {
            XYZ v = pEnd - pStart;
            double L = v.GetLength();
            if (L <= 0.001) return startDotIdx - 1;

            int dotIdx = startDotIdx;

            if (mode == CulvertArrayMode.CenterOut && !isReverseBuffer)
            {
                int n = (int)Math.Floor(L / lStd);
                double rem = L - (n * lStd);
                double lBien = rem / 2.0;

                double minSegLenCenterOut = UnitUtils.ConvertToInternalUnits(0.30, UnitTypeId.Meters);
                if (lBien > 0.001 && lBien < minSegLenCenterOut && n >= 2)
                {
                    n -= 2;
                    lBien += lStd;
                }

                int totalDots = n + (lBien > 0.001 ? 2 : 0);
                if (totalDots == 0) return startDotIdx - 1;

                double curDist = 0.0;
                int placedCount = 0;

                // Đốt biên 1
                if (lBien > 0.001)
                {
                    bool isFirst = true;
                    bool isLast = (totalDots == 1);
                    bool hasVaiKe = isVaiKeGlobal && (!isFirst || !noVaiKeAtStart) && (!isLast || !noVaiKeAtEnd);

                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * curDist;
                    XYZ ptB = (totalDots == 1) ? pEnd : (ptA + u * lBien);
                    double actualLen = (ptB - ptA).GetLength();
                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, actualLen, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, zDauM, (totalDots == 1) ? zCuoiM : null, materialSettings, createdBarrels, hasVaiKe);
                    curDist += lBien + jointGap;
                    placedCount++;
                }

                // Các đốt chuẩn ở giữa
                for (int i = 0; i < n; i++)
                {
                    bool isFirst = (placedCount == 0);
                    bool isLast = (placedCount == totalDots - 1);
                    bool hasVaiKe = isVaiKeGlobal && (!isFirst || !noVaiKeAtStart) && (!isLast || !noVaiKeAtEnd);

                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * curDist;
                    XYZ ptB = isLast ? pEnd : (ptA + u * lStd);
                    double actualLen = (ptB - ptA).GetLength();
                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, actualLen, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, isFirst ? zDauM : null, isLast ? zCuoiM : null, materialSettings, createdBarrels, hasVaiKe);
                    curDist += lStd + jointGap;
                    placedCount++;
                }

                // Đốt biên 2
                if (lBien > 0.001 && placedCount < totalDots)
                {
                    bool isFirst = (placedCount == 0);
                    bool isLast = true;
                    bool hasVaiKe = isVaiKeGlobal && (!isFirst || !noVaiKeAtStart) && (!isLast || !noVaiKeAtEnd);

                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * curDist;
                    XYZ ptB = pEnd;
                    double actualLen = (ptB - ptA).GetLength();
                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, actualLen, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, isFirst ? zDauM : null, zCuoiM, materialSettings, createdBarrels, hasVaiKe);
                }

                return dotIdx - 1;
            }

            // Thuật toán OneWay chính xác:
            // Đảm bảo đốt cống co dãn bù trừ khớp tuyệt đối với mép thành trong hộp nối và triệt tiêu sai số cộng dồn
            double pitch = lStd + jointGap;
            double minSegLen = UnitUtils.ConvertToInternalUnits(0.40, UnitTypeId.Meters);

            if (L <= lStd + 0.05)
            {
                // Chỉ có 1 đốt duy nhất kết nối trọn vẹn pStart -> pEnd (đốt co dãn)
                bool hasVaiKe = isVaiKeGlobal && !noVaiKeAtStart && !noVaiKeAtEnd;
                string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                PlaceBarrelComponents(doc, barrelComponents, pStart, pEnd, L, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, zDauM, zCuoiM, materialSettings, createdBarrels, hasVaiKe);
                return dotIdx - 1;
            }

            int N = (int)Math.Floor((L - minSegLen) / pitch);
            if (N < 0) N = 0;

            if (!isReverseBuffer)
            {
                // Buffer ở cuối đoạn (khóa cứng tại pEnd - ví dụ mép thành trong hộp nối hoặc Cửa xả P2)
                // Các đốt chuẩn đi trước từ pStart
                for (int i = 0; i < N; i++)
                {
                    bool isFirst = (i == 0);
                    bool hasVaiKe = isVaiKeGlobal && (!isFirst || !noVaiKeAtStart);

                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * (i * pitch);
                    XYZ ptB = ptA + u * lStd;

                    double? zDauSeg = isFirst ? zDauM : null;
                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, lStd, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, zDauSeg, null, materialSettings, createdBarrels, hasVaiKe);
                }

                // Đốt co dãn bù trừ cuối cùng kết thúc CHÍNH XÁC tại pEnd (khóa cứng mép thành trong hộp nối cống)
                XYZ ptALast = pStart + u * (N * pitch);
                XYZ ptBLast = pEnd; // KHÓA CỨNG VÀO pEnd - TRIỆT TIÊU ĐÂM XUYÊN VÀO HỘP NỐI
                double segLenLast = (ptBLast - ptALast).GetLength();

                if (segLenLast > 0.001)
                {
                    bool hasVaiKe = isVaiKeGlobal && !noVaiKeAtEnd;
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    PlaceBarrelComponents(doc, barrelComponents, ptALast, ptBLast, segLenLast, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, (N == 0) ? zDauM : null, zCuoiM, materialSettings, createdBarrels, hasVaiKe);
                }
            }
            else
            {
                // Buffer ở đầu đoạn (khóa cứng tại pStart - ví dụ mép thành trong hộp nối phía hạ lưu)
                // Đốt co dãn bù trừ bắt đầu CHÍNH XÁC từ pStart
                double lBuffer = L - (N * pitch);
                XYZ ptA0 = pStart; // KHÓA CỨNG VÀO pStart - mép thành trong hộp nối
                XYZ ptB0 = (N == 0) ? pEnd : (ptA0 + u * lBuffer);
                double segLen0 = (ptB0 - ptA0).GetLength();

                if (segLen0 > 0.001)
                {
                    bool hasVaiKe = isVaiKeGlobal && !noVaiKeAtStart && (N > 0 || !noVaiKeAtEnd);
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    PlaceBarrelComponents(doc, barrelComponents, ptA0, ptB0, segLen0, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, zDauM, (N == 0) ? zCuoiM : null, materialSettings, createdBarrels, hasVaiKe);
                }

                // Các đốt chuẩn tiếp theo dẫn tới pEnd
                for (int i = 0; i < N; i++)
                {
                    bool isLast = (i == N - 1);
                    bool hasVaiKe = isVaiKeGlobal && (!isLast || !noVaiKeAtEnd);

                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * (lBuffer + jointGap + i * pitch);
                    XYZ ptB = isLast ? pEnd : (ptA + u * lStd); // Đốt cuối khóa cứng vào pEnd!
                    double segLen = (ptB - ptA).GetLength();

                    double? zCuoiSeg = isLast ? zCuoiM : null;
                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, segLen, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, null, zCuoiSeg, materialSettings, createdBarrels, hasVaiKe);
                }
            }

            return dotIdx - 1;
        }

        private static string FormatDotName(string pattern, int index, string branchSuffix)
        {
            string name = (pattern ?? "DOT_{STT}").Replace("{STT}", index.ToString());
            if (!string.IsNullOrEmpty(branchSuffix))
            {
                name += $".{branchSuffix}";
            }
            return name;
        }

        private static void PlaceBarrelComponents(
            Document doc,
            IList<CulvertComponentItem> barrelComponents,
            XYZ ptStart,
            XYZ ptEnd,
            double lenFeet,
            double angle,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            CulvertRowData rowData,
            string tenCauKien,
            double? zDau,
            double? zCuoi,
            IEnumerable<CulvertMaterialItem>? materialSettings = null,
            List<FamilyInstance>? createdBarrels = null,
            bool hasVaiKe = false)
        {
            if (barrelComponents == null) return;

            string[] possibleLenParams = new[] { "L", "Length", "ChieuDai", "L_dot_chuan", "L_dot_bu", "Chiều dài", "L_DOT" };

            foreach (var comp in barrelComponents)
            {
                if (!comp.IsActive || comp.SelectedSymbol == null) continue;

                var sym = comp.SelectedSymbol.GetFreshSymbol(doc);
                if (sym == null) continue;

                ActivateSymbol(sym);

                double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
                XYZ offZ = new XYZ(0, 0, offFeetZ);
                XYZ ptA = ptStart + offZ;
                XYZ ptB = ptEnd + offZ;

                FamilyInstance? inst = null;
                try
                {
                    if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(sym))
                    {
                        inst = CreateAdaptiveInstanceSafe(doc, sym, new[] { ptA, ptB });
                    }
                    else
                    {
                        XYZ ptMid = (ptA + ptB) * 0.5;
                        inst = CreateInstanceSafe(doc, ptMid, sym);
                        SafeRotate(doc, inst.Id, ptMid, angle);
                    }
                }
                catch { }

                if (inst == null) continue;
                createdBarrels?.Add(inst);

                try
                {
                    // Gán tham số VAI / CO VAI KE / TAI theo đúng vị trí đốt cống
                    string[] vaiKeParams = new[] { 
                        "VAI", "CO VAI KE", "CO_VAI_KE", "VAI KE", "VAI_KE", 
                        "CO VAI", "CO_VAI", "CO TAI", "CO_TAI", "TAI",
                        "TAI CONG", "TAI_CONG", "CỐNG HỘP CÓ TAI", "CONG HOP CO TAI"
                    };
                    foreach (var vkName in vaiKeParams)
                    {
                        var pInst = inst.LookupParameter(vkName);
                        if (pInst != null && !pInst.IsReadOnly && pInst.StorageType == StorageType.Integer)
                        {
                            pInst.Set(hasVaiKe ? 1 : 0);
                        }
                        else if (pInst == null && inst.Symbol != null)
                        {
                            var pSym = inst.Symbol.LookupParameter(vkName);
                            if (pSym != null && !pSym.IsReadOnly && pSym.StorageType == StorageType.Integer)
                            {
                                try { pSym.Set(hasVaiKe ? 1 : 0); } catch { }
                            }
                        }
                    }
                }
                catch { }

                try
                {
                    foreach (var pName in possibleLenParams)
                    {
                        Parameter p = inst.LookupParameter(pName);
                        if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                        {
                            p.Set(lenFeet);
                            break;
                        }
                    }
                }
                catch { }

                string nameSub = $"{tenCauKien}_{comp.CategoryType}";
                try { BimParameterService.SetElementBimProperties(inst, bimConfig, nameSub, comp.CategoryType, zDau, zCuoi, null, null, null); } catch { }
                try { BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, rowData, comp.CategoryType); } catch { }
                
                int? parsedDot = null;
                var matchDot = System.Text.RegularExpressions.Regex.Match(tenCauKien, @"\d+");
                if (matchDot.Success && int.TryParse(matchDot.Value, out int dotVal))
                    parsedDot = dotVal;

                string? barrelMat = materialSettings?.FirstOrDefault(m => m.CategoryType == comp.CategoryType)?.MaterialName;
                try { BimParameterService.ApplyCustomBimParameters(inst, customBimParams, rowData, comp.CategoryType ?? "Thân cống", parsedDot, null, barrelMat, doc, bimConfig.AutoCreateSharedParameters); } catch { }
                try { TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType); } catch { }
            }
        }

        private static void TryApplyMaterial(
            Document doc,
            FamilyInstance inst,
            IEnumerable<CulvertMaterialItem>? materialSettings,
            string componentCategory,
            string? culvertType = null)
        {
            if (materialSettings == null || inst == null) return;

            string cat = componentCategory.Trim();
            bool isOutlet = cat.Contains("Cửa xả") || cat.Contains("Tường đầu") || cat.Contains("Tường cánh") || cat.Contains("Sân cống");
            bool isApron = cat.Contains("Sân gia cố") || cat.Contains("Tấm sân");
            bool isTech = cat.Contains("Hố kỹ thuật") || cat.Contains("kỹ thuật") || (culvertType != null && culvertType.Contains("KỸ THUẬT"));
            bool isManhole = cat.Contains("Hố ga") || cat.Contains("Hộp nối");

            string targetGroup = "";
            if (isOutlet) targetGroup = "CỬA XẢ";
            else if (isApron) targetGroup = "SÂN GIA CỐ";
            else if (isTech) targetGroup = "CỐNG KỸ THUẬT";
            else if (isManhole) targetGroup = "HỐ GA (HỘP NỐI)";
            else if (!string.IsNullOrEmpty(culvertType)) targetGroup = culvertType;
            else targetGroup = "CỐNG ĐÚC SẴN";

            // 1. Khớp chính xác cả Group/CulvertType và CategoryType
            CulvertMaterialItem? item = materialSettings.FirstOrDefault(m =>
                m.IsActive &&
                (string.Equals(m.CulvertType, targetGroup, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(m.GroupType, targetGroup, StringComparison.OrdinalIgnoreCase) ||
                 (targetGroup.Contains("ĐỔ TẠI CHỖ") && m.CulvertType != null && m.CulvertType.Contains("đổ tại chỗ", StringComparison.OrdinalIgnoreCase)) ||
                 (targetGroup.Contains("ĐÚC SẴN") && m.CulvertType != null && m.CulvertType.Contains("đúc sẵn", StringComparison.OrdinalIgnoreCase))) &&
                (string.Equals(m.CategoryType, cat, StringComparison.OrdinalIgnoreCase) ||
                 m.CategoryType.IndexOf(cat, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 cat.IndexOf(m.CategoryType, StringComparison.OrdinalIgnoreCase) >= 0));

            // 2. Khớp CategoryType trong cùng targetGroup nếu có lớp đệm (Đá dăm / BTL)
            if (item == null && !string.IsNullOrEmpty(targetGroup))
            {
                item = materialSettings.FirstOrDefault(m =>
                    m.IsActive &&
                    (string.Equals(m.CulvertType, targetGroup, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(m.GroupType, targetGroup, StringComparison.OrdinalIgnoreCase)) &&
                    ((cat.Contains("đá dăm") && m.CategoryType.Contains("đá dăm")) ||
                     (cat.Contains("lót") && m.CategoryType.Contains("lót")) ||
                     (cat.Contains("thân") && m.CategoryType.Contains("thân"))));
            }

            // 3. Fallback theo CategoryType
            if (item == null)
            {
                item = materialSettings.FirstOrDefault(m =>
                    m.IsActive &&
                    (string.Equals(m.CategoryType, cat, StringComparison.OrdinalIgnoreCase) ||
                     m.CategoryType.IndexOf(cat, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     cat.IndexOf(m.CategoryType, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            if (item != null)
            {
                BimMaterialService.ApplyMaterialToInstance(doc, inst, item);
            }
        }

        /// <summary>
        /// Tự động phân định nhóm cấu kiện chuẩn từ thông tin Family, Type và Parameter của instance
        /// </summary>
        public static string ResolveInstanceCategory(FamilyInstance inst)
        {
            if (inst == null) return "Thân cống";

            string pTenCK = inst.LookupParameter("Tên cấu kiện")?.AsString() ?? "";
            string pMoTa = inst.LookupParameter("Mô tả")?.AsString() ?? "";
            string famName = inst.Symbol?.FamilyName ?? "";
            string typeName = inst.Name ?? "";
            string allNames = $"{pTenCK} {pMoTa} {famName} {typeName}".ToUpperInvariant();

            if (allNames.Contains("TƯỜNG ĐẦU") || allNames.Contains("TUONG DAU"))
                return "Tường đầu";
            if (allNames.Contains("TƯỜNG CÁNH") || allNames.Contains("TUONG CANH"))
                return "Tường cánh";
            if (allNames.Contains("SÂN GIA CỐ") || allNames.Contains("SAN GIA CO") || allNames.Contains("SGC"))
                return "Sân gia cố";
            if (allNames.Contains("SÂN CỐNG") || allNames.Contains("SAN CONG"))
                return "Sân cống";
            if (allNames.Contains("HỐ KỸ THUẬT") || allNames.Contains("HO KY THUAT") || allNames.Contains("HKT"))
                return "Hố kỹ thuật";
            if (allNames.Contains("HỘP NỐI") || allNames.Contains("HOP NOI"))
                return "Hộp nối cống";
            if (allNames.Contains("HỐ GA") || allNames.Contains("HO GA"))
                return "Hố ga";
            if (allNames.Contains("BÊ TÔNG LÓT") || allNames.Contains("BE TONG LOT") || allNames.Contains("BTL"))
                return "Bê tông lót";
            if (allNames.Contains("ĐÁ DĂM") || allNames.Contains("DA DAM") || allNames.Contains("DDD"))
                return "Đá dăm đệm";
            if (allNames.Contains("LIÊN NGOÀI TRÁI") || allNames.Contains("LIEN NGOAI TRAI"))
                return "Liên ngoài trái";
            if (allNames.Contains("LIÊN TRONG") || allNames.Contains("LIEN TRONG"))
                return "Liên trong";
            if (allNames.Contains("LIÊN NGOÀI PHẢI") || allNames.Contains("LIEN NGOAI PHAI"))
                return "Liên ngoài phải";

            return "Thân cống";
        }

        /// <summary>
        /// Tìm tất cả các FamilyInstance trong Revit Document thuộc về một cống cụ thể (theo STT, Lý trình hoặc vị trí tọa độ).
        /// </summary>
        public static List<FamilyInstance> FindExistingCulvertInstances(
            Document doc,
            CulvertRowData row,
            IList<CulvertRowData>? allRows = null,
            bool useSurveyPoint = true)
        {
            var matched = new List<FamilyInstance>();
            if (doc == null || row == null) return matched;

            string sttStr = row.STT.ToString();
            string lyTrinhStr = row.LyTrinh?.Trim() ?? "";
            bool isTechnical = row.IsTechnical || (row.LoaiCong != null && (row.LoaiCong.Contains("kỹ thuật") || row.LoaiCong.Contains("CKT")));

            XYZ p1 = CoordinateService.ConvertVN2000ToRevitInternal(doc, row.X1, row.Y1, row.Z1, useSurveyPoint);
            XYZ p2 = CoordinateService.ConvertVN2000ToRevitInternal(doc, row.X2, row.Y2, row.Z2, useSurveyPoint);
            XYZ culvertVec = p2 - p1;
            double culvertLen = culvertVec.GetLength();
            XYZ culvertDir = culvertLen > 1e-4 ? culvertVec.Normalize() : XYZ.BasisX;

            var allInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(fi => fi.IsValidObject)
                .ToList();

            foreach (var fi in allInstances)
            {
                var pMark = fi.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                string mark = pMark?.AsString()?.Trim() ?? "";
                var pComments = fi.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                string comm = pComments?.AsString()?.Trim() ?? "";
                string famName = fi.Symbol?.FamilyName ?? "";

                // Phân định tuyệt đối giữa Cống Kỹ Thuật (Sheet 2) và Cống Ngang (Sheet 1)
                bool isFiTechnical = mark.StartsWith("CKT", StringComparison.OrdinalIgnoreCase) ||
                                     comm.Contains("kỹ thuật", StringComparison.OrdinalIgnoreCase) ||
                                     comm.Contains("CKT", StringComparison.OrdinalIgnoreCase) ||
                                     famName.Contains("TNN_CKT", StringComparison.OrdinalIgnoreCase) ||
                                     famName.Contains("HO KY THUAT", StringComparison.OrdinalIgnoreCase);

                if (isTechnical != isFiTechnical)
                {
                    continue;
                }

                // 1. Kiểm tra tham số BIM_LyTrinh hoặc Lý trình
                var pBimLyTrinh = fi.LookupParameter("BIM_LyTrinh") ?? fi.LookupParameter("Lý trình");
                if (pBimLyTrinh != null && !string.IsNullOrWhiteSpace(pBimLyTrinh.AsString()) && !string.IsNullOrWhiteSpace(lyTrinhStr))
                {
                    if (string.Equals(pBimLyTrinh.AsString().Trim(), lyTrinhStr, StringComparison.OrdinalIgnoreCase))
                    {
                        matched.Add(fi);
                        continue;
                    }
                }

                // 2. Kiểm tra tham số Comments (Ghi chú Revit mặc định)
                if (!string.IsNullOrEmpty(comm))
                {
                    if (!string.IsNullOrEmpty(lyTrinhStr) && comm.IndexOf(lyTrinhStr, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matched.Add(fi);
                        continue;
                    }
                    if (System.Text.RegularExpressions.Regex.IsMatch(comm, $@"\b(Cống|Cong)\s+{sttStr}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                        System.Text.RegularExpressions.Regex.IsMatch(comm, $@"\bCKT\s*{sttStr}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        matched.Add(fi);
                        continue;
                    }
                }

                // 3. Kiểm tra tham số Mark (Ký hiệu cấu kiện)
                if (!string.IsNullOrEmpty(mark))
                {
                    string expectedPrefix = isTechnical ? "CKT" : "CN";
                    if (string.Equals(mark, $"{expectedPrefix}-{sttStr}", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(mark, sttStr, StringComparison.OrdinalIgnoreCase) ||
                        mark.StartsWith($"{expectedPrefix}-{sttStr}-", StringComparison.OrdinalIgnoreCase) ||
                        mark.StartsWith($"{expectedPrefix}-{sttStr}_", StringComparison.OrdinalIgnoreCase))
                    {
                        matched.Add(fi);
                        continue;
                    }
                }

                // 4. Nhận diện theo vùng hành lang tọa độ 3D
                bool isCulvertFamily = famName.IndexOf("CONG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      famName.IndexOf("CỐNG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      famName.IndexOf("CX_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      famName.IndexOf("TNN_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      famName.IndexOf("SAN GIA CO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      famName.IndexOf("SGC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      famName.IndexOf("HOP NOI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      famName.IndexOf("HO KY THUAT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      famName.IndexOf("HO GA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      famName.IndexOf("MASS_", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isCulvertFamily && culvertLen > 1e-4)
                {
                    XYZ? pt = (fi.Location as LocationPoint)?.Point;
                    if (pt == null)
                    {
                        var bbox = fi.get_BoundingBox(null);
                        if (bbox != null) pt = (bbox.Min + bbox.Max) * 0.5;
                    }

                    if (pt != null)
                    {
                        XYZ diff = pt - p1;
                        double t = diff.DotProduct(culvertDir);
                        double bufFeet = 16.4; // 5 mét đệm đầu/cuối
                        if (t >= -bufFeet && t <= culvertLen + bufFeet)
                        {
                            XYZ projPt = p1 + culvertDir * t;
                            double perpDistFeet = pt.DistanceTo(projPt);
                            if (perpDistFeet <= 32.8) // Hành lang 10 mét
                            {
                                bool isClosestToThisRow = true;
                                if (allRows != null && allRows.Count > 1)
                                {
                                    foreach (var otherRow in allRows)
                                    {
                                        if (otherRow.STT == row.STT) continue;
                                        XYZ op1 = CoordinateService.ConvertVN2000ToRevitInternal(doc, otherRow.X1, otherRow.Y1, otherRow.Z1, useSurveyPoint);
                                        XYZ op2 = CoordinateService.ConvertVN2000ToRevitInternal(doc, otherRow.X2, otherRow.Y2, otherRow.Z2, useSurveyPoint);
                                        XYZ oVec = op2 - op1;
                                        double oLen = oVec.GetLength();
                                        if (oLen < 1e-4) continue;
                                        XYZ oDir = oVec.Normalize();
                                        double ot = (pt - op1).DotProduct(oDir);
                                        XYZ oProj = op1 + oDir * Math.Clamp(ot, 0.0, oLen);
                                        double oDist = pt.DistanceTo(oProj);
                                        if (oDist < perpDistFeet)
                                        {
                                            isClosestToThisRow = false;
                                            break;
                                        }
                                    }
                                }

                                if (isClosestToThisRow)
                                {
                                    matched.Add(fi);
                                }
                            }
                        }
                    }
                }
            }

            return matched.GroupBy(e => e.Id.Value).Select(g => g.First()).ToList();
        }

        /// <summary>
        /// Cập nhật thông tin BIM, tham số hình học/mapping và vật liệu cho các cấu kiện đã có sẵn trong mô hình Revit
        /// </summary>
        public static int UpdateExistingCulvertElements(
            Document doc,
            IList<FamilyInstance> instances,
            CulvertRowData row,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IEnumerable<CulvertMaterialItem>? materialSettings)
        {
            if (instances == null || instances.Count == 0) return 0;
            int count = 0;
            string culvertType = row.DetermineCulvertType();

            foreach (var inst in instances)
            {
                if (inst == null || !inst.IsValidObject) continue;

                string cat = ResolveInstanceCategory(inst);

                // 1. Cập nhật thông tin BIM chung (Tên công trình, Tên nhóm, Tên cấu kiện, Mô tả, Cao độ, Tọa độ)
                string? existingTenCK = inst.LookupParameter("Tên cấu kiện")?.AsString();
                string tenCK = !string.IsNullOrWhiteSpace(existingTenCK) ? existingTenCK : cat;
                try
                {
                    BimParameterService.SetElementBimProperties(inst, bimConfig, tenCK, null, null, null, null, null, null);
                }
                catch { }

                // 2. Gán các tham số BIM tùy biến từ Tab 04
                try
                {
                    string? mat = materialSettings?.FirstOrDefault(m => m.CategoryType == cat)?.MaterialName;
                    BimParameterService.ApplyCustomBimParameters(inst, customBimParams, row, cat, null, null, mat, doc, bimConfig.AutoCreateSharedParameters);
                }
                catch { }

                // 3. Gán các tham số hình học / Dimensions đã tùy chỉnh từ Tab 02
                try
                {
                    BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, row, cat);
                }
                catch { }

                // 4. Gán vật liệu và đồ họa hiển thị từ Tab 03
                try
                {
                    TryApplyMaterial(doc, inst, materialSettings, cat, culvertType);
                }
                catch { }

                // 5. Đảm bảo các tham số nhận diện cơ bản luôn đồng bộ
                try
                {
                    bool isTech = row.IsTechnical || (row.LoaiCong != null && (row.LoaiCong.Contains("kỹ thuật") || row.LoaiCong.Contains("CKT")));
                    string markVal = isTech ? $"CKT-{row.STT}" : $"CN-{row.STT}";
                    string commVal = isTech ? $"Cống kỹ thuật {row.STT} ({row.LyTrinh})" : $"Cống {row.STT} - {row.LoaiCong} ({row.LyTrinh})";

                    var pMark = inst.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    if (pMark != null && !pMark.IsReadOnly)
                    {
                        pMark.Set(markVal);
                    }
                    var pComments = inst.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (pComments != null && !pComments.IsReadOnly)
                    {
                        pComments.Set(commVal);
                    }
                    var pBimLyTrinh = inst.LookupParameter("BIM_LyTrinh") ?? inst.LookupParameter("Lý trình");
                    if (pBimLyTrinh != null && !pBimLyTrinh.IsReadOnly)
                    {
                        pBimLyTrinh.Set(row.LyTrinh ?? "");
                    }
                }
                catch { }

                count++;
            }

            return count;
        }

        /// <summary>
        /// Quét và tự động gán lại toàn bộ thông tin BIM và Vật liệu vào các cống đã dựng trong Revit
        /// </summary>
        public static (int SuccessCount, int ErrorCount, int TotalElementsUpdated, List<string> Logs) UpdateCulvertBimAndMaterials(
            Document doc,
            IList<CulvertRowData> culvertList,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IEnumerable<CulvertMaterialItem>? materialSettings,
            bool useSurveyPoint = true)
        {
            var logs = new List<string>();
            int successCount = 0;
            int errorCount = 0;
            int totalUpdated = 0;

            if (culvertList == null || culvertList.Count == 0)
            {
                logs.Add("Không có cống nào được chọn để cập nhật.");
                return (0, 0, 0, logs);
            }

            using (var tg = new TransactionGroup(doc, "INFRA BIM - Cập nhật Thông tin & Vật liệu Cống"))
            {
                tg.Start();

                foreach (var row in culvertList)
                {
                    if (!row.IsSelected) continue;

                    var existing = FindExistingCulvertInstances(doc, row, culvertList, useSurveyPoint);
                    if (existing == null || existing.Count == 0)
                    {
                        logs.Add($"⚠️ Cống STT {row.STT} ({row.LyTrinh}): Chưa tìm thấy mô hình trong Revit. Vui lòng bấm 'THỰC HIỆN TẠO MÔ HÌNH' để dựng mới.");
                        errorCount++;
                        continue;
                    }

                    using (var subT = new Transaction(doc, $"Cập nhật BIM & Vật liệu Cống STT {row.STT}"))
                    {
                        try
                        {
                            var failOpts = subT.GetFailureHandlingOptions();
                            failOpts.SetFailuresPreprocessor(new SuppressAllWarningsFailuresPreprocessor());
                            failOpts.SetClearAfterRollback(true);
                            subT.SetFailureHandlingOptions(failOpts);

                            subT.Start();
                            int updated = UpdateExistingCulvertElements(
                                doc,
                                existing,
                                row,
                                bimConfig,
                                customBimParams,
                                familyParameterMappings,
                                materialSettings);
                            subT.Commit();

                            successCount++;
                            totalUpdated += updated;
                            logs.Add($"✅ Cống STT {row.STT} ({row.LyTrinh}): Đã cập nhật thành công thông tin BIM & vật liệu cho {updated} cấu kiện.");
                        }
                        catch (Exception ex)
                        {
                            subT.RollBack();
                            errorCount++;
                            logs.Add($"❌ Lỗi khi cập nhật cống STT {row.STT} ({row.LyTrinh}): {ex.Message}");
                        }
                    }
                }

                tg.Assimilate();
            }

            return (successCount, errorCount, totalUpdated, logs);
        }
    }

    /// <summary>
    /// Bộ xử lý cảnh báo tự động triệt tiêu các thông báo Warning/Failures modal của Revit
    /// Giúp tiến trình dựng hình không bị chặn lại bởi các hộp thoại thông báo lỗi phụ của Family
    /// </summary>
    public class SuppressAllWarningsFailuresPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failureMessages = failuresAccessor.GetFailureMessages();
            bool hasUnresolvedError = false;
            bool resolvedAny = false;
            foreach (var fmsg in failureMessages)
            {
                var severity = fmsg.GetSeverity();
                if (severity == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(fmsg);
                }
                else if (severity == FailureSeverity.Error)
                {
                    if (failuresAccessor.IsFailureResolutionPermitted(fmsg))
                    {
                        failuresAccessor.ResolveFailure(fmsg);
                        resolvedAny = true;
                    }
                    else
                    {
                        hasUnresolvedError = true;
                    }
                }
            }
            if (hasUnresolvedError)
            {
                // Triệt tiêu hoàn toàn hộp thoại modal Error không thể bỏ qua của Revit bằng cách chỉ đạo RollBack
                return FailureProcessingResult.ProceedWithRollBack;
            }
            if (resolvedAny)
            {
                return FailureProcessingResult.ProceedWithCommit;
            }
            return FailureProcessingResult.Continue;
        }
    }
}
