using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using InfraBIM.CulvertTool.Models;

namespace InfraBIM.CulvertTool.Services
{
    /// <summary>
    /// Tính toán các thông số hình học và phân rã các đốt cống phục vụ vẽ bản vẽ xem trước 2D/3D
    /// </summary>
    public static class CulvertPreviewService
    {
        public static CulvertPreviewGeometry ComputePreview(
            CulvertRowData row,
            double lStd,
            double lMin,
            double lNgam,
            double bBox,
            double offsetZ_BTL,
            double offsetZ_Cat,
            CulvertArrayMode arrayMode,
            bool hasBtlDot = true,
            bool hasBtlSan = false,
            bool hasBtlHn = false,
            double offsetZ_BTL_San = -0.10,
            double offsetZ_BTL_HN = -0.30)
        {
            var geom = new CulvertPreviewGeometry
            {
                STT = row.STT,
                LyTrinh = row.LyTrinh,
                LoaiCong = row.LoaiCong,
                SoCua = row.SoCua,
                KhauDo = row.KhauDo,
                KhoangCachTim = row.KhoangCachTim > 0.1 ? row.KhoangCachTim : 2.0,
                TotalLengthM = row.ChieuDai > 0 ? row.ChieuDai : row.TinhChieuDai2D(),
                DoDocPercent = row.DoDoc,
                GocXoayDeg = row.GocXoay,
                Z1 = row.Z1,
                Z2 = row.Z2,
                DeltaH = row.Z1 - row.Z2,
                L_Std = lStd > 0.1 ? lStd : 1.0,
                L_Min = lMin > 0.05 ? lMin : 0.5,
                L_Ngam = lNgam >= 0 ? lNgam : 0.3,
                B_Box = bBox > 0.2 ? bBox : 1.5,
                OffsetZ_BTL = offsetZ_BTL,
                OffsetZ_Cat = offsetZ_Cat,
                HasBTL_Dot = hasBtlDot,
                HasBTL_San = hasBtlSan,
                HasBTL_HN = hasBtlHn,
                OffsetZ_BTL_Dot = offsetZ_BTL,
                OffsetZ_BTL_San = offsetZ_BTL_San,
                OffsetZ_BTL_HN = offsetZ_BTL_HN
            };

            // Phân tích khẩu độ
            ParseDimensions(row.KhauDo, out double w, out double h);
            geom.BarrelWidthM = w;
            geom.BarrelHeightM = h;

            double totalL = geom.TotalLengthM;
            if (totalL <= 0.5) return geom;

            // Thiết lập sân Thượng lưu & Hạ lưu
            geom.ApronTL = new PreviewApronItem
            {
                Title = "Sân thượng lưu",
                PositionX = 0,
                ElevationZ = geom.Z1,
                LengthM = 2.0,
                WallHeightM = geom.BarrelHeightM + 0.6,
                SlabThicknessM = 0.3
            };

            geom.ApronHL = new PreviewApronItem
            {
                Title = "Sân hạ lưu",
                PositionX = totalL,
                ElevationZ = geom.Z2,
                LengthM = 2.0,
                WallHeightM = geom.BarrelHeightM + 0.6,
                SlabThicknessM = 0.3
            };

            // Tính toán phân bổ đốt cống dọc tuyến
            int dotCounter = 1;
            int stdCount = 0;
            int compCount = 0;
            double compLenSum = 0.0;
            bool isCompValid = true;

            // Hệ số nội suy cao độ: Z(x) = Z1 + (x / totalL) * (Z2 - Z1)
            double GetElevationAt(double dist)
            {
                if (totalL <= 0.001) return geom.Z1;
                double frac = Math.Clamp(dist / totalL, 0.0, 1.0);
                return geom.Z1 + frac * (geom.Z2 - geom.Z1);
            }

            if (row.SoHopNoi == 0)
            {
                // Trường hợp 0 hộp nối: Rải giữa 2 sân cống
                double startDist = geom.L_Ngam;
                double segLen = totalL - (2 * geom.L_Ngam);
                if (segLen > 0)
                {
                    GenerateSpanSegments(geom, startDist, segLen, geom.L_Std, geom.L_Min, arrayMode, ref dotCounter, ref stdCount, ref compCount, ref compLenSum, ref isCompValid, GetElevationAt);
                }
            }
            else if (row.SoHopNoi == 1)
            {
                // Trường hợp 1 hộp nối
                double distHN1 = Math.Clamp(row.KC_HN1, geom.L_Ngam + 0.5, totalL - geom.L_Ngam - 0.5);
                double zHN1 = GetElevationAt(distHN1);

                geom.Manholes.Add(new PreviewManholeItem
                {
                    Index = 1,
                    Title = "Hố thu 1",
                    DistanceFromP1M = distHN1,
                    WidthM = geom.B_Box,
                    ElevationZ = zHN1,
                    HeightM = geom.BarrelHeightM + 0.8
                });

                // Nhịp 1: Sân 1 -> Hộp 1
                double start1 = geom.L_Ngam;
                double len1 = distHN1 - geom.L_Ngam - (geom.B_Box / 2.0);
                if (len1 > 0)
                {
                    GenerateSpanSegments(geom, start1, len1, geom.L_Std, geom.L_Min, arrayMode, ref dotCounter, ref stdCount, ref compCount, ref compLenSum, ref isCompValid, GetElevationAt);
                }

                // Nhịp 2: Hộp 1 -> Sân 2
                double start2 = distHN1 + (geom.B_Box / 2.0);
                double len2 = (totalL - distHN1) - geom.L_Ngam - (geom.B_Box / 2.0);
                if (len2 > 0)
                {
                    GenerateSpanSegments(geom, start2, len2, geom.L_Std, geom.L_Min, arrayMode, ref dotCounter, ref stdCount, ref compCount, ref compLenSum, ref isCompValid, GetElevationAt);
                }
            }
            else // SoHopNoi >= 2
            {
                double distHN1 = Math.Clamp(row.KC_HN1, geom.L_Ngam + 0.5, totalL / 2.0);
                double distHN2 = Math.Clamp(row.KC_HN2, geom.L_Ngam + 0.5, totalL / 2.0);
                double posHN2 = totalL - distHN2;

                geom.Manholes.Add(new PreviewManholeItem
                {
                    Index = 1,
                    Title = "Hố thu 1",
                    DistanceFromP1M = distHN1,
                    WidthM = geom.B_Box,
                    ElevationZ = GetElevationAt(distHN1),
                    HeightM = geom.BarrelHeightM + 0.8
                });

                geom.Manholes.Add(new PreviewManholeItem
                {
                    Index = 2,
                    Title = "Hố thu 2",
                    DistanceFromP1M = posHN2,
                    WidthM = geom.B_Box,
                    ElevationZ = GetElevationAt(posHN2),
                    HeightM = geom.BarrelHeightM + 0.8
                });

                // Nhịp 1
                double len1 = distHN1 - geom.L_Ngam - (geom.B_Box / 2.0);
                if (len1 > 0)
                {
                    GenerateSpanSegments(geom, geom.L_Ngam, len1, geom.L_Std, geom.L_Min, arrayMode, ref dotCounter, ref stdCount, ref compCount, ref compLenSum, ref isCompValid, GetElevationAt);
                }

                // Nhịp 2
                double start2 = distHN1 + (geom.B_Box / 2.0);
                double len2 = (posHN2 - (geom.B_Box / 2.0)) - start2;
                if (len2 > 0)
                {
                    GenerateSpanSegments(geom, start2, len2, geom.L_Std, geom.L_Min, arrayMode, ref dotCounter, ref stdCount, ref compCount, ref compLenSum, ref isCompValid, GetElevationAt);
                }

                // Nhịp 3
                double start3 = posHN2 + (geom.B_Box / 2.0);
                double len3 = distHN2 - geom.L_Ngam - (geom.B_Box / 2.0);
                if (len3 > 0)
                {
                    GenerateSpanSegments(geom, start3, len3, geom.L_Std, geom.L_Min, arrayMode, ref dotCounter, ref stdCount, ref compCount, ref compLenSum, ref isCompValid, GetElevationAt);
                }
            }

            geom.StandardCount = stdCount;
            geom.CompensatingCount = compCount;
            geom.CompensatingLengthM = compLenSum;
            geom.IsCompensatingValid = isCompValid;

            if (!isCompValid)
            {
                geom.ValidationMessage = $"⚠️ Cảnh báo: Chiều dài đốt bù < L_min ({geom.L_Min}m). Cần hiệu chỉnh chiều dài đốt chuẩn hoặc chiều dài cống!";
            }
            else if (compCount > 0)
            {
                geom.ValidationMessage = $"✅ Hợp lệ: Rải {stdCount} đốt chuẩn ({geom.L_Std}m) + {compCount} đốt bù ({compLenSum:N2}m) đạt yêu cầu kỹ thuật.";
            }
            else
            {
                geom.ValidationMessage = $"✅ Hợp lệ: Toàn bộ cống khớp chẵn {stdCount} đốt chuẩn ({geom.L_Std}m).";
            }

            return geom;
        }

        private static void GenerateSpanSegments(
            CulvertPreviewGeometry geom,
            double startDist,
            double spanLen,
            double lStd,
            double lMin,
            CulvertArrayMode arrayMode,
            ref int dotCounter,
            ref int stdCount,
            ref int compCount,
            ref double compLenSum,
            ref bool isCompValid,
            Func<double, double> getElevation)
        {
            if (spanLen <= 0.001) return;

            if (arrayMode == CulvertArrayMode.CenterOut)
            {
                int n = (int)Math.Floor(spanLen / lStd);
                double lBien = (spanLen - (n * lStd)) / 2.0;

                if (lBien < lMin && n > 0)
                {
                    n = (n >= 2) ? n - 2 : 0;
                    lBien = (spanLen - (n * lStd)) / 2.0;
                }

                double cur = startDist;

                // Đốt biên 1
                if (lBien > 0.005)
                {
                    if (lBien < lMin) isCompValid = false;
                    compCount++;
                    compLenSum += lBien;
                    geom.Segments.Add(new PreviewSegmentItem
                    {
                        Index = dotCounter++,
                        SegmentName = $"Đốt bù 1 ({lBien:N2}m)",
                        IsStandard = false,
                        LengthM = lBien,
                        StartDistanceM = cur,
                        EndDistanceM = cur + lBien,
                        StartElevationZ = getElevation(cur),
                        EndElevationZ = getElevation(cur + lBien)
                    });
                    cur += lBien;
                }

                // Các đốt chuẩn ở giữa
                for (int i = 0; i < n; i++)
                {
                    stdCount++;
                    geom.Segments.Add(new PreviewSegmentItem
                    {
                        Index = dotCounter++,
                        SegmentName = $"Đốt chuẩn ({lStd:N2}m)",
                        IsStandard = true,
                        LengthM = lStd,
                        StartDistanceM = cur,
                        EndDistanceM = cur + lStd,
                        StartElevationZ = getElevation(cur),
                        EndElevationZ = getElevation(cur + lStd)
                    });
                    cur += lStd;
                }

                // Đốt biên 2
                if (lBien > 0.005)
                {
                    if (lBien < lMin) isCompValid = false;
                    compCount++;
                    compLenSum += lBien;
                    geom.Segments.Add(new PreviewSegmentItem
                    {
                        Index = dotCounter++,
                        SegmentName = $"Đốt bù 2 ({lBien:N2}m)",
                        IsStandard = false,
                        LengthM = lBien,
                        StartDistanceM = cur,
                        EndDistanceM = cur + lBien,
                        StartElevationZ = getElevation(cur),
                        EndElevationZ = getElevation(cur + lBien)
                    });
                }
            }
            else // OneWay
            {
                int n = (int)Math.Floor(spanLen / lStd);
                double lDu = spanLen - (n * lStd);

                if (lDu < lMin && n > 0)
                {
                    n = n - 1;
                    lDu = spanLen - (n * lStd);
                }

                double cur = startDist;

                for (int i = 0; i < n; i++)
                {
                    stdCount++;
                    geom.Segments.Add(new PreviewSegmentItem
                    {
                        Index = dotCounter++,
                        SegmentName = $"Đốt chuẩn ({lStd:N2}m)",
                        IsStandard = true,
                        LengthM = lStd,
                        StartDistanceM = cur,
                        EndDistanceM = cur + lStd,
                        StartElevationZ = getElevation(cur),
                        EndElevationZ = getElevation(cur + lStd)
                    });
                    cur += lStd;
                }

                if (lDu > 0.005)
                {
                    if (lDu < lMin) isCompValid = false;
                    compCount++;
                    compLenSum += lDu;
                    geom.Segments.Add(new PreviewSegmentItem
                    {
                        Index = dotCounter++,
                        SegmentName = $"Đốt bù ({lDu:N2}m)",
                        IsStandard = false,
                        LengthM = lDu,
                        StartDistanceM = cur,
                        EndDistanceM = cur + lDu,
                        StartElevationZ = getElevation(cur),
                        EndElevationZ = getElevation(cur + lDu)
                    });
                }
            }
        }

        private static void ParseDimensions(string khauDo, out double width, out double height)
        {
            width = 1.5;
            height = 1.5;
            if (string.IsNullOrWhiteSpace(khauDo)) return;

            string s = khauDo.Trim().ToUpperInvariant();

            // Khớp cống tròn: D1000, D1200, D1500, D2000, 1000, 1200, 1500
            var matchD = Regex.Match(s, @"D?(\d{3,4})");
            if (matchD.Success && double.TryParse(matchD.Groups[1].Value, out double dMm))
            {
                double dM = dMm / 1000.0;
                width = dM;
                height = dM;
                return;
            }

            // Khớp cống hộp: 1.5x1.5, 2.0x2.0, 1x1, 2.5x2.5
            var matchBox = Regex.Match(s, @"(\d+(\.\d+)?)\s*[X*x]\s*(\d+(\.\d+)?)");
            if (matchBox.Success)
            {
                if (double.TryParse(matchBox.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double wVal))
                {
                    width = wVal;
                }
                if (double.TryParse(matchBox.Groups[3].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hVal))
                {
                    height = hVal;
                }
            }
        }
    }
}
