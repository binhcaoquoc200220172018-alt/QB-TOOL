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
        /// Xây dựng toàn bộ các cống ngang theo danh sách cấu hình, bọc trong TransactionGroup
        /// </summary>
        public static (int SuccessCount, int ErrorCount, List<string> Logs) BuildAllCulverts(
            Document doc,
            IList<CulvertRowData> culvertList,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            FamilySymbol? symDotChuan,
            FamilySymbol? symDotBu,
            FamilySymbol? symSanCongTL,
            FamilySymbol? symSanCongHL,
            FamilySymbol? symHopNoi,
            FamilySymbol? symBeTongLot,
            double lStdM,
            double lMinM,
            double bBoxM,
            double defaultKhoangCachTim,
            CulvertArrayMode arrayMode,
            bool useSurveyPoint = true,
            IEnumerable<CulvertMaterialItem>? materialSettings = null,
            IEnumerable<FamilySymbol>? allAvailableSymbols = null,
            FamilySymbol? symBTL_San = null,
            FamilySymbol? symBTL_HN = null,
            double offsetZ_BTL_DotM = -0.10,
            double offsetZ_BTL_SanM = -0.10,
            double offsetZ_BTL_HNM = -0.30)
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

                foreach (var row in culvertList)
                {
                    if (!row.IsSelected) continue;

                    using (var subT = new Transaction(doc, $"Rải cống STT {row.STT} ({row.LyTrinh})"))
                    {
                        try
                        {
                            subT.Start();

                            // Tự động nhận diện Family tương ứng theo Loại cống & Khẩu độ của từng hàng trong Excel (Smart Auto-Mapping)
                            FamilySymbol? rowDotChuan = ResolveFamilyForCulvert(allAvailableSymbols, row.LoaiCong, row.KhauDo, "Đốt") ?? symDotChuan;
                            FamilySymbol? rowDotBu = ResolveFamilyForCulvert(allAvailableSymbols, row.LoaiCong, row.KhauDo, "Đốt") ?? symDotBu ?? rowDotChuan;
                            FamilySymbol? rowSanTL = ResolveFamilyForCulvert(allAvailableSymbols, row.LoaiCong, row.KhauDo, "Sân") ?? symSanCongTL;
                            FamilySymbol? rowSanHL = ResolveFamilyForCulvert(allAvailableSymbols, row.LoaiCong, row.KhauDo, "Sân") ?? symSanCongHL ?? rowSanTL;
                            FamilySymbol? rowHopNoi = ResolveFamilyForCulvert(allAvailableSymbols, row.LoaiCong, row.KhauDo, "Hộp nối") ?? symHopNoi;
                            FamilySymbol? rowBeTongLot = ResolveFamilyForCulvert(allAvailableSymbols, row.LoaiCong, row.KhauDo, "BTL") ?? symBeTongLot;

                            ActivateSymbol(rowDotChuan);
                            ActivateSymbol(rowDotBu);
                            ActivateSymbol(rowSanTL);
                            ActivateSymbol(rowSanHL);
                            ActivateSymbol(rowHopNoi);
                            ActivateSymbol(rowBeTongLot);
                            ActivateSymbol(symBTL_San);
                            ActivateSymbol(symBTL_HN);

                            BuildSingleCulvert(
                                doc,
                                row,
                                bimConfig,
                                customBimParams,
                                familyParameterMappings,
                                rowDotChuan,
                                rowDotBu,
                                rowSanTL,
                                rowSanHL,
                                rowHopNoi,
                                rowBeTongLot,
                                lStdM,
                                lMinM,
                                bBoxM,
                                defaultKhoangCachTim,
                                arrayMode,
                                useSurveyPoint,
                                materialSettings,
                                symBTL_San,
                                symBTL_HN,
                                offsetZ_BTL_DotM,
                                offsetZ_BTL_SanM,
                                offsetZ_BTL_HNM);

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
                else
                {
                    matchKhauDo = true;
                }

                if (matchKhauDo)
                {
                    return sym;
                }
            }

            return null;
        }

        /// <summary>
        /// Xây dựng 1 cụm cống ngang (hỗ trợ cống đơn, cống hộp đôi đúc liền, và cống tròn đôi 2 nhánh lệch tim)
        /// </summary>
        public static void BuildSingleCulvert(
            Document doc,
            CulvertRowData data,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            FamilySymbol? symDotChuan,
            FamilySymbol? symDotBu,
            FamilySymbol? symSanCongTL,
            FamilySymbol? symSanCongHL,
            FamilySymbol? symHopNoi,
            FamilySymbol? symBeTongLot,
            double lStdM,
            double lMinM,
            double bBoxM,
            double defaultKhoangCachTim,
            CulvertArrayMode arrayMode,
            bool useSurveyPoint,
            IEnumerable<CulvertMaterialItem>? materialSettings = null,
            FamilySymbol? symBTL_San = null,
            FamilySymbol? symBTL_HN = null,
            double offsetZ_BTL_DotM = -0.10,
            double offsetZ_BTL_SanM = -0.10,
            double offsetZ_BTL_HNM = -0.30)
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
            double lMinFeet = UnitUtils.ConvertToInternalUnits(lMinM, UnitTypeId.Meters);
            double lNgamFeet = UnitUtils.ConvertToInternalUnits(data.L_Ngam_San > 0 ? data.L_Ngam_San : 0.30, UnitTypeId.Meters);
            double bBoxFeet = UnitUtils.ConvertToInternalUnits(bBoxM > 0 ? bBoxM : 1.50, UnitTypeId.Meters);

            // 2. Đặt Sân cống Thượng lưu & Hạ lưu
            if (symSanCongTL != null)
            {
                FamilyInstance instSan1 = doc.Create.NewFamilyInstance(p1, symSanCongTL, StructuralType.NonStructural);
                ElementTransformUtils.RotateElement(doc, instSan1.Id, Line.CreateBound(p1, p1 + XYZ.BasisZ), rotAngle);
                BimParameterService.SetElementBimProperties(instSan1, bimConfig, bimConfig.MauTenSanCongTL, "SÂN CỐNG THƯỢNG LƯU", null, null, data.X1, data.Y1, data.Z1);
                BimParameterService.ApplyFamilyMappedParameters(instSan1, familyParameterMappings, data, "Sân cống");
                BimParameterService.ApplyCustomBimParameters(instSan1, customBimParams, data, "Sân cống");
                TryApplyMaterial(doc, instSan1, materialSettings, "Sân cống thượng lưu");

                if (symBTL_San != null)
                {
                    double offFeet = UnitUtils.ConvertToInternalUnits(offsetZ_BTL_SanM, UnitTypeId.Meters);
                    XYZ pBTL1 = p1 + new XYZ(0, 0, offFeet);
                    FamilyInstance instBTL_San1 = doc.Create.NewFamilyInstance(pBTL1, symBTL_San, StructuralType.NonStructural);
                    ElementTransformUtils.RotateElement(doc, instBTL_San1.Id, Line.CreateBound(pBTL1, pBTL1 + XYZ.BasisZ), rotAngle);
                    BimParameterService.SetElementBimProperties(instBTL_San1, bimConfig, "BTL.SAN.TL", "BTL SÂN CỐNG THƯỢNG LƯU", null, null, data.X1, data.Y1, data.Z1 + offsetZ_BTL_SanM);
                    TryApplyMaterial(doc, instBTL_San1, materialSettings, "BTL - Sân cống");
                }
            }

            if (symSanCongHL != null || symSanCongTL != null)
            {
                var symHL = symSanCongHL ?? symSanCongTL!;
                FamilyInstance instSan2 = doc.Create.NewFamilyInstance(p2, symHL, StructuralType.NonStructural);
                ElementTransformUtils.RotateElement(doc, instSan2.Id, Line.CreateBound(p2, p2 + XYZ.BasisZ), rotAngle + Math.PI);
                BimParameterService.SetElementBimProperties(instSan2, bimConfig, bimConfig.MauTenSanCongHL, "SÂN CỐNG HẠ LƯU", null, null, data.X2, data.Y2, data.Z2);
                BimParameterService.ApplyFamilyMappedParameters(instSan2, familyParameterMappings, data, "Sân cống");
                BimParameterService.ApplyCustomBimParameters(instSan2, customBimParams, data, "Sân cống");
                TryApplyMaterial(doc, instSan2, materialSettings, "Sân cống hạ lưu");

                if (symBTL_San != null)
                {
                    double offFeet = UnitUtils.ConvertToInternalUnits(offsetZ_BTL_SanM, UnitTypeId.Meters);
                    XYZ pBTL2 = p2 + new XYZ(0, 0, offFeet);
                    FamilyInstance instBTL_San2 = doc.Create.NewFamilyInstance(pBTL2, symBTL_San, StructuralType.NonStructural);
                    ElementTransformUtils.RotateElement(doc, instBTL_San2.Id, Line.CreateBound(pBTL2, pBTL2 + XYZ.BasisZ), rotAngle + Math.PI);
                    BimParameterService.SetElementBimProperties(instBTL_San2, bimConfig, "BTL.SAN.HL", "BTL SÂN CỐNG HẠ LƯU", null, null, data.X2, data.Y2, data.Z2 + offsetZ_BTL_SanM);
                    TryApplyMaterial(doc, instBTL_San2, materialSettings, "BTL - Sân cống");
                }
            }

            if (symDotChuan == null) return;

            // 3. Phân biệt Cống tròn đôi (2 ống rời lệch tim) và Cống đơn/hộp đôi (1 tim)
            string lcNorm = (data.LoaiCong ?? "").ToUpperInvariant();
            bool isTronNorm = lcNorm.Contains("TRON") || lcNorm.Contains("TRÒN") || lcNorm.Contains("CT");
            bool isCongTronDoi = (data.SoCua >= 2 && isTronNorm) || (isTronNorm && (lcNorm.Contains("ĐÔI") || lcNorm.Contains("DOI")));

            if (isCongTronDoi)
            {
                // CỐNG TRÒN ĐÔI: Tách thành 2 trục song song cách nhau d_tim
                XYZ uPerp = new XYZ(-u.Y, u.X, 0); // Vector pháp tuyến vuông góc trục cống trên mặt bằng
                double dTimM = data.KhoangCachTim > 0.1 ? data.KhoangCachTim : (defaultKhoangCachTim > 0.1 ? defaultKhoangCachTim : 2.0);
                double dHalfFeet = UnitUtils.ConvertToInternalUnits(dTimM / 2.0, UnitTypeId.Meters);

                // Nhánh Trái
                XYZ p1Left = p1 - uPerp * dHalfFeet;
                XYZ p2Left = p2 - uPerp * dHalfFeet;
                BuildBranch(doc, data, bimConfig, customBimParams, familyParameterMappings, symDotChuan, symDotBu, symHopNoi, symBeTongLot,
                    p1Left, p2Left, u, rotAngle, totalLengthFeet, lStdFeet, lMinFeet, lNgamFeet, bBoxFeet, arrayMode, "T", materialSettings,
                    symBTL_HN, offsetZ_BTL_DotM, offsetZ_BTL_HNM);

                // Nhánh Phải
                XYZ p1Right = p1 + uPerp * dHalfFeet;
                XYZ p2Right = p2 + uPerp * dHalfFeet;
                BuildBranch(doc, data, bimConfig, customBimParams, familyParameterMappings, symDotChuan, symDotBu, symHopNoi, symBeTongLot,
                    p1Right, p2Right, u, rotAngle, totalLengthFeet, lStdFeet, lMinFeet, lNgamFeet, bBoxFeet, arrayMode, "P", materialSettings,
                    symBTL_HN, offsetZ_BTL_DotM, offsetZ_BTL_HNM);
            }
            else
            {
                // CỐNG ĐƠN HOẶC CỐNG HỘP ĐÔI (Đúc liền 2 ngăn): Rải theo 1 tim trung tâm
                BuildBranch(doc, data, bimConfig, customBimParams, familyParameterMappings, symDotChuan, symDotBu, symHopNoi, symBeTongLot,
                    p1, p2, u, rotAngle, totalLengthFeet, lStdFeet, lMinFeet, lNgamFeet, bBoxFeet, arrayMode, "", materialSettings,
                    symBTL_HN, offsetZ_BTL_DotM, offsetZ_BTL_HNM);
            }
        }

        private static void BuildBranch(
            Document doc,
            CulvertRowData data,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            FamilySymbol symDotChuan,
            FamilySymbol? symDotBu,
            FamilySymbol? symHopNoi,
            FamilySymbol? symBeTongLot,
            XYZ p1,
            XYZ p2,
            XYZ u,
            double rotAngle,
            double totalLengthFeet,
            double lStdFeet,
            double lMinFeet,
            double lNgamFeet,
            double bBoxFeet,
            CulvertArrayMode arrayMode,
            string branchSuffix,
            IEnumerable<CulvertMaterialItem>? materialSettings = null,
            FamilySymbol? symBTL_HN = null,
            double offsetZ_BTL_DotM = -0.10,
            double offsetZ_BTL_HNM = -0.30)
        {
            if (data.SoHopNoi == 0)
            {
                // TH1: Không hộp nối, rải giữa 2 sân cống
                XYZ startPt = p1 + u * lNgamFeet;
                double segLen = totalLengthFeet - (2 * lNgamFeet);
                if (segLen > 0)
                {
                    LayAdaptiveSegments(doc, symDotChuan, symDotBu, startPt, segLen, lStdFeet, lMinFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, data.Z1, data.Z2, 1, branchSuffix, materialSettings, symBeTongLot, offsetZ_BTL_DotM);
                }
            }
            else if (data.SoHopNoi == 1)
            {
                // TH2.1: Có 1 hộp nối
                double distHN1Feet = UnitUtils.ConvertToInternalUnits(data.KC_HN1, UnitTypeId.Meters);
                XYZ pHN1 = p1 + u * distHN1Feet;

                if (symHopNoi != null && string.IsNullOrEmpty(branchSuffix))
                {
                    FamilyInstance instHN1 = doc.Create.NewFamilyInstance(pHN1, symHopNoi, StructuralType.NonStructural);
                    ElementTransformUtils.RotateElement(doc, instHN1.Id, Line.CreateBound(pHN1, pHN1 + XYZ.BasisZ), rotAngle);
                    BimParameterService.SetElementBimProperties(instHN1, bimConfig, "HT.1", "HỐ THU 1", null, null, data.X1 + data.KC_HN1 * Math.Cos(rotAngle), data.Y1 + data.KC_HN1 * Math.Sin(rotAngle), data.Z1);
                    BimParameterService.ApplyFamilyMappedParameters(instHN1, familyParameterMappings, data, "Hộp nối");
                    BimParameterService.ApplyCustomBimParameters(instHN1, customBimParams, data, "Hộp nối");
                    TryApplyMaterial(doc, instHN1, materialSettings, "Hộp nối / Hố thu");

                    // BTL Hố thu: Chỉ đặt nếu symBTL_HN được bật
                    if (symBTL_HN != null)
                    {
                        double offFeet = UnitUtils.ConvertToInternalUnits(offsetZ_BTL_HNM, UnitTypeId.Meters);
                        XYZ pBtlHN = pHN1 + new XYZ(0, 0, offFeet);
                        FamilyInstance instBTL = doc.Create.NewFamilyInstance(pBtlHN, symBTL_HN, StructuralType.NonStructural);
                        ElementTransformUtils.RotateElement(doc, instBTL.Id, Line.CreateBound(pBtlHN, pBtlHN + XYZ.BasisZ), rotAngle);
                        BimParameterService.SetElementBimProperties(instBTL, bimConfig, "BTL.HT.1", "BÊ TÔNG LÓT HỐ THU 1");
                        TryApplyMaterial(doc, instBTL, materialSettings, "BTL - Hố thu");
                    }
                }

                // Đoạn 1: Sân 1 -> Hộp 1
                XYZ pStart1 = p1 + u * lNgamFeet;
                double len1 = distHN1Feet - lNgamFeet - (bBoxFeet / 2.0);
                int dotCount1 = LayAdaptiveSegments(doc, symDotChuan, symDotBu, pStart1, len1, lStdFeet, lMinFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, data.Z1, null, 1, branchSuffix, materialSettings, symBeTongLot, offsetZ_BTL_DotM);

                // Đoạn 2: Hộp 1 -> Sân 2
                XYZ pStart2 = pHN1 + u * (bBoxFeet / 2.0);
                double len2 = (totalLengthFeet - distHN1Feet) - lNgamFeet - (bBoxFeet / 2.0);
                LayAdaptiveSegments(doc, symDotChuan, symDotBu, pStart2, len2, lStdFeet, lMinFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, null, data.Z2, dotCount1 + 1, branchSuffix, materialSettings, symBeTongLot, offsetZ_BTL_DotM);
            }
            else // data.SoHopNoi >= 2
            {
                // TH2.2: Có 2 hộp nối
                double distHN1Feet = UnitUtils.ConvertToInternalUnits(data.KC_HN1, UnitTypeId.Meters);
                double distHN2Feet = UnitUtils.ConvertToInternalUnits(data.KC_HN2, UnitTypeId.Meters);

                XYZ pHN1 = p1 + u * distHN1Feet;
                XYZ pHN2 = p2 - u * distHN2Feet;

                if (symHopNoi != null && string.IsNullOrEmpty(branchSuffix))
                {
                    // Đặt Hộp nối 1
                    FamilyInstance instHN1 = doc.Create.NewFamilyInstance(pHN1, symHopNoi, StructuralType.NonStructural);
                    ElementTransformUtils.RotateElement(doc, instHN1.Id, Line.CreateBound(pHN1, pHN1 + XYZ.BasisZ), rotAngle);
                    BimParameterService.SetElementBimProperties(instHN1, bimConfig, "HT.1", "HỐ THU 1", null, null, data.X1 + data.KC_HN1 * Math.Cos(rotAngle), data.Y1 + data.KC_HN1 * Math.Sin(rotAngle), data.Z1);
                    BimParameterService.ApplyFamilyMappedParameters(instHN1, familyParameterMappings, data, "Hộp nối");
                    BimParameterService.ApplyCustomBimParameters(instHN1, customBimParams, data, "Hộp nối");
                    TryApplyMaterial(doc, instHN1, materialSettings, "Hộp nối / Hố thu");

                    // Đặt Hộp nối 2
                    FamilyInstance instHN2 = doc.Create.NewFamilyInstance(pHN2, symHopNoi, StructuralType.NonStructural);
                    ElementTransformUtils.RotateElement(doc, instHN2.Id, Line.CreateBound(pHN2, pHN2 + XYZ.BasisZ), rotAngle);
                    BimParameterService.SetElementBimProperties(instHN2, bimConfig, "HT.2", "HỐ THU 2", null, null, data.X2 - data.KC_HN2 * Math.Cos(rotAngle), data.Y2 - data.KC_HN2 * Math.Sin(rotAngle), data.Z2);
                    BimParameterService.ApplyFamilyMappedParameters(instHN2, familyParameterMappings, data, "Hộp nối");
                    BimParameterService.ApplyCustomBimParameters(instHN2, customBimParams, data, "Hộp nối");
                    TryApplyMaterial(doc, instHN2, materialSettings, "Hộp nối / Hố thu");

                    // BTL Hố thu 1 & 2: Chỉ đặt nếu symBTL_HN được bật
                    if (symBTL_HN != null)
                    {
                        double offFeet = UnitUtils.ConvertToInternalUnits(offsetZ_BTL_HNM, UnitTypeId.Meters);

                        XYZ pBtlHN1 = pHN1 + new XYZ(0, 0, offFeet);
                        FamilyInstance instBTL1 = doc.Create.NewFamilyInstance(pBtlHN1, symBTL_HN, StructuralType.NonStructural);
                        ElementTransformUtils.RotateElement(doc, instBTL1.Id, Line.CreateBound(pBtlHN1, pBtlHN1 + XYZ.BasisZ), rotAngle);
                        BimParameterService.SetElementBimProperties(instBTL1, bimConfig, "BTL.HT.1", "BÊ TÔNG LÓT HỐ THU 1");
                        TryApplyMaterial(doc, instBTL1, materialSettings, "BTL - Hố thu");

                        XYZ pBtlHN2 = pHN2 + new XYZ(0, 0, offFeet);
                        FamilyInstance instBTL2 = doc.Create.NewFamilyInstance(pBtlHN2, symBTL_HN, StructuralType.NonStructural);
                        ElementTransformUtils.RotateElement(doc, instBTL2.Id, Line.CreateBound(pBtlHN2, pBtlHN2 + XYZ.BasisZ), rotAngle);
                        BimParameterService.SetElementBimProperties(instBTL2, bimConfig, "BTL.HT.2", "BÊ TÔNG LÓT HỐ THU 2");
                        TryApplyMaterial(doc, instBTL2, materialSettings, "BTL - Hố thu");
                    }
                }

                // Đoạn 1: Sân 1 -> Hộp 1
                XYZ pStart1 = p1 + u * lNgamFeet;
                double len1 = distHN1Feet - lNgamFeet - (bBoxFeet / 2.0);
                int dotCount1 = LayAdaptiveSegments(doc, symDotChuan, symDotBu, pStart1, len1, lStdFeet, lMinFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, data.Z1, null, 1, branchSuffix, materialSettings, symBeTongLot, offsetZ_BTL_DotM);

                // Đoạn 2: Hộp 1 -> Hộp 2
                XYZ pStart2 = pHN1 + u * (bBoxFeet / 2.0);
                double len2 = (pHN2 - pHN1).GetLength() - bBoxFeet;
                int dotCount2 = LayAdaptiveSegments(doc, symDotChuan, symDotBu, pStart2, len2, lStdFeet, lMinFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, null, null, dotCount1 + 1, branchSuffix, materialSettings, symBeTongLot, offsetZ_BTL_DotM);

                // Đoạn 3: Hộp 2 -> Sân 2
                XYZ pStart3 = pHN2 + u * (bBoxFeet / 2.0);
                double len3 = distHN2Feet - lNgamFeet - (bBoxFeet / 2.0);
                LayAdaptiveSegments(doc, symDotChuan, symDotBu, pStart3, len3, lStdFeet, lMinFeet, u, rotAngle, arrayMode, bimConfig, customBimParams, familyParameterMappings, data, null, data.Z2, dotCount2 + 1, branchSuffix, materialSettings, symBeTongLot, offsetZ_BTL_DotM);
            }
        }

        private static int LayAdaptiveSegments(
            Document doc,
            FamilySymbol symStd,
            FamilySymbol? symBu,
            XYZ pStart,
            double L,
            double lStd,
            double lMin,
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
            FamilySymbol? symBTL_Dot = null,
            double offsetZ_BTL_DotM = -0.10)
        {
            if (L <= 0.001) return startDotIdx - 1;

            int dotIdx = startDotIdx;

            if (mode == CulvertArrayMode.CenterOut)
            {
                int n = (int)Math.Floor(L / lStd);
                double lBien = (L - (n * lStd)) / 2.0;

                if (lBien < lMin && n > 0)
                {
                    n = (n >= 2) ? n - 2 : 0;
                    lBien = (L - (n * lStd)) / 2.0;
                }

                double curDist = 0.0;

                // Đốt biên 1
                if (lBien > 0.001)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    PlaceAndTag(doc, symBu ?? symStd, pStart + u * curDist, lBien, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, zDauM, null, materialSettings, symBTL_Dot, offsetZ_BTL_DotM);
                    curDist += lBien;
                }

                // Các đốt chuẩn ở giữa
                for (int i = 0; i < n; i++)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    PlaceAndTag(doc, symStd, pStart + u * curDist, lStd, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, null, null, materialSettings, symBTL_Dot, offsetZ_BTL_DotM);
                    curDist += lStd;
                }

                // Đốt biên 2
                if (lBien > 0.001)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    PlaceAndTag(doc, symBu ?? symStd, pStart + u * curDist, lBien, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, null, zCuoiM, materialSettings, symBTL_Dot, offsetZ_BTL_DotM);
                }
            }
            else // OneWay
            {
                int n = (int)Math.Floor(L / lStd);
                double lDu = L - (n * lStd);

                if (lDu < lMin && n > 0)
                {
                    n = n - 1;
                    lDu = L - (n * lStd);
                }

                double curDist = 0.0;

                for (int i = 0; i < n; i++)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    PlaceAndTag(doc, symStd, pStart + u * curDist, lStd, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, (i == 0) ? zDauM : null, null, materialSettings, symBTL_Dot, offsetZ_BTL_DotM);
                    curDist += lStd;
                }

                if (lDu > 0.001)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    PlaceAndTag(doc, symBu ?? symStd, pStart + u * curDist, lDu, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, (n == 0) ? zDauM : null, zCuoiM, materialSettings, symBTL_Dot, offsetZ_BTL_DotM);
                }
            }

            return dotIdx - 1;
        }

        private static string FormatDotName(string pattern, int index, string branchSuffix)
        {
            string name = pattern.Replace("{STT}", index.ToString());
            if (!string.IsNullOrEmpty(branchSuffix))
            {
                name += $".{branchSuffix}";
            }
            return name;
        }

        private static void PlaceAndTag(
            Document doc,
            FamilySymbol sym,
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
            IEnumerable<CulvertMaterialItem>? materialSettings = null,
            FamilySymbol? symBTL_Dot = null,
            double offsetZ_BTL_DotM = -0.10)
        {
            FamilyInstance inst = doc.Create.NewFamilyInstance(pt, sym, StructuralType.NonStructural);
            ElementTransformUtils.RotateElement(doc, inst.Id, Line.CreateBound(pt, pt + XYZ.BasisZ), angle);

            // Gán chiều dài đốt
            string[] possibleLenParams = new[] { "L", "Length", "ChieuDai", "L_dot_chuan", "L_dot_bu", "Chiều dài" };
            foreach (var pName in possibleLenParams)
            {
                Parameter p = inst.LookupParameter(pName);
                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                {
                    p.Set(lenFeet);
                    break;
                }
            }

            // Gán các tham số Dimensions tùy chỉnh từ Tab 02
            BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, rowData, "Đốt cống");

            // Gán thông tin BIM cơ bản
            BimParameterService.SetElementBimProperties(inst, bimConfig, tenCauKien, bimConfig.MoTa, zDau, zCuoi, null, null, null);

            // Gán danh sách tham số BIM tùy biến động từ Tab 04
            BimParameterService.ApplyCustomBimParameters(inst, customBimParams, rowData, "Thân cống");

            // Gán vật liệu kỹ thuật từ Tab 03
            TryApplyMaterial(doc, inst, materialSettings, "Đốt cống");

            // Đặt BTL Đốt cống (nếu có và IsActive)
            if (symBTL_Dot != null && symBTL_Dot.Id != sym.Id)
            {
                double offFeet = UnitUtils.ConvertToInternalUnits(offsetZ_BTL_DotM, UnitTypeId.Meters);
                XYZ ptBTL = pt + new XYZ(0, 0, offFeet);
                FamilyInstance instBTL = doc.Create.NewFamilyInstance(ptBTL, symBTL_Dot, StructuralType.NonStructural);
                ElementTransformUtils.RotateElement(doc, instBTL.Id, Line.CreateBound(ptBTL, ptBTL + XYZ.BasisZ), angle);

                foreach (var pName in possibleLenParams)
                {
                    Parameter p = instBTL.LookupParameter(pName);
                    if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                    {
                        p.Set(lenFeet);
                        break;
                    }
                }

                BimParameterService.SetElementBimProperties(instBTL, bimConfig, $"BTL.{tenCauKien}", "BÊ TÔNG LÓT THÂN CỐNG", zDau, zCuoi, null, null, null);
                TryApplyMaterial(doc, instBTL, materialSettings, "BTL - Đốt cống");
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
