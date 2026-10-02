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

            // Kích hoạt tất cả Family Symbol được dùng
            foreach (var c in barrelComponents.Concat(outletComponents).Concat(manholeComponents))
            {
                if (c.IsActive && c.SelectedSymbol?.Symbol != null)
                {
                    ActivateSymbol(c.SelectedSymbol.Symbol);
                }
            }

            using (var tg = new TransactionGroup(doc, "INFRA BIM - Tự Động Rải Cống Ngang"))
            {
                tg.Start();

                foreach (var row in culvertList)
                {
                    if (!row.IsSelected) continue;

                    using (var subT = new Transaction(doc, $"Rải cống STT {row.STT} ({row.LyTrinh})"))
                    {
                        try
                        {
                            subT.Start();

                            BuildSingleCulvertV2(
                                doc,
                                row,
                                bimConfig,
                                customBimParams,
                                familyParameterMappings,
                                barrelComponents,
                                outletComponents,
                                manholeComponents,
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


        private static FamilySymbol? ResolveFamilyForCulvert(
            IEnumerable<FamilySymbol>? symbols,
            string loaiCong,
            string khauDo,
            string partHint)
        {
            if (symbols == null) return null;

            string lcUpper = (loaiCong ?? "").Trim().ToUpperInvariant();
            string kdUpper = (khauDo ?? "").Trim().ToUpperInvariant();
            string hintUpper = (partHint ?? "").Trim().ToUpperInvariant();

            bool isTron = lcUpper.Contains("TRON") || lcUpper.Contains("TRÒN") || lcUpper.Contains("PIPE") || lcUpper.Contains("CT");
            bool isHop = lcUpper.Contains("HOP") || lcUpper.Contains("HỘP") || lcUpper.Contains("BOX") || lcUpper.Contains("CH");

            foreach (var sym in symbols)
            {
                string fam = sym.FamilyName.ToUpperInvariant();
                string name = sym.Name.ToUpperInvariant();
                string full = $"{fam} {name}";

                // 1. Phù hợp phân loại cống tròn / cống hộp
                bool matchType = true;
                if (isTron)
                {
                    matchType = full.Contains("TRON") || full.Contains("TRÒN") || full.Contains("_CT_") || full.StartsWith("CT_") || full.Contains("TNN_CT") || full.Contains("TNM_CT");
                }
                else if (isHop)
                {
                    matchType = full.Contains("HOP") || full.Contains("HỘP") || full.Contains("_CH_") || full.StartsWith("CH_") || full.Contains("TNN_CH") || full.Contains("BOX");
                }

                if (!matchType) continue;

                // 2. Phù hợp loại cấu kiện (partHint)
                bool matchPart = true;
                if (hintUpper.Contains("BTL") || hintUpper.Contains("LÓT") || hintUpper.Contains("LOT"))
                {
                    matchPart = full.Contains("BE TONG LOT") || full.Contains("BÊ TÔNG LÓT") || full.Contains("BTL") || full.Contains("LOT");
                }
                else if (hintUpper.Contains("ĐỐT") || hintUpper.Contains("DOT") || hintUpper.Contains("THÂN") || hintUpper.Contains("THAN"))
                {
                    matchPart = (full.Contains("THAN CONG") || full.Contains("THÂN CỐNG") || full.Contains("DOT CONG") || full.Contains("ĐỐT CỐNG") || full.Contains("CONG HOP"))
                                && !full.Contains("LOT") && !full.Contains("BTL") && !full.Contains("CUA XA") && !full.Contains("SAN GIA CO");
                }
                else if (hintUpper.Contains("SÂN") || hintUpper.Contains("SAN") || hintUpper.Contains("CỬA") || hintUpper.Contains("CUA"))
                {
                    matchPart = full.Contains("CUA XA") || full.Contains("CỬA XẢ") || full.Contains("SAN CONG") || full.Contains("SÂN CỐNG") || full.Contains("SAN GIA CO");
                }
                else if (hintUpper.Contains("HỘP") || hintUpper.Contains("HOP") || hintUpper.Contains("HỐ") || hintUpper.Contains("HO"))
                {
                    matchPart = full.Contains("HO THU") || full.Contains("HỐ THU") || full.Contains("HOP NOI") || full.Contains("HỘP NỐI") || full.Contains("HO GA") || full.Contains("HỐ GA");
                }

                if (!matchPart) continue;

                // 3. Phù hợp khẩu độ hình học (D800, D1000, D1200, 1.5x1.5...)
                bool matchKhauDo = false;
                if (!string.IsNullOrEmpty(kdUpper))
                {
                    if (full.Contains(kdUpper))
                    {
                        matchKhauDo = true;
                    }
                    else if (kdUpper.StartsWith("D"))
                    {
                        string num = kdUpper.Substring(1); // D1000 -> 1000
                        if (num.Length >= 3 && full.Contains(num)) matchKhauDo = true;
                    }
                    else if (kdUpper.All(char.IsDigit) && kdUpper.Length >= 3)
                    {
                        if (full.Contains($"D{kdUpper}") || full.Contains(kdUpper)) matchKhauDo = true;
                    }
                    else if (kdUpper.Contains("X") || kdUpper.Contains("*"))
                    {
                        string cleanKd = kdUpper.Replace("*", "X").Replace(" ", "");
                        string cleanFull = full.Replace("*", "X").Replace(" ", "");
                        if (cleanFull.Contains(cleanKd)) matchKhauDo = true;
                    }
                }
                if (matchKhauDo)
                {
                    return sym;
                }
            }

            return null;
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
            PlaceOutletAssembly(doc, p1, rotAngle, outletComponents, bimConfig, customBimParams, familyParameterMappings, data, isUpstream: true, materialSettings);
            PlaceOutletAssembly(doc, p2, rotAngle + Math.PI, outletComponents, bimConfig, customBimParams, familyParameterMappings, data, isUpstream: false, materialSettings);

            // 3. Phân biệt Cống tròn đôi và Cống 1 tim
            string lcNorm = (data.LoaiCong ?? "").ToUpperInvariant();
            bool isTronNorm = lcNorm.Contains("TRON") || lcNorm.Contains("TRÒN") || lcNorm.Contains("CT");
            bool isCongTronDoi = (data.SoCua >= 2 && isTronNorm) || (isTronNorm && (lcNorm.Contains("ĐÔI") || lcNorm.Contains("DOI")));

            if (isCongTronDoi)
            {
                // CỐNG TRÒN ĐÔI: Tách thành 2 trục song song cách nhau D_tim
                XYZ uPerp = new XYZ(-u.Y, u.X, 0);
                double dTimM = data.KhoangCachTim > 0.1 ? data.KhoangCachTim : (defaultKhoangCachTim > 0.1 ? defaultKhoangCachTim : 2.0);
                double dHalfFeet = UnitUtils.ConvertToInternalUnits(dTimM / 2.0, UnitTypeId.Meters);

                // Nhánh Trái
                XYZ p1Left = p1 - uPerp * dHalfFeet;
                XYZ p2Left = p2 - uPerp * dHalfFeet;
                BuildBranchV2(doc, data, bimConfig, customBimParams, familyParameterMappings, barrelComponents, manholeComponents,
                    p1Left, p2Left, u, rotAngle, totalLengthFeet, lStdFeet, jointGapFeet, bBoxFeet, arrayMode, "T", materialSettings);

                // Nhánh Phải
                XYZ p1Right = p1 + uPerp * dHalfFeet;
                XYZ p2Right = p2 + uPerp * dHalfFeet;
                BuildBranchV2(doc, data, bimConfig, customBimParams, familyParameterMappings, barrelComponents, manholeComponents,
                    p1Right, p2Right, u, rotAngle, totalLengthFeet, lStdFeet, jointGapFeet, bBoxFeet, arrayMode, "P", materialSettings);
            }
            else
            {
                // 1 Tim trung tâm
                BuildBranchV2(doc, data, bimConfig, customBimParams, familyParameterMappings, barrelComponents, manholeComponents,
                    p1, p2, u, rotAngle, totalLengthFeet, lStdFeet, jointGapFeet, bBoxFeet, arrayMode, "", materialSettings);
            }
        }

        private static void PlaceOutletAssembly(
            Document doc,
            XYZ ptBase,
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

            XYZ uDir = new XYZ(Math.Cos(rotAngle), Math.Sin(rotAngle), 0);

            foreach (var comp in outletComponents)
            {
                if (!comp.IsActive || comp.SelectedSymbol?.Symbol == null) continue;

                var sym = comp.SelectedSymbol.Symbol;
                ActivateSymbol(sym);

                double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
                XYZ ptPlace = ptBase + new XYZ(0, 0, offFeetZ);

                // Nếu là Sân gia cố thì đặt lùi ra phía ngoài thêm 1.5m
                string cat = (comp.CategoryType ?? "").ToUpperInvariant();
                if (cat.Contains("GIA CỐ") || cat.Contains("GIA CO") || cat.Contains("SGC"))
                {
                    double sgcOffsetFeet = UnitUtils.ConvertToInternalUnits(1.5, UnitTypeId.Meters);
                    ptPlace -= uDir * sgcOffsetFeet;
                }

                FamilyInstance inst = doc.Create.NewFamilyInstance(ptPlace, sym, StructuralType.NonStructural);
                ElementTransformUtils.RotateElement(doc, inst.Id, Line.CreateBound(ptPlace, ptPlace + XYZ.BasisZ), rotAngle);

                string suffix = isUpstream ? "TL" : "HL";
                string tenCK = $"{comp.CategoryType}_{suffix}";
                BimParameterService.SetElementBimProperties(inst, bimConfig, tenCK, isUpstream ? "CỬA XẢ THƯỢNG LƯU" : "CỬA XẢ HẠ LƯU", null, null, ptPlace.X, ptPlace.Y, ptPlace.Z);
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
            if (data.SoHopNoi == 0)
            {
                // TH1: Không hộp nối, rải suốt chiều dài cống
                if (totalLengthFeet > 0)
                {
                    LayAdaptiveSegmentsV2(doc, barrelComponents, p1, totalLengthFeet, lStdFeet, jointGapFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, data.Z1, data.Z2, 1, branchSuffix, materialSettings);
                }
            }
            else if (data.SoHopNoi == 1)
            {
                // TH2.1: Có 1 hộp nối
                double distHN1Feet = UnitUtils.ConvertToInternalUnits(data.KC_HN1, UnitTypeId.Meters);
                XYZ pHN1 = p1 + u * distHN1Feet;

                if (string.IsNullOrEmpty(branchSuffix))
                {
                    PlaceManholeAssembly(doc, pHN1, rotAngle, manholeComponents, bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings);
                }

                // Đoạn 1: P1 -> Hộp 1
                double len1 = distHN1Feet - (bBoxFeet / 2.0);
                int dotCount1 = LayAdaptiveSegmentsV2(doc, barrelComponents, p1, len1, lStdFeet, jointGapFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, data.Z1, null, 1, branchSuffix, materialSettings);

                // Đoạn 2: Hộp 1 -> P2
                XYZ pStart2 = pHN1 + u * (bBoxFeet / 2.0);
                double len2 = totalLengthFeet - distHN1Feet - (bBoxFeet / 2.0);
                LayAdaptiveSegmentsV2(doc, barrelComponents, pStart2, len2, lStdFeet, jointGapFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, null, data.Z2, dotCount1 + 1, branchSuffix, materialSettings);
            }
            else // data.SoHopNoi >= 2
            {
                // TH2.2: Có 2 hộp nối
                double distHN1Feet = UnitUtils.ConvertToInternalUnits(data.KC_HN1, UnitTypeId.Meters);
                double distHN2Feet = UnitUtils.ConvertToInternalUnits(data.KC_HN2, UnitTypeId.Meters);

                XYZ pHN1 = p1 + u * distHN1Feet;
                XYZ pHN2 = p2 - u * distHN2Feet;

                if (string.IsNullOrEmpty(branchSuffix))
                {
                    PlaceManholeAssembly(doc, pHN1, rotAngle, manholeComponents, bimConfig, customBimParams, familyParameterMappings, data, 1, materialSettings);
                    PlaceManholeAssembly(doc, pHN2, rotAngle, manholeComponents, bimConfig, customBimParams, familyParameterMappings, data, 2, materialSettings);
                }

                // Đoạn 1: P1 -> Hộp 1
                double len1 = distHN1Feet - (bBoxFeet / 2.0);
                int dotCount1 = LayAdaptiveSegmentsV2(doc, barrelComponents, p1, len1, lStdFeet, jointGapFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, data.Z1, null, 1, branchSuffix, materialSettings);

                // Đoạn 2: Hộp 1 -> Hộp 2
                XYZ pStart2 = pHN1 + u * (bBoxFeet / 2.0);
                double len2 = (pHN2 - pHN1).GetLength() - bBoxFeet;
                int dotCount2 = LayAdaptiveSegmentsV2(doc, barrelComponents, pStart2, len2, lStdFeet, jointGapFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, null, null, dotCount1 + 1, branchSuffix, materialSettings);

                // Đoạn 3: Hộp 2 -> P2
                XYZ pStart3 = pHN2 + u * (bBoxFeet / 2.0);
                double len3 = distHN2Feet - (bBoxFeet / 2.0);
                LayAdaptiveSegmentsV2(doc, barrelComponents, pStart3, len3, lStdFeet, jointGapFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, null, data.Z2, dotCount2 + 1, branchSuffix, materialSettings);
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

                FamilyInstance inst = doc.Create.NewFamilyInstance(ptComp, sym, StructuralType.NonStructural);
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
                    PlaceBarrelComponents(doc, barrelComponents, pStart + u * curDist, lBien, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, zDauM, null, materialSettings);
                    curDist += lBien + jointGap;
                }

                // Các đốt chuẩn ở giữa
                for (int i = 0; i < n; i++)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    PlaceBarrelComponents(doc, barrelComponents, pStart + u * curDist, lStd, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, null, null, materialSettings);
                    curDist += lStd + jointGap;
                }

                // Đốt biên 2
                if (lBien > 0.001)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    PlaceBarrelComponents(doc, barrelComponents, pStart + u * curDist, lBien, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, null, zCuoiM, materialSettings);
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
                    PlaceBarrelComponents(doc, barrelComponents, pStart + u * curDist, lStd, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, (i == 0) ? zDauM : null, null, materialSettings);
                    curDist += lStd + jointGap;
                }

                if (lDu > 0.001)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    PlaceBarrelComponents(doc, barrelComponents, pStart + u * curDist, lDu, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, (n == 0) ? zDauM : null, zCuoiM, materialSettings);
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
            XYZ pt,
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
                XYZ ptComp = pt + new XYZ(0, 0, offFeetZ);

                FamilyInstance inst = doc.Create.NewFamilyInstance(ptComp, sym, StructuralType.NonStructural);
                ElementTransformUtils.RotateElement(doc, inst.Id, Line.CreateBound(ptComp, ptComp + XYZ.BasisZ), angle);

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
