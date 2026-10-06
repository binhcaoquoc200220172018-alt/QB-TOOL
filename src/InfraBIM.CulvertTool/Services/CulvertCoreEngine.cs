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
        /// Xây dựng toàn bộ các cống ngang theo chuẩn 3 cụm cấu kiện (Thân cống, Cửa xả, Hố ga)
        /// </summary>
        public static (int SuccessCount, int ErrorCount, List<string> Logs) BuildAllCulvertsV2(
            Document doc,
            IList<CulvertRowData> culvertList,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IList<CulvertComponentItem> barrelComponents,
            IList<CulvertComponentItem> outletComponents,
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
                    tAct.Start();
                    foreach (var c in barrelComponents.Concat(outletComponents).Concat(manholeComponents))
                    {
                        if (c.IsActive && c.SelectedSymbol?.Symbol != null)
                        {
                            ActivateSymbol(c.SelectedSymbol.Symbol);
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
                            subT.Start();

                            // Tự động nhận diện Family tương ứng cho từng dòng cống theo Loại cống, Khẩu độ, Cấu kiện
                            var curBarrel = ResolveComponentsForCulvert(doc, barrelComponents, row);
                            var curOutlet = ResolveComponentsForCulvert(doc, outletComponents, row);
                            var curManhole = ResolveComponentsForCulvert(doc, manholeComponents, row);

                            BuildSingleCulvertV2(
                                doc,
                                row,
                                bimConfig,
                                customBimParams,
                                familyParameterMappings,
                                curBarrel,
                                curOutlet,
                                curManhole,
                                lStdM,
                                jointGapM,
                                bBoxM,
                                defaultKhoangCachTim,
                                arrayMode,
                                isCastInPlace,
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
                            logs.Add($"❌ Lỗi khi tạo cống STT {row.STT}: {ex.Message}");
                        }
                    }
                }

                tg.Assimilate();
            }

            return (successCount, errorCount, logs);
        }

        private static void ActivateSymbol(FamilySymbol? sym)
        {
            if (sym != null && !sym.IsActive)
            {
                sym.Activate();
            }
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
                FamilyInstance inst = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, sym);
                IList<ElementId> placePointIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(inst);
                for (int i = 0; i < Math.Min(points.Count, placePointIds.Count); i++)
                {
                    if (doc.GetElement(placePointIds[i]) is ReferencePoint refPt)
                    {
                        refPt.Position = points[i];
                    }
                }
                return inst;
            }
            else
            {
                XYZ midPt = points[0];
                if (points.Count > 1)
                {
                    midPt = (points[0] + points[1]) * 0.5;
                }
                return CreateInstanceSafe(doc, midPt, sym);
            }
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
            var p = inst.LookupParameter(paramName);
            if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Integer)
            {
                p.Set(value);
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
                    var matchSym = ResolveFamilyForCulvert(allSymbols, loaiCong, cauKien, ghiChu, row.SoCua, row.KhauDo, copy.CategoryType ?? "");
                    if (matchSym != null)
                    {
                        copy.SelectedSymbol = new FamilySymbolWrapper(matchSym);
                        ActivateSymbol(matchSym);
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
                                     hintUpper.Contains("SÂN GIA CỐ") || hintUpper.Contains("SAN GIA CO") ||
                                     hintUpper.Contains("SGC") || hintUpper.Contains("TƯỜNG ĐẦU") ||
                                     hintUpper.Contains("TUONG DAU") || hintUpper.Contains("TƯỜNG CÁNH") ||
                                     hintUpper.Contains("TUONG CANH");

            bool isSearchingManhole = hintUpper.Contains("HỘP NỐI") || hintUpper.Contains("HOP NOI") ||
                                      hintUpper.Contains("HỐ GA") || hintUpper.Contains("HO GA") ||
                                      hintUpper.Contains("CỔ GIẾNG") || hintUpper.Contains("CO GIENG") ||
                                      hintUpper.Contains("KHUÔN HẦM") || hintUpper.Contains("KHUON HAM") ||
                                      hintUpper.Contains("NẮP ĐAN") || hintUpper.Contains("NAP DAN");

            bool isSearchingBarrel = !isSearchingOutlet && !isSearchingManhole;

            var candidates = new List<(FamilySymbol Symbol, int Score)>();

            foreach (var sym in symbols)
            {
                string fam = sym.FamilyName.ToUpperInvariant();
                string name = sym.Name.ToUpperInvariant();
                string full = $"{fam} {name}";
                int score = 0;

                if (isSearchingOutlet)
                {
                    // Cửa xả & Sân gia cố: BẮT BUỘC chỉ match TNN_CX hoặc SAN CONG / SAN GIA CO
                    if (full.Contains("TNN_CH") || full.Contains("THAN CONG") || full.Contains("HOP NOI") || full.Contains("TNM_HG"))
                        continue;
                    if (!full.Contains("TNN_CX") && !full.Contains("SAN CONG") && !full.Contains("SAN GIA CO") && !full.Contains("SGC"))
                        continue;

                    score += 50;

                    if (hintUpper.Contains("SÂN GIA CỐ") || hintUpper.Contains("SAN GIA CO") || hintUpper.Contains("SGC"))
                    {
                        if (!full.Contains("SAN GIA CO") && !full.Contains("SGC")) continue;
                        score += 30;

                        if (hintUpper.Contains("LÓT") || hintUpper.Contains("LOT") || hintUpper.Contains("BTL"))
                        {
                            if (full.Contains("LOT") || full.Contains("BTL")) score += 20;
                        }
                        else
                        {
                            if (!full.Contains("LOT") && !full.Contains("BTL")) score += 20;
                        }
                    }
                    else // Sân cống (TNN_CX_SAN CONG)
                    {
                        if (full.Contains("SAN GIA CO") || full.Contains("SGC")) continue;
                        if (!full.Contains("SAN CONG") && !full.Contains("TNN_CX")) continue;
                        score += 30;

                        if (hintUpper.Contains("TƯỜNG ĐẦU") || hintUpper.Contains("TUONG DAU"))
                        {
                            if (full.Contains("TUONG DAU")) score += 25;
                        }
                        else if (hintUpper.Contains("TƯỜNG CÁNH") || hintUpper.Contains("TUONG CANH"))
                        {
                            if (full.Contains("TUONG CANH")) score += 25;
                        }
                        else if (hintUpper.Contains("BÊ TÔNG LÓT") || hintUpper.Contains("BE TONG LOT") || hintUpper.Contains("BTL"))
                        {
                            if (full.Contains("BE TONG LOT") || full.Contains("BTL")) score += 25;
                        }
                        else if (hintUpper.Contains("ĐÁ DĂM") || hintUpper.Contains("DA DAM"))
                        {
                            if (full.Contains("DA DAM")) score += 25;
                        }
                        else if (hintUpper.Contains("SÂN CỐNG") || hintUpper.Contains("SAN CONG") || hintUpper.Contains("BẢN ĐÁY"))
                        {
                            if (name.Contains("SAN CONG")) score += 25;
                        }
                    }
                }
                else if (isSearchingManhole)
                {
                    // Hố ga & Hộp nối: BẮT BUỘC chỉ match HOP NOI hoặc TNM_HG
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
                else // isSearchingBarrel (Thân cống, BTL thân cống, Đá dăm đệm thân cống)
                {
                    // Thân cống: TUYỆT ĐỐI KHÔNG match Cửa xả (TNN_CX), Sân gia cố (SAN GIA CO), Hộp nối (HOP NOI), Hố ga (TNM_HG)
                    if (full.Contains("TNN_CX") || full.Contains("CUA XA") || full.Contains("SAN CONG") ||
                        full.Contains("SAN GIA CO") || full.Contains("SGC") ||
                        full.Contains("HOP NOI") || full.Contains("HỘP NỐI") || full.Contains("TNM_HG") || full.Contains("HO GA"))
                        continue;

                    // Bắt buộc thuộc hệ TNN_CH (Cống hộp) hoặc TNN_CT (Cống tròn)
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
                    else // Thân cống (đốt cống)
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
        /// Xây dựng 1 cụm cống ngang theo chuẩn 3 cụm cấu kiện (Thân cống, Cửa xả, Hố ga)
        /// </summary>
        public static void BuildSingleCulvertV2(
            Document doc,
            CulvertRowData data,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IList<CulvertComponentItem> barrelComponents,
            IList<CulvertComponentItem> outletComponents,
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
            // Trong hệ thống Family Revit của dự án (TNN_SAN GIA CO_CUA XA & TNN_CUA XA):
            // Hình học hướng thoát nước của Cửa xả & Sân gia cố nằm dọc theo trục Facing (+Y).
            // - Hạ lưu (P2): Thoát nước xuôi dòng theo +u -> Facing = +u -> quay góc (rotAngle - PI/2)
            // - Thượng lưu (P1): Đón nước ngược dòng từ taluy vào cống theo -u -> Facing = -u -> quay góc (rotAngle + PI/2)
            double rotOutletTL = rotAngle + Math.PI / 2.0;
            double rotOutletHL = rotAngle - Math.PI / 2.0;
            PlaceOutletAssembly(doc, p1, u, rotOutletTL, outletComponents, bimConfig, customBimParams, familyParameterMappings, data, isUpstream: true, materialSettings);
            PlaceOutletAssembly(doc, p2, u, rotOutletHL, outletComponents, bimConfig, customBimParams, familyParameterMappings, data, isUpstream: false, materialSettings);

            // 3. Rải thân cống và hộp nối theo tim cống trung tâm (xoay theo rotAngle của tim cống)
            BuildBranchV2(doc, data, bimConfig, customBimParams, familyParameterMappings, barrelComponents, manholeComponents,
                p1, p2, u, rotAngle, totalLengthFeet, lStdFeet, jointGapFeet, bBoxFeet, arrayMode, "", materialSettings);
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
            IEnumerable<CulvertMaterialItem>? materialSettings)
        {
            if (outletComponents == null || outletComponents.Count == 0) return;

            // Hướng vector trải dài của cửa xả / sân cống ra phía ngoài
            XYZ uOut = isUpstream
                ? -new XYZ(uCulvert.X, uCulvert.Y, 0).Normalize()
                : new XYZ(uCulvert.X, uCulvert.Y, 0).Normalize();

            // Chiều dài thiết kế của Sân cống (TNN_CX_SAN CONG): 1.35m
            double lSanCongFeet = UnitUtils.ConvertToInternalUnits(1.35, UnitTypeId.Meters);

            XYZ ptSC1 = ptBase;
            XYZ ptSC2 = ptBase + uOut * lSanCongFeet;

            foreach (var comp in outletComponents)
            {
                if (!comp.IsActive || comp.SelectedSymbol?.Symbol == null) continue;

                var sym = comp.SelectedSymbol.Symbol;
                ActivateSymbol(sym);

                string catUpper = (comp.CategoryType ?? "").ToUpperInvariant();
                bool isSGC = catUpper.Contains("SÂN GIA CỐ") || catUpper.Contains("SAN GIA CO") || catUpper.Contains("SGC");

                XYZ pA;
                XYZ pB;

                if (isSGC)
                {
                    // Sân gia cố (TNN_CX_SAN GIA CO):
                    // 2 điểm Adaptive chạy dọc theo hướng thoát nước uOut (từ mép sân cống ptSC2 ra dầm chân khay)
                    // Chiều dài thiết kế chuẩn theo CAD Hình 2: 3.0m (2.6m sân + 0.4m dầm chân khay)
                    double lSgcM = 3.0;
                    var pLParam = sym.LookupParameter("CH_SGC_L") ?? sym.LookupParameter("L") ?? sym.LookupParameter("Length");
                    if (pLParam != null && pLParam.StorageType == StorageType.Double && pLParam.AsDouble() > 0.1)
                    {
                        lSgcM = UnitUtils.ConvertFromInternalUnits(pLParam.AsDouble(), UnitTypeId.Meters);
                    }
                    double lSgcFeet = UnitUtils.ConvertToInternalUnits(lSgcM, UnitTypeId.Meters);

                    // Điểm 1: Mép tiếp giáp sân cống (ptSC2)
                    // Điểm 2: Mép ngoài cùng tại dầm chân khay (ptSC2 + uOut * lSgcFeet)
                    pA = ptSC2;
                    pB = ptSC2 + uOut * lSgcFeet;
                }
                else
                {
                    // Sân cống (TNN_CX_SAN CONG):
                    // Đảo ngược 2 điểm Adaptive để tường đầu tựa vào thân cống tại ptBase,
                    // sân cống và tường cánh mở loe ra ngoài hạ lưu/thượng lưu theo CAD (sửa lỗi xoay 180°):
                    // Điểm 1: Mép ngoài sân cống (ptSC2)
                    // Điểm 2: Tường đầu tiếp giáp thân cống (ptSC1 = ptBase)
                    pA = ptSC2;
                    pB = ptSC1;
                }

                double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
                XYZ offZ = new XYZ(0, 0, offFeetZ);
                XYZ pA_off = pA + offZ;
                XYZ pB_off = pB + offZ;

                FamilyInstance? inst = null;
                if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(sym))
                {
                    inst = CreateAdaptiveInstanceSafe(doc, sym, new[] { pA_off, pB_off });
                }
                else
                {
                    inst = CreateInstanceSafe(doc, pA_off, sym);
                    double rotOut = Math.Atan2(uOut.Y, uOut.X);
                    ElementTransformUtils.RotateElement(doc, inst.Id, Line.CreateBound(pA_off, pA_off + XYZ.BasisZ), rotOut);
                }

                if (inst == null) continue;

                // Chuẩn hóa góc xiên thiết kế CH_GX (không gán góc phương vị Azimuth của tuyến vào để tránh làm vẹo tường cánh)
                var pGx = inst.LookupParameter("CH_GX");
                if (pGx != null && !pGx.IsReadOnly && pGx.StorageType == StorageType.Double)
                {
                    pGx.Set(Math.PI / 2.0); // 90° cống vuông góc chuẩn
                }
                var pGxC = inst.LookupParameter("A_GOC XIENG") ?? inst.LookupParameter("CH_SGC_GX");
                if (pGxC != null && !pGxC.IsReadOnly && pGxC.StorageType == StorageType.Double)
                {
                    pGxC.Set(0.0); // 0° góc xiên chuẩn
                }

                // Tự động điều khiển biến hiển thị Yes/No của cấu kiện
                SetOutletVisibilitySafe(inst, comp.CategoryType ?? "");

                string suffix = isUpstream ? "TL" : "HL";
                string tenCK = $"{comp.CategoryType}_{suffix}";
                BimParameterService.SetElementBimProperties(inst, bimConfig, tenCK, isUpstream ? "CỬA XẢ THƯỢNG LƯU" : "CỬA XẢ HẠ LƯU", null, null, pA_off.X, pA_off.Y, pA_off.Z);
                BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, data, "Cửa xả");
                BimParameterService.ApplyCustomBimParameters(inst, customBimParams, data, "Cửa xả");
                TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType ?? "");
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
            IEnumerable<CulvertMaterialItem>? materialSettings = null)
        {
            // Tách cấu kiện thân cống (rải theo đốt 1m) và cấu kiện đệm/lót (rải dải liên tục theo từng phân đoạn - CAD Hình 5)
            var segmentedBarrelComps = barrelComponents.Where(c => !IsBeddingComponent(c)).ToList();
            var continuousBeddingComps = barrelComponents.Where(c => IsBeddingComponent(c)).ToList();

            if (data.SoHopNoi == 0)
            {
                // TH1: Không hộp nối, rải suốt chiều dài cống
                if (totalLengthFeet > 0)
                {
                    LayAdaptiveSegmentsV2(doc, segmentedBarrelComps, p1, totalLengthFeet, lStdFeet, jointGapFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, data.Z1, data.Z2, 1, branchSuffix, materialSettings);
                    PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, p1, totalLengthFeet, u, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings);
                }
            }
            else if (data.SoHopNoi == 1)
            {
                // TH2.1: Có 1 hộp nối
                double b1M = data.B_HT1 > 0.1 ? data.B_HT1 : 1.50;
                double distHN1M = data.KC_HN1;
                // Nếu khoảng cách hố ga quá nhỏ (< nửa bề rộng hố ga), tự động bảo vệ theo cự ly thiết kế
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

                // Đoạn 1: P1 -> Hộp 1 (sau tường đầu đến mép trước hộp nối)
                double len1 = Math.Max(0.0, distHN1Feet - (b1Feet / 2.0));
                int dotCount1 = LayAdaptiveSegmentsV2(doc, segmentedBarrelComps, p1, len1, lStdFeet, jointGapFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, data.Z1, null, 1, branchSuffix, materialSettings);
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, p1, len1, u, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings);

                // Đoạn 2: Hộp 1 -> P2 (mép sau hộp nối đến sau tường đầu hạ lưu)
                XYZ pStart2 = pHN1 + u * (b1Feet / 2.0);
                double len2 = Math.Max(0.0, totalLengthFeet - distHN1Feet - (b1Feet / 2.0));
                LayAdaptiveSegmentsV2(doc, segmentedBarrelComps, pStart2, len2, lStdFeet, jointGapFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, null, data.Z2, dotCount1 + 1, branchSuffix, materialSettings);
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, pStart2, len2, u, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, 2, materialSettings);
            }
            else // data.SoHopNoi >= 2
            {
                // TH2.2: Có 2 hộp nối
                double b1M = data.B_HT1 > 0.1 ? data.B_HT1 : 1.50;
                double b2M = data.B_HT2 > 0.1 ? data.B_HT2 : 1.50;
                double distHN1M = data.KC_HN1;
                double distHN2M = data.KC_HN2;

                // Nếu khoảng cách hố ga quá nhỏ (< nửa bề rộng hố ga), tự động bảo vệ theo cự ly thiết kế
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

                // Đoạn 1: P1 -> Hộp 1
                double len1 = Math.Max(0.0, distHN1Feet - (b1Feet / 2.0));
                int dotCount1 = LayAdaptiveSegmentsV2(doc, segmentedBarrelComps, p1, len1, lStdFeet, jointGapFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, data.Z1, null, 1, branchSuffix, materialSettings);
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, p1, len1, u, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings);

                // Đoạn 2: Hộp 1 -> Hộp 2 (từ mép hộp nối này đến mép hộp nối kia - CAD Hình 5)
                XYZ pStart2 = pHN1 + u * (b1Feet / 2.0);
                double len2 = Math.Max(0.0, (pHN2 - pHN1).GetLength() - ((b1Feet + b2Feet) / 2.0));
                int dotCount2 = LayAdaptiveSegmentsV2(doc, segmentedBarrelComps, pStart2, len2, lStdFeet, jointGapFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, null, null, dotCount1 + 1, branchSuffix, materialSettings);
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, pStart2, len2, u, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, 2, materialSettings);

                // Đoạn 3: Hộp 2 -> P2 (từ mép hộp nối đến sau tường đầu kia - CAD Hình 5)
                XYZ pStart3 = pHN2 + u * (b2Feet / 2.0);
                double len3 = Math.Max(0.0, distHN2Feet - (b2Feet / 2.0));
                LayAdaptiveSegmentsV2(doc, segmentedBarrelComps, pStart3, len3, lStdFeet, jointGapFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, null, data.Z2, dotCount2 + 1, branchSuffix, materialSettings);
                PlaceContinuousBeddingForSegment(doc, continuousBeddingComps, pStart3, len3, u, rotAngle, bimConfig, customBimParams, familyParameterMappings, data, 3, materialSettings);
            }
        }

        private static bool IsBeddingComponent(CulvertComponentItem comp)
        {
            string cat = (comp.CategoryType ?? "").ToUpperInvariant();
            string fam = (comp.SelectedSymbol?.Symbol?.FamilyName ?? "").ToUpperInvariant();
            string name = (comp.SelectedSymbol?.Symbol?.Name ?? "").ToUpperInvariant();
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
            IEnumerable<CulvertMaterialItem>? materialSettings)
        {
            if (beddingComponents == null || beddingComponents.Count == 0 || lenSegFeet <= 0.01) return;

            XYZ pSegEnd = pSegStart + u * lenSegFeet;
            string[] possibleLenParams = new[] { "L", "Length", "ChieuDai", "L_dot_chuan", "L_dot_bu", "Chiều dài", "L_DOT", "CH_L" };

            foreach (var comp in beddingComponents)
            {
                if (!comp.IsActive || comp.SelectedSymbol?.Symbol == null) continue;

                var sym = comp.SelectedSymbol.Symbol;
                ActivateSymbol(sym);

                double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
                XYZ offZ = new XYZ(0, 0, offFeetZ);
                XYZ ptA = pSegStart + offZ;
                XYZ ptB = pSegEnd + offZ;

                FamilyInstance? inst = null;
                if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(sym))
                {
                    inst = CreateAdaptiveInstanceSafe(doc, sym, new[] { ptA, ptB });
                }
                else
                {
                    XYZ ptMid = (ptA + ptB) * 0.5;
                    inst = CreateInstanceSafe(doc, ptMid, sym);
                    ElementTransformUtils.RotateElement(doc, inst.Id, Line.CreateBound(ptMid, ptMid + XYZ.BasisZ), rotAngle);
                }

                if (inst == null) continue;

                // Gán chiều dài phân đoạn cho cấu kiện đệm / lót
                foreach (var pName in possibleLenParams)
                {
                    Parameter p = inst.LookupParameter(pName);
                    if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                    {
                        p.Set(lenSegFeet);
                        break;
                    }
                }

                string tenCauKien = $"{comp.CategoryType}_DOAN_{segIndex}";
                BimParameterService.SetElementBimProperties(inst, bimConfig, tenCauKien, comp.CategoryType ?? "", null, null, null, null, null);
                BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, rowData, comp.CategoryType ?? "");
                BimParameterService.ApplyCustomBimParameters(inst, customBimParams, rowData, "Thân cống");
                TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType ?? "");
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
                if (!comp.IsActive || comp.SelectedSymbol?.Symbol == null) continue;

                var sym = comp.SelectedSymbol.Symbol;
                ActivateSymbol(sym);

                double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
                XYZ ptComp = ptPlace + new XYZ(0, 0, offFeetZ);

                FamilyInstance inst = CreateInstanceSafe(doc, ptComp, sym);
                ElementTransformUtils.RotateElement(doc, inst.Id, Line.CreateBound(ptComp, ptComp + XYZ.BasisZ), rotAngle);

                string tenCK = $"{comp.CategoryType}_{manholeIndex}";
                BimParameterService.SetElementBimProperties(inst, bimConfig, tenCK, $"HỐ GA {manholeIndex}", null, null, ptComp.X, ptComp.Y, ptComp.Z);
                BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, data, "Hố ga");
                BimParameterService.ApplyCustomBimParameters(inst, customBimParams, data, "Hố ga");
                TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType ?? "");
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
            IEnumerable<CulvertMaterialItem>? materialSettings = null)
        {
            if (L <= 0.001) return startDotIdx - 1;

            int dotIdx = startDotIdx;

            if (mode == CulvertArrayMode.CenterOut)
            {
                int n = (int)Math.Floor(L / lStd);
                double lBien = (L - (n * lStd)) / 2.0;

                double curDist = 0.0;

                // Đốt biên 1
                if (lBien > 0.001)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * curDist;
                    XYZ ptB = ptA + u * lBien;
                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, lBien, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, zDauM, null, materialSettings);
                    curDist += lBien + jointGap;
                }

                // Các đốt chuẩn ở giữa
                for (int i = 0; i < n; i++)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * curDist;
                    XYZ ptB = ptA + u * lStd;
                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, lStd, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, null, null, materialSettings);
                    curDist += lStd + jointGap;
                }

                // Đốt biên 2
                if (lBien > 0.001)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * curDist;
                    XYZ ptB = ptA + u * lBien;
                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, lBien, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, null, zCuoiM, materialSettings);
                }
            }
            else // OneWay
            {
                int n = (int)Math.Floor(L / lStd);
                double lDu = L - (n * lStd);

                double curDist = 0.0;

                for (int i = 0; i < n; i++)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * curDist;
                    XYZ ptB = ptA + u * lStd;
                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, lStd, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, (i == 0) ? zDauM : null, null, materialSettings);
                    curDist += lStd + jointGap;
                }

                if (lDu > 0.001)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    XYZ ptA = pStart + u * curDist;
                    XYZ ptB = ptA + u * lDu;
                    PlaceBarrelComponents(doc, barrelComponents, ptA, ptB, lDu, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, (n == 0) ? zDauM : null, zCuoiM, materialSettings);
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
            IEnumerable<CulvertMaterialItem>? materialSettings = null)
        {
            if (barrelComponents == null) return;

            string[] possibleLenParams = new[] { "L", "Length", "ChieuDai", "L_dot_chuan", "L_dot_bu", "Chiều dài", "L_DOT" };

            foreach (var comp in barrelComponents)
            {
                if (!comp.IsActive || comp.SelectedSymbol?.Symbol == null) continue;

                var sym = comp.SelectedSymbol.Symbol;
                ActivateSymbol(sym);

                double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
                XYZ offZ = new XYZ(0, 0, offFeetZ);
                XYZ ptA = ptStart + offZ;
                XYZ ptB = ptEnd + offZ;

                FamilyInstance? inst = null;
                if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(sym))
                {
                    inst = CreateAdaptiveInstanceSafe(doc, sym, new[] { ptA, ptB });
                }
                else
                {
                    XYZ ptMid = (ptA + ptB) * 0.5;
                    inst = CreateInstanceSafe(doc, ptMid, sym);
                    ElementTransformUtils.RotateElement(doc, inst.Id, Line.CreateBound(ptMid, ptMid + XYZ.BasisZ), angle);
                }

                if (inst == null) continue;

                foreach (var pName in possibleLenParams)
                {
                    Parameter p = inst.LookupParameter(pName);
                    if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                    {
                        p.Set(lenFeet);
                        break;
                    }
                }

                string nameSub = $"{tenCauKien}_{comp.CategoryType}";
                BimParameterService.SetElementBimProperties(inst, bimConfig, nameSub, comp.CategoryType, zDau, zCuoi, null, null, null);
                BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, rowData, comp.CategoryType);
                BimParameterService.ApplyCustomBimParameters(inst, customBimParams, rowData, "Thân cống");
                TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType);
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
}
