using System;
using System.Collections.Generic;
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
            IEnumerable<CulvertMaterialItem>? materialSettings = null)
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

                    using (var subT = new Transaction(doc, $"Rải cống STT {row.STT} ({row.LyTrinh})"))
                    {
                        try
                        {
                            var failOpts = subT.GetFailureHandlingOptions();
                            failOpts.SetFailuresPreprocessor(new SuppressAllWarningsFailuresPreprocessor());
                            failOpts.SetClearAfterRollback(true);
                            subT.SetFailureHandlingOptions(failOpts);

                            subT.Start();

                            // Phân định dòng cống này thuộc loại Đúc sẵn hay Đổ tại chỗ
                            bool isRowCastInPlace = (row.SoCua > 1) ||
                                (row.CauKien?.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (row.CauKien?.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (row.LoaiCong?.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (row.LoaiCong?.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (row.GhiChu?.IndexOf("đổ tại chỗ", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                (row.GhiChu?.IndexOf("do tai cho", StringComparison.OrdinalIgnoreCase) >= 0);

                            var targetBarrelList = isRowCastInPlace ? castInPlaceBarrelComponents : precastBarrelComponents;
                            double curLStd = isRowCastInPlace ? lStdCastInPlaceM : lStdPrecastM;
                            double curJointGap = isRowCastInPlace ? jointGapCastInPlaceM : jointGapPrecastM;
                            CulvertArrayMode curArrayMode = isRowCastInPlace ? arrayModeCastInPlace : arrayModePrecast;

                            // Tự động nhận diện / bảo toàn Family tương ứng cho từng dòng cống
                            var curBarrel = ResolveComponentsForCulvert(doc, targetBarrelList, row);
                            var curOutlet = ResolveComponentsForCulvert(doc, outletComponents, row);
                            var curApron = ResolveComponentsForCulvert(doc, apronComponents, row);
                            var curManhole = ResolveComponentsForCulvert(doc, manholeComponents, row);

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
                                materialSettings);

                            subT.Commit();
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

        private static IList<CulvertComponentItem> ResolveComponentsForCulvert(
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
                    if (!full.Contains("HOP NOI") && !full.Contains("HỘP NỐI") && !full.Contains("TNM_HG") && !full.Contains("HO GA") && !full.Contains("HỐ GA"))
                        continue;

                    score += 50;
                    if (hintUpper.Contains("CỔ GIẾNG") && full.Contains("CO GIENG")) score += 30;
                    else if (hintUpper.Contains("KHUÔN") && full.Contains("KHUON")) score += 30;
                    else if (hintUpper.Contains("NẮP") && full.Contains("NAP")) score += 30;
                    else if (hintUpper.Contains("LÓT") && (full.Contains("LOT") || full.Contains("BTL"))) score += 30;
                    else if (hintUpper.Contains("HỘP NỐI") && (full.Contains("HOP NOI") || full.Contains("HỘP NỐI"))) score += 30;
                }
                else // isSearchingBarrel
                {
                    if (full.Contains("TNN_CX") || full.Contains("CUA XA") || full.Contains("SAN CONG") ||
                        full.Contains("SAN GIA CO") || full.Contains("SGC") ||
                        full.Contains("HOP NOI") || full.Contains("HỘP NỐI") || full.Contains("TNM_HG") || full.Contains("HO GA"))
                        continue;

                    if (!full.Contains("TNN_CH") && !full.Contains("TNN_CT") && !full.Contains("CONG HOP") && !full.Contains("THAN CONG"))
                        continue;

                    score += 50;

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
            IEnumerable<CulvertMaterialItem>? materialSettings = null)
        {
            // 1. Chuyển đổi tọa độ VN2000 sang Revit Internal (Feet)
            XYZ p1 = CoordinateService.ConvertVN2000ToRevitInternal(doc, data.X1, data.Y1, data.Z1, useSurveyPoint);
            XYZ p2 = CoordinateService.ConvertVN2000ToRevitInternal(doc, data.X2, data.Y2, data.Z2, useSurveyPoint);

            XYZ v3D = p2 - p1;
            double totalLengthFeet = v3D.GetLength();
            if (totalLengthFeet < 0.01)
                throw new InvalidOperationException("Chiều dài cống quá nhỏ (P1 trùng P2).");

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

            var outletInstances = new List<FamilyInstance>();
            var barrelInstances = new List<FamilyInstance>();
            var beddingInstances = new List<FamilyInstance>();

            PlaceOutletAssembly(doc, p1, u, rotOutletTL, outletComponents, bimConfig, customBimParams, familyParameterMappings, data, isUpstream: true, lSanCongFeet, materialSettings, outletInstances);
            PlaceOutletAssembly(doc, p2, u, rotOutletHL, outletComponents, bimConfig, customBimParams, familyParameterMappings, data, isUpstream: false, lSanCongFeet, materialSettings, outletInstances);

            // 3. Đặt Sân gia cố Thượng lưu & Hạ lưu:
            // YÊU CẦU 1 (Hình 1): Điểm 1 của Sân gia cố adaptive đặt đúng tại vị trí mép chân khay của Sân cống (đúng khoảng cách lSanCongM = 2.16m từ tim P1/P2)
            double lOuterCX_M = lSanCongM;
            double lOuterCX_Feet = UnitUtils.ConvertToInternalUnits(lOuterCX_M, UnitTypeId.Meters);

            XYZ uOutTL = -new XYZ(u.X, u.Y, 0).Normalize();
            XYZ uOutHL = new XYZ(u.X, u.Y, 0).Normalize();
            XYZ ptSC2_TL = p1 + uOutTL * lOuterCX_Feet;
            XYZ ptSC2_HL = p2 + uOutHL * lOuterCX_Feet;

            double lSgcTL = data.L_SGC_TL > 0.05 ? data.L_SGC_TL : 3.0;
            double lSgcHL = data.L_SGC_HL > 0.05 ? data.L_SGC_HL : 3.0;

            PlaceApronAssembly(doc, ptSC2_TL, u, apronComponents, bimConfig, customBimParams, familyParameterMappings, data, isUpstream: true, lSgcTL, materialSettings);
            PlaceApronAssembly(doc, ptSC2_HL, u, apronComponents, bimConfig, customBimParams, familyParameterMappings, data, isUpstream: false, lSgcHL, materialSettings);

            // 4. Rải thân cống và hộp nối: Offset xuống 1 đoạn L_ngam_san như trong file Excel & Hình 4
            double lNgamSanM = data.L_Ngam_San > 0 ? data.L_Ngam_San : 0.0;
            double lNgamSanFeet = UnitUtils.ConvertToInternalUnits(lNgamSanM, UnitTypeId.Meters);
            XYZ p1_Culvert = p1 - new XYZ(0, 0, lNgamSanFeet);
            XYZ p2_Culvert = p2 - new XYZ(0, 0, lNgamSanFeet);

            BuildBranchV2(doc, data, bimConfig, customBimParams, familyParameterMappings, barrelComponents, manholeComponents,
                p1_Culvert, p2_Culvert, u, rotAngle, totalLengthFeet, lStdFeet, jointGapFeet, bBoxFeet, arrayMode, "", materialSettings,
                barrelInstances, beddingInstances);

            // 5. YÊU CẦU 1 & 6: Cut Geometry
            // Yêu cầu 1: Cut geometry giữa BTL & đá dăm đệm cống hộp với khối Cửa xả (Hình 1)
            foreach (var bed in beddingInstances)
            {
                foreach (var outlet in outletInstances)
                {
                    TryCutGeometrySafe(doc, bed, outlet);
                }
            }

            // Yêu cầu 6: Cut geometry giữa thân cống hộp với Cửa xả (tường đầu)
            foreach (var bar in barrelInstances)
            {
                foreach (var outlet in outletInstances)
                {
                    TryCutGeometrySafe(doc, outlet, bar);
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
                try { BimParameterService.ApplyCustomBimParameters(inst, customBimParams, data, "Cửa xả"); } catch { }
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
            IEnumerable<CulvertMaterialItem>? materialSettings)
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
                try { BimParameterService.ApplyCustomBimParameters(inst, customBimParams, data, "Sân gia cố"); } catch { }
                try { TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType ?? ""); } catch { }
            }
        }

        private static void TryCutGeometrySafe(Document doc, Element? eToCut, Element? eCutter)
        {
            if (doc == null || eToCut == null || eCutter == null || !eToCut.IsValidObject || !eCutter.IsValidObject) return;
            if (eToCut.Id == eCutter.Id) return;

            try
            {
                if (SolidSolidCutUtils.CanElementCutElement(eCutter, eToCut, out _))
                {
                    SolidSolidCutUtils.AddCutBetweenSolids(doc, eToCut, eCutter);
                    return;
                }
            }
            catch { }

            try
            {
                if (SolidSolidCutUtils.CanElementCutElement(eToCut, eCutter, out _))
                {
                    SolidSolidCutUtils.AddCutBetweenSolids(doc, eCutter, eToCut);
                    return;
                }
            }
            catch { }

            try
            {
                if (!JoinGeometryUtils.AreElementsJoined(doc, eToCut, eCutter))
                {
                    JoinGeometryUtils.JoinGeometry(doc, eToCut, eCutter);
                }
            }
            catch { }
        }

        private static bool CheckIfVaiKeEnabled(
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            CulvertRowData data)
        {
            if (familyParameterMappings != null)
            {
                var item = familyParameterMappings.FirstOrDefault(m =>
                {
                    string name = (m.InternalName ?? "").ToUpperInvariant();
                    string disp = (m.DisplayName ?? "").ToUpperInvariant();
                    return name == "CO VAI KE" || name == "CO_VAI_KE" || name.Contains("VAI KE") || name.Contains("VAI_KE") ||
                           disp.Contains("VAI KE") || disp.Contains("VAI_KE");
                });

                if (item != null)
                {
                    string val = data.GetParamOverride(item.InternalName, item.CustomValue);
                    val = (val ?? "").Trim().ToLowerInvariant();
                    if (val == "1" || val == "yes" || val == "true" || val == "có" || val == "co")
                    {
                        return true;
                    }
                    if (val == "0" || val == "no" || val == "false" || val == "không" || val == "khong")
                    {
                        return false;
                    }
                    if (item.IsSelected)
                    {
                        return true;
                    }
                }
            }

            if (data.ParameterOverrides != null)
            {
                foreach (var kvp in data.ParameterOverrides)
                {
                    string key = kvp.Key.ToUpperInvariant();
                    if (key == "CO VAI KE" || key == "CO_VAI_KE" || key.Contains("VAI KE") || key.Contains("VAI_KE"))
                    {
                        string val = (kvp.Value ?? "").Trim().ToLowerInvariant();
                        if (val == "1" || val == "yes" || val == "true" || val == "có" || val == "co")
                        {
                            return true;
                        }
                        if (val == "0" || val == "no" || val == "false" || val == "không" || val == "khong")
                        {
                            return false;
                        }
                    }
                }
            }

            return false;
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
            List<FamilyInstance>? createdBeddings = null)
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

            if (data.SoHopNoi == 0)
            {
                // TH1: Không có hộp nối
                // Rải từ P1 đến P2, đốt cuối co dãn bù trừ sát P2
                if (totalLengthFeet > 0)
                {
                    LayAdaptiveSegmentsV2(doc, segmentedBarrelComps, p1, totalLengthFeet, lStdFeet, jointGapFeet, u, rotAngle, arrayMode,
                        bimConfig, customBimParams, familyParameterMappings, data, zDauCulvert, zCuoiCulvert, 1, branchSuffix, materialSettings,
                        createdBarrels, isReverseBuffer: false, isVaiKeGlobal: isVaiKeGlobal);

                    PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, p1, totalLengthFeet, u, rotAngle,
                        bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings, createdBeddings);
                }
            }
            else if (data.SoHopNoi == 1)
            {
                // TH3: 1 hộp nối
                // Đoạn 1: Rải từ P1 đến mép thành trong hộp nối (đốt bù trừ sát hộp nối)
                // Đoạn 2: Rải từ P2 đến mép thành trong hộp nối (đốt bù trừ sát hộp nối)
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
                    PlaceManholeAssembly(doc, pHN1, rotAngle, manholeComponents, bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings);
                }

                double rOut1Feet = b1Feet / 2.0;
                double rIn1Feet = Math.Max(0.1, rOut1Feet - wallTFeet);

                // Đoạn 1: P1 -> mép thành trong Hộp 1
                double len1_barrel = Math.Max(0.0, distHN1Feet - rIn1Feet);
                int dotCount1 = LayAdaptiveSegmentsV2(doc, segmentedBarrelComps, p1, len1_barrel, lStdFeet, jointGapFeet, u, rotAngle, arrayMode,
                    bimConfig, customBimParams, familyParameterMappings, data, zDauCulvert, null, 1, branchSuffix, materialSettings,
                    createdBarrels, isReverseBuffer: false, isVaiKeGlobal: isVaiKeGlobal);

                // BTL & đá dăm Đoạn 1: đến mép ngoài hộp nối
                double len1_bedding = Math.Max(0.0, distHN1Feet - rOut1Feet);
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, p1, len1_bedding, u, rotAngle,
                    bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings, createdBeddings);

                // Đoạn 2: Từ mép thành trong Hộp 1 đến P2 (rải hướng về hộp nối, đốt bù trừ sát hộp nối: isReverseBuffer = true)
                XYZ pStart2_barrel = pHN1 + u * rIn1Feet;
                double len2_barrel = Math.Max(0.0, totalLengthFeet - distHN1Feet - rIn1Feet);
                LayAdaptiveSegmentsV2(doc, segmentedBarrelComps, pStart2_barrel, len2_barrel, lStdFeet, jointGapFeet, u, rotAngle, arrayMode,
                    bimConfig, customBimParams, familyParameterMappings, data, null, zCuoiCulvert, dotCount1 + 1, branchSuffix, materialSettings,
                    createdBarrels, isReverseBuffer: true, isVaiKeGlobal: isVaiKeGlobal);

                // BTL & đá dăm Đoạn 2: từ mép ngoài hộp nối đến P2
                XYZ pStart2_bedding = pHN1 + u * rOut1Feet;
                double len2_bedding = Math.Max(0.0, totalLengthFeet - distHN1Feet - rOut1Feet);
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, pStart2_bedding, len2_bedding, u, rotAngle,
                    bimConfig, customBimParams, familyParameterMappings, data, 2, materialSettings, createdBeddings);
            }
            else // data.SoHopNoi >= 2
            {
                // TH2: 2 hộp nối
                // Đoạn 1: Từ P1 đến mép thành trong hộp nối 1 (đốt bù trừ sát hộp 1)
                // Đoạn 2: Từ mép thành trong hộp nối 1 đến mép thành trong hộp nối 2 (đốt bù trừ sát hộp 2)
                // Đoạn 3: Từ P2 đến mép thành trong hộp nối 2 (đốt bù trừ sát hộp 2, isReverseBuffer = true)
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
                    PlaceManholeAssembly(doc, pHN1, rotAngle, manholeComponents, bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings);
                    PlaceManholeAssembly(doc, pHN2, rotAngle, manholeComponents, bimConfig, customBimParams, familyParameterMappings, data, 2, materialSettings);
                }

                double rOut1Feet = b1Feet / 2.0;
                double rIn1Feet = Math.Max(0.1, rOut1Feet - wallTFeet);
                double rOut2Feet = b2Feet / 2.0;
                double rIn2Feet = Math.Max(0.1, rOut2Feet - wallTFeet);

                // Đoạn 1: Từ P1 đến mép thành trong hộp nối 1
                double len1_barrel = Math.Max(0.0, distHN1Feet - rIn1Feet);
                int dotCount1 = LayAdaptiveSegmentsV2(doc, segmentedBarrelComps, p1, len1_barrel, lStdFeet, jointGapFeet, u, rotAngle, arrayMode,
                    bimConfig, customBimParams, familyParameterMappings, data, zDauCulvert, null, 1, branchSuffix, materialSettings,
                    createdBarrels, isReverseBuffer: false, isVaiKeGlobal: isVaiKeGlobal);

                double len1_bedding = Math.Max(0.0, distHN1Feet - rOut1Feet);
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, p1, len1_bedding, u, rotAngle,
                    bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings, createdBeddings);

                // Đoạn 2: Từ mép thành trong hộp nối 1 đến mép thành trong hộp nối 2
                XYZ pStart2_barrel = pHN1 + u * rIn1Feet;
                double len2_barrel = Math.Max(0.0, (pHN2 - pHN1).GetLength() - rIn1Feet - rIn2Feet);
                int dotCount2 = LayAdaptiveSegmentsV2(doc, segmentedBarrelComps, pStart2_barrel, len2_barrel, lStdFeet, jointGapFeet, u, rotAngle, arrayMode,
                    bimConfig, customBimParams, familyParameterMappings, data, null, null, dotCount1 + 1, branchSuffix, materialSettings,
                    createdBarrels, isReverseBuffer: false, isVaiKeGlobal: isVaiKeGlobal);

                XYZ pStart2_bedding = pHN1 + u * rOut1Feet;
                double len2_bedding = Math.Max(0.0, (pHN2 - pHN1).GetLength() - rOut1Feet - rOut2Feet);
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, pStart2_bedding, len2_bedding, u, rotAngle,
                    bimConfig, customBimParams, familyParameterMappings, data, 2, materialSettings, createdBeddings);

                // Đoạn 3: Từ mép thành trong hộp nối 2 đến P2 (đốt bù trừ sát hộp 2: isReverseBuffer = true)
                XYZ pStart3_barrel = pHN2 + u * rIn2Feet;
                double len3_barrel = Math.Max(0.0, distHN2Feet - rIn2Feet);
                LayAdaptiveSegmentsV2(doc, segmentedBarrelComps, pStart3_barrel, len3_barrel, lStdFeet, jointGapFeet, u, rotAngle, arrayMode,
                    bimConfig, customBimParams, familyParameterMappings, data, null, zCuoiCulvert, dotCount2 + 1, branchSuffix, materialSettings,
                    createdBarrels, isReverseBuffer: true, isVaiKeGlobal: isVaiKeGlobal);

                XYZ pStart3_bedding = pHN2 + u * rOut2Feet;
                double len3_bedding = Math.Max(0.0, distHN2Feet - rOut2Feet);
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, pStart3_bedding, len3_bedding, u, rotAngle,
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
            double lenSegFeet,
            XYZ u,
            double rotAngle,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            CulvertRowData rowData,
            int segIndex,
            IEnumerable<CulvertMaterialItem>? materialSettings,
            List<FamilyInstance>? createdBeddings = null)
        {
            if (beddingComponents == null || beddingComponents.Count == 0 || lenSegFeet <= 0.01) return;

            XYZ pSegEnd = pSegStart + u * lenSegFeet;
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
                try { BimParameterService.ApplyCustomBimParameters(inst, customBimParams, rowData, "Thân cống"); } catch { }
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
            IEnumerable<CulvertMaterialItem>? materialSettings)
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

                string tenCK = $"{comp.CategoryType}_{manholeIndex}";
                try { BimParameterService.SetElementBimProperties(inst, bimConfig, tenCK, $"HỐ GA {manholeIndex}", null, null, ptComp.X, ptComp.Y, ptComp.Z); } catch { }
                try { BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, data, "Hố ga"); } catch { }
                try { BimParameterService.ApplyCustomBimParameters(inst, customBimParams, data, "Hố ga"); } catch { }
                try { TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType ?? ""); } catch { }
            }
        }

        private static int LayAdaptiveSegmentsV2(
            Document doc,
            IList<CulvertComponentItem> barrelComponents,
            XYZ pStart,
            double L,
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
            bool isVaiKeGlobal = false)
        {
            if (L <= 0.001) return startDotIdx - 1;

            int dotIdx = startDotIdx;

            if (mode == CulvertArrayMode.CenterOut)
            {
                int n = (int)Math.Floor(L / lStd);
                double rem = L - (n * lStd);
                double lBien = rem / 2.0;

                double minSegLen = UnitUtils.ConvertToInternalUnits(0.30, UnitTypeId.Meters);
                if (lBien > 0.001 && lBien < minSegLen && n >= 2)
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
                    bool hasVaiKe = isVaiKeGlobal && (!isFirst && !isLast);

                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * curDist;
                    XYZ ptB = ptA + u * lBien;
                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, lBien, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, zDauM, (totalDots == 1) ? zCuoiM : null, materialSettings, createdBarrels, hasVaiKe);
                    curDist += lBien + jointGap;
                    placedCount++;
                }

                // Các đốt chuẩn ở giữa
                for (int i = 0; i < n; i++)
                {
                    bool isFirst = (placedCount == 0);
                    bool isLast = (placedCount == totalDots - 1);
                    bool hasVaiKe = isVaiKeGlobal && (!isFirst && !isLast);

                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * curDist;
                    XYZ ptB = ptA + u * lStd;
                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, lStd, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, isFirst ? zDauM : null, isLast ? zCuoiM : null, materialSettings, createdBarrels, hasVaiKe);
                    curDist += lStd + jointGap;
                    placedCount++;
                }

                // Đốt biên 2
                if (lBien > 0.001)
                {
                    bool isFirst = (placedCount == 0);
                    bool isLast = true;
                    bool hasVaiKe = isVaiKeGlobal && (!isFirst && !isLast);

                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * curDist;
                    XYZ ptB = ptA + u * lBien;
                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, lBien, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, isFirst ? zDauM : null, zCuoiM, materialSettings, createdBarrels, hasVaiKe);
                }
            }
            else // OneWay
            {
                // Chia chiều dài đoạn rải thành danh sách đốt chuẩn và đốt co dãn bù trừ hợp lý
                // Triệt tiêu hoàn toàn lỗi sinh đốt vụn 2cm thừa thãi (như Hình 3, 4, 5)
                List<double> segLens = CalculateSegmentLengths(L, lStd, isReverseBuffer);
                int totalDots = segLens.Count;
                if (totalDots == 0) return startDotIdx - 1;

                double curDist = 0.0;
                for (int i = 0; i < totalDots; i++)
                {
                    double segLen = segLens[i];
                    bool isFirst = (i == 0);
                    bool isLast = (i == totalDots - 1);
                    bool hasVaiKe = isVaiKeGlobal && (!isFirst && !isLast);

                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * curDist;
                    XYZ ptB = ptA + u * segLen;

                    double? zDauSeg = isFirst ? zDauM : null;
                    double? zCuoiSeg = isLast ? zCuoiM : null;

                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, segLen, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, zDauSeg, zCuoiSeg, materialSettings, createdBarrels, hasVaiKe);
                    curDist += segLen + jointGap;
                }
            }

            return dotIdx - 1;
        }

        private static List<double> CalculateSegmentLengths(double L, double lStd, bool isReverseBuffer)
        {
            var lengths = new List<double>();
            if (L <= 0.001) return lengths;

            if (L <= lStd + 0.05)
            {
                lengths.Add(L);
                return lengths;
            }

            int n = (int)Math.Floor(L / lStd);
            double rem = L - (n * lStd);

            // Ngưỡng đốt cống tối thiểu (0.40m):
            // Nếu phần dư < 0.40m (ví dụ 2cm, 5cm), gộp vào đốt chuẩn cuối cùng
            // để tạo thành đốt co dãn bù trừ thực tế (ví dụ 1.02m), không tạo đốt vụn riêng!
            double minSegLen = UnitUtils.ConvertToInternalUnits(0.40, UnitTypeId.Meters);

            int numStd;
            double bufferLen;

            if (rem < 0.001)
            {
                numStd = n;
                bufferLen = 0.0;
            }
            else if (rem < minSegLen && n >= 1)
            {
                numStd = n - 1;
                bufferLen = lStd + rem;
            }
            else
            {
                numStd = n;
                bufferLen = rem;
            }

            if (isReverseBuffer)
            {
                // Đốt co dãn bù trừ nằm ở đầu đoạn (sát mép thành trong hộp nối)
                if (bufferLen > 0.001)
                {
                    lengths.Add(bufferLen);
                }
                for (int i = 0; i < numStd; i++)
                {
                    lengths.Add(lStd);
                }
            }
            else
            {
                // Các đốt chuẩn đi trước, đốt co dãn bù trừ nằm ở cuối đoạn (sát mép thành trong hộp nối)
                for (int i = 0; i < numStd; i++)
                {
                    lengths.Add(lStd);
                }
                if (bufferLen > 0.001)
                {
                    lengths.Add(bufferLen);
                }
            }

            return lengths;
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
                    // Gán tham số CO VAI KE theo đúng vị trí đốt cống (Yêu cầu 3 & 5)
                    SetParamYesNo(inst, "CO VAI KE", hasVaiKe ? 1 : 0);
                    SetParamYesNo(inst, "CO_VAI_KE", hasVaiKe ? 1 : 0);
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
                try { BimParameterService.ApplyCustomBimParameters(inst, customBimParams, rowData, "Thân cống"); } catch { }
                try { TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType); } catch { }
            }
        }

        private static void TryApplyMaterial(
            Document doc,
            FamilyInstance inst,
            IEnumerable<CulvertMaterialItem>? materialSettings,
            string componentCategory)
        {
            if (materialSettings == null || inst == null) return;

            var item = materialSettings.FirstOrDefault(m => 
                m.IsActive && 
                (string.Equals(m.CategoryType, componentCategory, StringComparison.OrdinalIgnoreCase) ||
                 m.CategoryType.Contains(componentCategory, StringComparison.OrdinalIgnoreCase) ||
                 componentCategory.Contains(m.CategoryType, StringComparison.OrdinalIgnoreCase)));

            if (item != null)
            {
                BimMaterialService.ApplyMaterialToInstance(doc, inst, item);
            }
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
                        return FailureProcessingResult.ProceedWithCommit;
                    }
                }
            }
            return FailureProcessingResult.Continue;
        }
    }
}
