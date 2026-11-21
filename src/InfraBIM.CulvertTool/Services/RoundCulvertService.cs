using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using InfraBIM.CulvertTool.Models;

namespace InfraBIM.CulvertTool.Services
{
    /// <summary>
    /// Dịch vụ chuyên trách rải cống tròn (đơn & đôi), BTL móng và Cut Geometry tự động.
    /// Module độc lập hoàn toàn, không can thiệp vào thuật toán cống hộp và cống kỹ thuật.
    /// </summary>
    public static class RoundCulvertService
    {
        /// <summary>
        /// Phân tích cú pháp chuỗi quy cách đốt cống tròn (ví dụ: "6@2.5, 2@3.0" hoặc "8@2.5")
        /// </summary>
        public static List<double> ParseCustomSegmentPattern(string? pattern)
        {
            var result = new List<double>();
            if (string.IsNullOrWhiteSpace(pattern)) return result;

            var parts = pattern.Split(new[] { ',', ';', '+' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                string p = part.Trim();
                if (string.IsNullOrEmpty(p)) continue;

                if (p.Contains('@') || p.Contains('*'))
                {
                    char sep = p.Contains('@') ? '@' : '*';
                    var subParts = p.Split(sep);
                    if (subParts.Length == 2 &&
                        int.TryParse(subParts[0].Trim(), out int count) &&
                        double.TryParse(subParts[1].Replace("m", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double len))
                    {
                        for (int i = 0; i < count; i++)
                        {
                            result.Add(len);
                        }
                        continue;
                    }
                }

                if (double.TryParse(p.Replace("m", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double singleLen))
                {
                    result.Add(singleLen);
                }
            }
            return result;
        }
        public static bool IsRoundCulvert(CulvertRowData? row)
        {
            if (row == null) return false;
            string type = row.DetermineCulvertType();
            if (type.IndexOf("TRÒN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.IndexOf("TRON", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string lc = (row.LoaiCong ?? string.Empty).ToUpperInvariant();
            if (lc.Contains("TRÒN") || lc.Contains("TRON") || lc == "CT")
                return true;

            string gc = (row.GhiChu ?? string.Empty).ToUpperInvariant();
            if (gc.Contains("CỐNG TRÒN") || gc.Contains("CONG TRON"))
                return true;

            return false;
        }

        public static bool IsDoublePipe(CulvertRowData? row)
        {
            if (row == null) return false;
            if (row.SoCua > 1) return true;
            string type = row.DetermineCulvertType();
            if (type.IndexOf("ĐÔI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.IndexOf("DOI", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string gc = (row.GhiChu ?? string.Empty).ToUpperInvariant();
            if (gc.Contains("TRÒN ĐÔI") || gc.Contains("TRON DOI") || gc.Contains("CỐNG ĐÔI"))
                return true;

            string kd = (row.KhauDo ?? string.Empty).ToUpperInvariant();
            if (kd.StartsWith("2X") || kd.Contains("2*") || kd.Contains("2 X"))
                return true;

            return false;
        }

        /// <summary>
        /// Nhận diện dòng cống tròn đấu nối vào cống hiện trạng cũ
        /// (Khi GhiChu chứa từ khóa "MỐI NỐI", "MOI NOI", "ĐẤU NỐI", "DAU NOI", "HIỆN TRẠNG", "HIEN TRANG")
        /// </summary>
        public static bool IsConnectionToExisting(CulvertRowData? row)
        {
            if (row == null) return false;
            string gc = (row.GhiChu ?? string.Empty).ToUpperInvariant();
            return gc.Contains("MỐI NỐI") || gc.Contains("MOI NOI") ||
                   gc.Contains("ĐẤU NỐI") || gc.Contains("DAU NOI") ||
                   gc.Contains("HIỆN TRẠNG") || gc.Contains("HIEN TRANG");
        }

        /// <summary>
        /// Xác định đầu nào là mối nối:
        /// - Trả về TRUE nếu là P1 (khi GhiChu ghi rõ "MỐI NỐI P1", "MOI NOI P1" hoặc chứa "P1")
        /// - Trả về FALSE nếu là P2 (mặc định cho "Mối nối P2" hoặc khi chỉ ghi "Mối nối")
        /// </summary>
        public static bool IsConnectionAtP1(CulvertRowData? row)
        {
            if (row == null) return false;
            string gc = (row.GhiChu ?? string.Empty).ToUpperInvariant();
            if (gc.Contains("MỐI NỐI P1") || gc.Contains("MOI NOI P1") || 
                (IsConnectionToExisting(row) && gc.Contains("P1") && !gc.Contains("P2")))
            {
                return true;
            }
            return false;
        }

        public static double ResolvePipeRadiusMeters(CulvertRowData data, FamilySymbol? sym = null)
        {
            // 1. Thử bóc tách từ Khẩu độ (ví dụ D1500, D1200, 1500, 2xD1500, D1.5m)
            string kd = (data.KhauDo ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(kd))
            {
                // Ưu tiên khớp dạng nhiều cửa như 2xD1500, 2x1500, 2*D1500
                var matchMult = Regex.Match(kd, @"\d+\s*[xX*]\s*(?:D|Ø|Phi)?\s*(\d+(?:[\.,]\d+)?)", RegexOptions.IgnoreCase);
                if (matchMult.Success && double.TryParse(matchMult.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double valMult))
                {
                    double dMeters = (valMult >= 100.0) ? valMult / 1000.0 : ((valMult >= 10.0) ? valMult / 100.0 : valMult);
                    if (dMeters >= 0.3 && dMeters <= 5.0)
                    {
                        return dMeters / 2.0;
                    }
                }

                var match = Regex.Match(kd, @"(?:D|Ø|Phi)\s*(\d+(?:[\.,]\d+)?)", RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    match = Regex.Match(kd, @"(\d+(?:[\.,]\d+)?)", RegexOptions.IgnoreCase);
                }
                if (match.Success && double.TryParse(match.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    double dMeters = (val >= 100.0) ? val / 1000.0 : ((val >= 10.0) ? val / 100.0 : val);
                    if (dMeters >= 0.3 && dMeters <= 5.0)
                    {
                        return dMeters / 2.0;
                    }
                }
            }

            // 2. Thử tìm trong tham số FamilySymbol (Diameter, D, Đường kính, Radius, R)
            if (sym != null)
            {
                string[] diamParams = new[] { "Diameter", "Đường kính", "DuongKinh", "D", "KhauDo", "D_TRONG", "CT_D" };
                foreach (var pName in diamParams)
                {
                    var p = sym.LookupParameter(pName);
                    if (p != null && p.StorageType == StorageType.Double && p.AsDouble() > 0.1)
                    {
                        double dMeters = UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Meters);
                        if (dMeters >= 0.3 && dMeters <= 5.0)
                        {
                            return dMeters / 2.0;
                        }
                    }
                }
            }

            // Fallback mặc định: D = 1.50m -> R = 0.75m
            return 0.75;
        }

        public static FamilySymbol ResolveBestPipeSymbol(Document doc, CulvertRowData data, FamilySymbol defaultSym)
        {
            if (defaultSym == null || doc == null || data == null) return defaultSym;

            try
            {
                // 1. Lấy bán kính và đường kính cống D từ Excel (mm)
                double rMeters = ResolvePipeRadiusMeters(data, defaultSym);
                double targetDiameterMm = Math.Round(rMeters * 2.0 * 1000.0);
                if (targetDiameterMm <= 0) return defaultSym;

                string targetDStr = $"D{targetDiameterMm}";
                string targetDNum = $"{targetDiameterMm}";

                // 2. Tìm trong các Type của cùng Family với defaultSym
                Family? fam = defaultSym.Family;
                if (fam != null)
                {
                    ISet<ElementId> symbolIds = fam.GetFamilySymbolIds();
                    FamilySymbol? exactMatch = null;
                    FamilySymbol? fallbackMatch = null;

                    foreach (ElementId sId in symbolIds)
                    {
                        if (doc.GetElement(sId) is FamilySymbol fs && fs.IsValidObject)
                        {
                            string fsName = fs.Name.ToUpperInvariant();
                            // Khớp chuẩn D1000, D1200, D1500, D800...
                            if (fsName.Contains(targetDStr.ToUpperInvariant()))
                            {
                                exactMatch = fs;
                                break;
                            }
                            if (fsName.Contains(targetDNum))
                            {
                                fallbackMatch = fs;
                            }
                        }
                    }

                    var chosen = exactMatch ?? fallbackMatch;
                    if (chosen != null)
                    {
                        ActivateSymbol(chosen);
                        return chosen;
                    }
                }
            }
            catch
            {
                // Fallback an toàn nếu có ngoại lệ
            }

            return defaultSym;
        }

        public static void BuildRoundCulvertBarrelsAndBeddings(
            Document doc,
            CulvertRowData data,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IList<CulvertComponentItem> barrelComponents,
            XYZ p1Culvert,
            XYZ p2Culvert,
            XYZ u,
            double rotAngle,
            double lStdFeet,
            double jointGapFeet,
            double khoangCachTimM,
            CulvertArrayMode arrayMode,
            IEnumerable<CulvertMaterialItem>? materialSettings,
            List<FamilyInstance>? outCreatedBarrels,
            List<FamilyInstance>? outCreatedBeddings,
            IList<CulvertComponentItem>? outletComponents,
            FamilyInstance? sampleOutlet,
            string? customPattern = null)
        {
            if (barrelComponents == null || barrelComponents.Count == 0) return;

            bool isDouble = IsDoublePipe(data);

            // Tách cấu kiện thân cống tròn vs cấu kiện móng (BTL, Đá dăm) vs Mối nối
            var pipeBarrelComps = barrelComponents.Where(c => c.IsActive && c.SelectedSymbol != null && !IsBeddingComponent(c) && !IsConnectionComponent(c)).ToList();
            var beddingComps = barrelComponents.Where(c => c.IsActive && c.SelectedSymbol != null && IsBeddingComponent(c)).ToList();

            if (pipeBarrelComps.Count == 0 && beddingComps.Count == 0) return;

            // Lấy chuỗi quy cách đốt từ dữ liệu dòng Excel nếu có
            if (string.IsNullOrEmpty(customPattern) && !string.IsNullOrEmpty(data.CustomPattern))
            {
                customPattern = data.CustomPattern;
            }

            // 1. RẢI THÂN CỐNG TRÒN (2 điểm adaptive đặt đúng vị trí chuẩn theo tọa độ Excel, không cộng bán kính R)
            bool isConn = IsConnectionToExisting(data);
            bool isConnP1 = isConn && IsConnectionAtP1(data);
            bool isConnP2 = isConn && !isConnP1;

            double offsetMoiNoiM = data.OffsetMoiNoi > 0 ? data.OffsetMoiNoi : 0.40;
            double offsetMoiNoiFeet = UnitUtils.ConvertToInternalUnits(offsetMoiNoiM, UnitTypeId.Meters);

            if (pipeBarrelComps.Count > 0)
            {
                if (!isDouble)
                {
                    // CỐNG TRÒN ĐƠN: Rải 1 nhánh dọc theo tim tuyến
                    // YÊU CẦU 2 (Người dùng đã chốt): cống tròn thì rải theo 2 điểm trong excel
                    XYZ pStartCenter = p1Culvert;
                    XYZ pEndCenter = p2Culvert;

                    LayRoundPipeSegments(
                        doc,
                        pipeBarrelComps,
                        pStartCenter,
                        pEndCenter,
                        lStdFeet,
                        jointGapFeet,
                        u,
                        rotAngle,
                        arrayMode,
                        bimConfig,
                        customBimParams,
                        familyParameterMappings,
                        data,
                        data.Z1,
                        data.Z2,
                        startDotIdx: 1,
                        branchSuffix: "",
                        materialSettings,
                        outCreatedBarrels,
                        customPattern);
                }
                else
                {
                    // CỐNG TRÒN ĐÔI: Rải 2 nhánh song song cách nhau khoảng cách tim S
                    double sM = (khoangCachTimM > 0.5) ? khoangCachTimM : 2.12;
                    double sFeet = UnitUtils.ConvertToInternalUnits(sM, UnitTypeId.Meters);
                    double halfSFeet = sFeet * 0.5;

                    // Vector pháp tuyến ngang vuông góc với tim tuyến cống
                    XYZ nPerp = new XYZ(-u.Y, u.X, 0).Normalize();

                    // Nhánh Trái (Branch 1): Rải theo 2 điểm trong Excel
                    XYZ offsetL = -nPerp * halfSFeet;
                    XYZ pStartL = p1Culvert + offsetL;
                    XYZ pEndL = p2Culvert + offsetL;

                    int countL = LayRoundPipeSegments(
                        doc,
                        pipeBarrelComps,
                        pStartL,
                        pEndL,
                        lStdFeet,
                        jointGapFeet,
                        u,
                        rotAngle,
                        arrayMode,
                        bimConfig,
                        customBimParams,
                        familyParameterMappings,
                        data,
                        data.Z1,
                        data.Z2,
                        startDotIdx: 1,
                        branchSuffix: "Trái",
                        materialSettings,
                        outCreatedBarrels,
                        customPattern);

                    // Nhánh Phải (Branch 2): Rải theo 2 điểm trong Excel
                    XYZ offsetR = nPerp * halfSFeet;
                    XYZ pStartR = p1Culvert + offsetR;
                    XYZ pEndR = p2Culvert + offsetR;

                    LayRoundPipeSegments(
                        doc,
                        pipeBarrelComps,
                        pStartR,
                        pEndR,
                        lStdFeet,
                        jointGapFeet,
                        u,
                        rotAngle,
                        arrayMode,
                        bimConfig,
                        customBimParams,
                        familyParameterMappings,
                        data,
                        data.Z1,
                        data.Z2,
                        startDotIdx: countL + 1,
                        branchSuffix: "Phải",
                        materialSettings,
                        outCreatedBarrels,
                        customPattern);
                }
            }

            // 2. RẢI BÊ TÔNG LÓT VÀ ĐÁ DĂM ĐỆM
            if (beddingComps.Count > 0)
            {
                // YÊU CẦU (Người dùng đã chốt):
                // Chỉ có mỗi đá dăm đệm với bê tông lót tiếp xúc với mối nối tại Điểm 1 (cách vô OffsetMoiNoi = 0.40m) mới dừng lại
                double cxTdDdW_M = ResolveCxTdDdW(doc, data, familyParameterMappings, outletComponents, sampleOutlet);
                double cxTdDdW_Feet = UnitUtils.ConvertToInternalUnits(cxTdDdW_M, UnitTypeId.Meters);

                // Đầu P1: nếu là mối nối thì rải tới đúng vị trí tiếp xúc mặt trong mối nối tại Điểm 1 (+u * offsetMoiNoi), nếu là cửa xả thường thì lùi theo mép tường đầu (+u * cxTdDdW)
                XYZ pBedStart = isConnP1 ? (p1Culvert + u * offsetMoiNoiFeet) : (p1Culvert + u * cxTdDdW_Feet);

                // Đầu P2: nếu là mối nối thì rải tới đúng vị trí tiếp xúc mặt trong mối nối tại Điểm 1 (-u * offsetMoiNoi), nếu là cửa xả thường thì lùi theo mép tường đầu (-u * cxTdDdW)
                XYZ pBedEnd = isConnP2 ? (p2Culvert - u * offsetMoiNoiFeet) : (p2Culvert - u * cxTdDdW_Feet);

                PlaceContinuousRoundBedding(
                    doc,
                    beddingComps,
                    pBedStart,
                    pBedEnd,
                    rotAngle,
                    bimConfig,
                    customBimParams,
                    familyParameterMappings,
                    data,
                    materialSettings,
                    outCreatedBeddings);
            }
        }

        private static int LayRoundPipeSegments(
            Document doc,
            IList<CulvertComponentItem> pipeComponents,
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
            IEnumerable<CulvertMaterialItem>? materialSettings,
            List<FamilyInstance>? createdBarrels,
            string? customPattern = null)
        {
            XYZ v = pEnd - pStart;
            double L = v.GetLength();
            if (L <= 0.001) return startDotIdx - 1;

            int dotIdx = startDotIdx;

            // NẾU CÓ CHUỖI QUY CÁCH ĐỐT TÙY CHỈNH (Ví dụ: 6@2.5, 2@3.0)
            var customLengths = ParseCustomSegmentPattern(customPattern);
            if (customLengths != null && customLengths.Count > 0)
            {
                XYZ ptCur = pStart;
                for (int i = 0; i < customLengths.Count; i++)
                {
                    double segM = customLengths[i];
                    double segFeet = UnitUtils.ConvertToInternalUnits(segM, UnitTypeId.Meters);
                    bool isFirst = (i == 0);
                    bool isLast = (i == customLengths.Count - 1);

                    XYZ ptA = ptCur;
                    XYZ ptB = ptCur + u * segFeet;

                    if ((ptB - pStart).GetLength() > L)
                    {
                        ptB = pEnd;
                        segFeet = (ptB - ptA).GetLength();
                    }

                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    PlaceRoundPipeInstance(doc, pipeComponents, ptA, ptB, segFeet, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, isFirst ? zDauM : null, isLast ? zCuoiM : null, materialSettings, createdBarrels);

                    ptCur = ptB + u * jointGap;
                    if ((ptCur - pStart).GetLength() >= L - 0.05)
                    {
                        break;
                    }
                }

                // Nếu còn khoảng cách dư đến pEnd thì rải nốt đốt bù
                double remainDist = (pEnd - ptCur).GetLength();
                if (remainDist > 0.05 && (pEnd - ptCur).DotProduct(u) > 0)
                {
                    string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                    PlaceRoundPipeInstance(doc, pipeComponents, ptCur, pEnd, remainDist, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, null, zCuoiM, materialSettings, createdBarrels);
                }

                return dotIdx - 1;
            }

            // MẶC ĐỊNH: RẢI THEO ĐỐT CHUẨN + ĐỐT BÙ
            double pitch = lStd + jointGap;
            double minSegLen = UnitUtils.ConvertToInternalUnits(0.40, UnitTypeId.Meters);

            if (L <= lStd + 0.05)
            {
                // Chỉ có 1 đốt kết nối pStart -> pEnd
                string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                PlaceRoundPipeInstance(doc, pipeComponents, pStart, pEnd, L, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, zDauM, zCuoiM, materialSettings, createdBarrels);
                return dotIdx - 1;
            }

            int N = (int)Math.Floor((L - minSegLen) / pitch);
            if (N < 0) N = 0;

            // Rải các đốt chuẩn từ pStart
            for (int i = 0; i < N; i++)
            {
                bool isFirst = (i == 0);
                string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                XYZ ptA = pStart + u * (i * pitch);
                XYZ ptB = ptA + u * lStd;

                PlaceRoundPipeInstance(doc, pipeComponents, ptA, ptB, lStd, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, isFirst ? zDauM : null, null, materialSettings, createdBarrels);
            }

            // Đốt co giãn cuối cùng kết thúc CHÍNH XÁC tại pEnd
            XYZ ptALast = pStart + u * (N * pitch);
            XYZ ptBLast = pEnd;
            double segLenLast = (ptBLast - ptALast).GetLength();

            if (segLenLast > 0.001)
            {
                string tenCK = FormatDotName(bimConfig.MauTenDotCong, dotIdx++, branchSuffix);
                PlaceRoundPipeInstance(doc, pipeComponents, ptALast, ptBLast, segLenLast, angle, bimConfig, customBimParams, familyParameterMappings, rowData, tenCK, (N == 0) ? zDauM : null, zCuoiM, materialSettings, createdBarrels);
            }

            return dotIdx - 1;
        }

        private static void PlaceRoundPipeInstance(
            Document doc,
            IList<CulvertComponentItem> pipeComponents,
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
            IEnumerable<CulvertMaterialItem>? materialSettings,
            List<FamilyInstance>? createdBarrels)
        {
            if (pipeComponents == null) return;
            string[] possibleLenParams = new[] { "L", "Length", "ChieuDai", "L_dot_chuan", "L_dot_bu", "Chiều dài", "L_DOT", "CT_L" };

            // YÊU CẦU 2 (Người dùng đã chốt):
            // Mặc định: rải từ Điểm 1 đến Điểm 2 -> Điểm 1 là Đầu âm (ptA), Điểm 2 là Đầu dương (ptB)
            // Đảo đầu âm/dương (IsReversedPlacement == true): Điểm 1 là Đầu dương (ptA), Điểm 2 là Đầu âm (ptB)
            bool isReversed = rowData.IsReversedPlacement;

            foreach (var comp in pipeComponents)
            {
                if (!comp.IsActive || comp.SelectedSymbol == null) continue;
                var baseSym = comp.SelectedSymbol.GetFreshSymbol(doc);
                if (baseSym == null) continue;

                // Tự động đối chiếu thông minh loại đường kính D phù hợp với dòng Excel
                var sym = ResolveBestPipeSymbol(doc, rowData, baseSym);
                ActivateSymbol(sym);

                double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
                XYZ offZ = new XYZ(0, 0, offFeetZ);
                XYZ ptA = ptStart + offZ;
                XYZ ptB = ptEnd + offZ;

                XYZ ptPoint1 = isReversed ? ptB : ptA;
                XYZ ptPoint2 = isReversed ? ptA : ptB;

                FamilyInstance? inst = null;
                try
                {
                    if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(sym))
                    {
                        inst = CreateAdaptiveInstance(doc, sym, new[] { ptPoint1, ptPoint2 });
                    }
                    else
                    {
                        XYZ ptMid = (ptA + ptB) * 0.5;
                        inst = doc.Create.NewFamilyInstance(ptMid, sym, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        double rot = isReversed ? (angle + Math.PI) : angle;
                        var axis = Line.CreateBound(ptMid, ptMid + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(doc, inst.Id, axis, rot);
                    }
                }
                catch { }

                if (inst == null) continue;
                createdBarrels?.Add(inst);

                // Gán tham số chiều dài
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

                // Gán thông tin BIM & Vật liệu
                string nameSub = $"{tenCauKien}_{comp.CategoryType}";
                try { BimParameterService.SetElementBimProperties(inst, bimConfig, nameSub, comp.CategoryType, zDau, zCuoi, null, null, null); } catch { }
                try { BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, rowData, comp.CategoryType); } catch { }
                try { CulvertCoreEngine.TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType); } catch { }
            }
        }

        private static void PlaceContinuousRoundBedding(
            Document doc,
            IList<CulvertComponentItem> beddingComponents,
            XYZ pBedStart,
            XYZ pBedEnd,
            double rotAngle,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            CulvertRowData rowData,
            IEnumerable<CulvertMaterialItem>? materialSettings,
            List<FamilyInstance>? createdBeddings)
        {
            if (beddingComponents == null || beddingComponents.Count == 0) return;
            double lenFeet = (pBedEnd - pBedStart).GetLength();
            if (lenFeet <= 0.01) return;

            string[] possibleLenParams = new[] { "L", "Length", "ChieuDai", "L_dot_chuan", "L_dot_bu", "Chiều dài", "L_DOT", "CT_L", "CH_L" };

            foreach (var comp in beddingComponents)
            {
                if (!comp.IsActive || comp.SelectedSymbol == null) continue;
                var sym = comp.SelectedSymbol.GetFreshSymbol(doc);
                if (sym == null) continue;

                ActivateSymbol(sym);

                double offFeetZ = UnitUtils.ConvertToInternalUnits(comp.OffsetZ, UnitTypeId.Meters);
                XYZ offZ = new XYZ(0, 0, offFeetZ);
                XYZ ptA = pBedStart + offZ;
                XYZ ptB = pBedEnd + offZ;

                FamilyInstance? inst = null;
                try
                {
                    if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(sym))
                    {
                        inst = CreateAdaptiveInstance(doc, sym, new[] { ptA, ptB });
                    }
                    else
                    {
                        XYZ ptMid = (ptA + ptB) * 0.5;
                        inst = doc.Create.NewFamilyInstance(ptMid, sym, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        var axis = Line.CreateBound(ptMid, ptMid + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(doc, inst.Id, axis, rotAngle);

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

                if (inst == null) continue;
                createdBeddings?.Add(inst);

                string nameSub = $"{comp.CategoryType}";
                try { BimParameterService.SetElementBimProperties(inst, bimConfig, nameSub, comp.CategoryType, rowData.Z1, rowData.Z2, null, null, null); } catch { }
                try { BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, rowData, comp.CategoryType); } catch { }
                try { CulvertCoreEngine.TryApplyMaterial(doc, inst, materialSettings, comp.CategoryType); } catch { }
            }
        }

        /// <summary>
        /// YÊU CẦU 1 & 4 (Người dùng đã chốt): Rải Family Mối Nối đấu nối cống hiện trạng cũ tại đầu chỉ định (P1 hoặc P2)
        /// - Điểm 1 đặt tại điểm ở Excel (ptBase) + dịch chuyển chênh cao Z khai báo
        /// - Điểm 2 hướng vô trong thân cống một đoạn bằng khoảng cách khai báo OffsetMoiNoi + dịch chuyển Z
        /// Sử dụng Family Adaptive 2 điểm đặt chuẩn xác, không tự ý đảo thứ tự parameter,
        /// và tuyệt đối không rải thêm phụ kiện cửa xả nào khác tại đầu này.
        /// </summary>
        public static FamilyInstance? PlaceRoundCulvertConnectionJoint(
            Document doc,
            XYZ ptBase,
            XYZ uCulvert,
            FamilySymbol? connSymbol,
            BimInfoConfig bimConfig,
            IEnumerable<CustomBimParameterItem>? customBimParams,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            CulvertRowData data,
            bool isUpstream,
            IEnumerable<CulvertMaterialItem>? materialSettings,
            List<FamilyInstance>? outCreatedOutlets,
            double offsetZ_Meters = 0.0)
        {
            if (connSymbol == null || doc == null) return null;
            ActivateSymbol(connSymbol);

            // Vector hướng đặt mối nối theo yêu cầu người dùng:
            // - Tại P1 (Thượng lưu / Upstream): hướng vô trong cống là cùng chiều tim cống (+uDirNorm)
            // - Tại P2 (Hạ lưu / Downstream): hướng vô trong cống là ngược chiều tim cống (-uDirNorm)
            XYZ uDirNorm = new XYZ(uCulvert.X, uCulvert.Y, 0).Normalize();
            XYZ uIn = isUpstream ? uDirNorm : -uDirNorm;

            double offsetMoiNoiM = data.OffsetMoiNoi > 0 ? data.OffsetMoiNoi : 0.40;
            double offsetMoiNoiFeet = UnitUtils.ConvertToInternalUnits(offsetMoiNoiM, UnitTypeId.Meters);
            double offsetZFeet = UnitUtils.ConvertToInternalUnits(offsetZ_Meters, UnitTypeId.Meters);
            XYZ zVec = XYZ.BasisZ * offsetZFeet;

            // YÊU CẦU CUỐI CÙNG (Người dùng đã chốt):
            // Từ điểm 1 cách vô 1 khoảng 0.4m chứ không phải cách điểm 2 vô 1 đoạn 0.4m:
            // - Điểm 2 Adaptive: đặt tại điểm ở Excel (ptBase) + dịch chuyển Z
            // - Điểm 1 Adaptive: cách vô trong đúng 1 đoạn bằng khoảng cách khai báo (OffsetMoiNoi = 0.40m) + dịch chuyển Z
            XYZ p2Adaptive = ptBase + zVec;
            XYZ p1Adaptive = ptBase + uIn * offsetMoiNoiFeet + zVec;

            FamilyInstance? inst = null;
            try
            {
                if (AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(connSymbol))
                {
                    inst = CreateAdaptiveInstance(doc, connSymbol, new[] { p1Adaptive, p2Adaptive });
                }
                else
                {
                    XYZ ptMid = (p1Adaptive + p2Adaptive) * 0.5;
                    inst = doc.Create.NewFamilyInstance(ptMid, connSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    double rotIn = Math.Atan2(-uIn.Y, -uIn.X);
                    var axis = Line.CreateBound(ptMid, ptMid + XYZ.BasisZ);
                    ElementTransformUtils.RotateElement(doc, inst.Id, axis, rotIn);
                }
            }
            catch { }

            if (inst == null) return null;
            outCreatedOutlets?.Add(inst);

            // Gán thông số chiều dài nếu có
            try
            {
                var pL = inst.LookupParameter("L") ?? inst.LookupParameter("Length") ?? inst.LookupParameter("ChieuDai");
                if (pL != null && !pL.IsReadOnly && pL.StorageType == StorageType.Double)
                {
                    pL.Set(offsetMoiNoiFeet);
                }
            }
            catch { }

            // Gán thông tin BIM và vật liệu
            string suffix = isUpstream ? "TL" : "HL";
            string tenCK = $"MỐI NỐI HIỆN TRẠNG_{suffix}";
            string loaiCK = "MỐI NỐI CỐNG HIỆN TRẠNG";
            try { BimParameterService.SetElementBimProperties(inst, bimConfig, tenCK, loaiCK, null, null, p1Adaptive.X, p1Adaptive.Y, p1Adaptive.Z); } catch { }
            try { BimParameterService.ApplyFamilyMappedParameters(inst, familyParameterMappings, data, "Mối nối"); } catch { }
            try { CulvertCoreEngine.TryApplyMaterial(doc, inst, materialSettings, "Mối nối", "CỐNG TRÒN"); } catch { }

            return inst;
        }

        public static void ExecuteRoundCulvertCuts(
            Document doc,
            List<FamilyInstance>? outlets,
            List<FamilyInstance>? barrels,
            List<FamilyInstance>? beddings)
        {
            if (barrels == null || barrels.Count == 0) return;

            var validBarrels = barrels.Where(b => b != null && b.IsValidObject).ToList();
            if (validBarrels.Count == 0) return;

            XYZ GetCenter(FamilyInstance inst)
            {
                if (inst.Location is LocationPoint lp) return lp.Point;
                if (inst.Location is LocationCurve lc) return (lc.Curve.GetEndPoint(0) + lc.Curve.GetEndPoint(1)) * 0.5;
                if (AdaptiveComponentInstanceUtils.IsAdaptiveComponentInstance(inst))
                {
                    var pIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(inst);
                    if (pIds.Count > 0)
                    {
                        XYZ sum = XYZ.Zero;
                        int count = 0;
                        foreach (var id in pIds)
                        {
                            if (doc.GetElement(id) is ReferencePoint rp)
                            {
                                sum += rp.Position;
                                count++;
                            }
                        }
                        if (count > 0) return sum / count;
                    }
                }
                var bb = inst.get_BoundingBox(null);
                if (bb != null) return (bb.Min + bb.Max) * 0.5;
                return XYZ.Zero;
            }

            // 1. Cut Geometry giữa TƯỜNG ĐẦU CỬA XẢ với phần cống tròn tiếp xúc
            if (outlets != null && outlets.Count > 0)
            {
                var headwalls = outlets.Where(o =>
                {
                    if (o == null || !o.IsValidObject) return false;
                    string fn = (o.Symbol?.FamilyName ?? "").ToUpperInvariant();
                    string tn = (o.Symbol?.Name ?? "").ToUpperInvariant();
                    return (fn.Contains("TUONG DAU") || fn.Contains("TƯỜNG ĐẦU") || tn.Contains("TUONG DAU") || tn.Contains("TƯỜNG ĐẦU") ||
                            fn.Contains("SAN CONG") || fn.Contains("SÂN CỐNG") || fn.Contains("CUA XA") || fn.Contains("CỬA XẢ")) &&
                           !fn.Contains("MOI NOI") && !fn.Contains("MỐI NỐI") && !tn.Contains("MOI NOI") && !tn.Contains("MỐI NỐI");
                }).ToList();

                foreach (var hw in headwalls)
                {
                    foreach (var bar in validBarrels)
                    {
                        CulvertCoreEngine.TryCutSolidDirectional(doc, hw, bar);
                    }
                }
            }

            // 2. Cut Geometry giữa BÊ TÔNG LÓT CỐNG TRÒN với TOÀN BỘ cống tròn tiếp xúc
            // Đảm bảo đơn hướng: BTL bị cắt bởi cống tròn (đáy cống tròn khoét bệ đỡ vào BTL), TUYỆT ĐỐI không để BTL cắt cống tròn!
            if (beddings != null && beddings.Count > 0)
            {
                var btlBeddings = beddings.Where(b =>
                {
                    if (b == null || !b.IsValidObject) return false;
                    string fn = (b.Symbol?.FamilyName ?? "").ToUpperInvariant();
                    string tn = (b.Symbol?.Name ?? "").ToUpperInvariant();
                    bool isBtl = fn.Contains("BE TONG LOT") || fn.Contains("BTL") || fn.Contains("LÓT") || fn.Contains("LOT") ||
                                 tn.Contains("BE TONG LOT") || tn.Contains("BTL") || tn.Contains("LÓT") || tn.Contains("LOT");
                    bool isDaDam = fn.Contains("DA DAM") || fn.Contains("ĐÁ DĂM") || fn.Contains("DEM") || fn.Contains("ĐỆM") ||
                                   tn.Contains("DA DAM") || tn.Contains("ĐÁ DĂM") || tn.Contains("DEM") || tn.Contains("ĐỆM");
                    return isBtl && !isDaDam;
                }).ToList();

                foreach (var bed in btlBeddings)
                {
                    foreach (var bar in validBarrels)
                    {
                        CulvertCoreEngine.TryCutSolidDirectional(doc, bed, bar);
                    }
                }
            }

            // 3. Cut Geometry giữa MỐI NỐI CỐNG với cống tròn tiếp xúc
            if (outlets != null && outlets.Count > 0)
            {
                var joints = outlets.Where(o =>
                {
                    if (o == null || !o.IsValidObject) return false;
                    string fn = (o.Symbol?.FamilyName ?? "").ToUpperInvariant();
                    string tn = (o.Symbol?.Name ?? "").ToUpperInvariant();
                    return fn.Contains("MOI NOI") || fn.Contains("MỐI NỐI") || tn.Contains("MOI NOI") || tn.Contains("MỐI NỐI");
                }).ToList();

                foreach (var jt in joints)
                {
                    foreach (var bar in validBarrels)
                    {
                        CulvertCoreEngine.TryCutSolidDirectional(doc, jt, bar);
                    }
                }
            }
        }

        private static FamilyInstance? CreateAdaptiveInstance(Document doc, FamilySymbol sym, IList<XYZ> points)
        {
            if (sym == null || points == null || points.Count == 0) return null;
            ActivateSymbol(sym);

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
                // Fallback nếu đảo thứ tự điểm
                if (points.Count >= 2)
                {
                    try
                    {
                        FamilyInstance inst = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, sym);
                        IList<ElementId> placePointIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(inst);
                        var rev = points.Reverse().ToList();
                        for (int i = 0; i < Math.Min(rev.Count, placePointIds.Count); i++)
                        {
                            if (doc.GetElement(placePointIds[i]) is ReferencePoint refPt)
                            {
                                refPt.Position = rev[i];
                            }
                        }
                        try { doc.Regenerate(); } catch { }
                        return inst;
                    }
                    catch { }
                }
            }

            return null;
        }

        private static void ActivateSymbol(FamilySymbol sym)
        {
            if (!sym.IsActive)
            {
                try { sym.Activate(); } catch { }
            }
        }

        private static bool IsBeddingComponent(CulvertComponentItem c)
        {
            if (c == null) return false;
            string cat = (c.CategoryType ?? string.Empty).ToUpperInvariant();
            string fam = (c.SelectedSymbol?.FamilyName ?? string.Empty).ToUpperInvariant();
            return cat.Contains("LÓT") || cat.Contains("LOT") || cat.Contains("ĐỆM") || cat.Contains("DEM") ||
                   cat.Contains("ĐÁ DĂM") || cat.Contains("DA DAM") ||
                   fam.Contains("BE TONG LOT") || fam.Contains("DA DAM DEM");
        }

        public static bool IsConnectionComponent(CulvertComponentItem c)
        {
            if (c == null) return false;
            string cat = (c.CategoryType ?? string.Empty).ToUpperInvariant();
            string fam = (c.SelectedSymbol?.FamilyName ?? string.Empty).ToUpperInvariant();
            return cat.Contains("MỐI NỐI") || cat.Contains("MOI NOI") ||
                   fam.Contains("MOI NOI") || fam.Contains("MOI_NOI");
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

        private static double ResolveCxTdDdW(
            Document doc,
            CulvertRowData data,
            IEnumerable<ParameterMappingItem>? familyParameterMappings,
            IList<CulvertComponentItem>? outletComponents,
            FamilyInstance? sampleOutletInst)
        {
            // 1. Thử lấy từ ParameterOverrides của dòng dữ liệu
            string? valStrW = data.GetParamOverride("Cửa xả", "Cửa xả - Tường đầu", "CX_TD_DD_W", null);
            if (string.IsNullOrEmpty(valStrW))
                valStrW = data.GetParamOverride("Cửa xả", "CX_TD_DD_W", null);
            if (string.IsNullOrEmpty(valStrW))
                valStrW = data.GetParamOverride(null, "CX_TD_DD_W", null);
            if (string.IsNullOrEmpty(valStrW) && data.ParameterOverrides != null)
            {
                foreach (var kvp in data.ParameterOverrides)
                {
                    if (kvp.Key.EndsWith("::CX_TD_DD_W", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(kvp.Key, "CX_TD_DD_W", StringComparison.OrdinalIgnoreCase))
                    {
                        valStrW = kvp.Value;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(valStrW) && double.TryParse(valStrW.Replace("mm", "").Replace("m", "").Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dValW))
            {
                double m = (Math.Abs(dValW) >= 10.0) ? dValW / 1000.0 : dValW;
                if (m > 0.01) return m;
            }

            // 2. Thử lấy từ familyParameterMappings
            var mappingW = familyParameterMappings?.FirstOrDefault(m =>
                string.Equals(m.InternalName, "CX_TD_DD_W", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.InternalName, "TD_DD_W", StringComparison.OrdinalIgnoreCase));
            if (mappingW != null)
            {
                string? mapVal = !string.IsNullOrEmpty(mappingW.CustomValue) ? mappingW.CustomValue : mappingW.DefaultValue;
                if (!string.IsNullOrEmpty(mapVal) && double.TryParse(mapVal.Replace("mm", "").Replace("m", "").Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dMapVal))
                {
                    double m = (Math.Abs(dMapVal) >= 10.0) ? dMapVal / 1000.0 : dMapVal;
                    if (m > 0.01) return m;
                }
            }

            // 3. Thử lấy từ FamilyInstance hoặc FamilySymbol cửa xả
            if (sampleOutletInst != null)
            {
                var p = sampleOutletInst.LookupParameter("CX_TD_DD_W") ?? sampleOutletInst.Symbol?.LookupParameter("CX_TD_DD_W");
                if (p != null && p.StorageType == StorageType.Double && p.AsDouble() > 0.01)
                {
                    return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Meters);
                }
            }
            if (outletComponents != null)
            {
                foreach (var comp in outletComponents)
                {
                    var sym = comp.SelectedSymbol?.GetFreshSymbol(doc);
                    var p = sym?.LookupParameter("CX_TD_DD_W");
                    if (p != null && p.StorageType == StorageType.Double && p.AsDouble() > 0.01)
                    {
                        return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Meters);
                    }
                }
            }

            return 0.40;
        }
    }
}
