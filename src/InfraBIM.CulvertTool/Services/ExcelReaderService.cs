using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using InfraBIM.CulvertTool.Models;

namespace InfraBIM.CulvertTool.Services
{
    public static class ExcelReaderService
    {
        /// <summary>
        /// Lấy danh sách tên các Sheet trong file Excel (.xlsx)
        /// </summary>
        public static List<string> GetSheetNames(string filePath)
        {
            var sheetNames = new List<string>();
            if (!File.Exists(filePath)) return sheetNames;

            using (var workbook = new XLWorkbook(filePath))
            {
                foreach (var ws in workbook.Worksheets)
                {
                    sheetNames.Add(ws.Name);
                }
            }
            return sheetNames;
        }

        /// <summary>
        /// Đọc bảng dữ liệu cống từ Sheet được chỉ định
        /// </summary>
        public static List<CulvertRowData> ReadCulvertRows(string filePath, string sheetName)
        {
            var list = new List<CulvertRowData>();
            if (!File.Exists(filePath)) return list;

            using (var workbook = new XLWorkbook(filePath))
            {
                IXLWorksheet ws = null!;
                if (!string.IsNullOrEmpty(sheetName) && workbook.Worksheets.Contains(sheetName))
                {
                    ws = workbook.Worksheet(sheetName);
                }
                else
                {
                    ws = workbook.Worksheet(1);
                }

                // Tìm dòng tiêu đề (chứa "STT" hoặc "Lý trình" hoặc mặc định dòng 1)
                int headerRow = 1;
                for (int r = 1; r <= Math.Min(10, ws.LastRowUsed()?.RowNumber() ?? 1); r++)
                {
                    string cellVal = ws.Cell(r, 1).GetString().Trim().ToUpperInvariant();
                    string cellVal2 = ws.Cell(r, 2).GetString().Trim().ToUpperInvariant();
                    if (cellVal.Contains("STT") || cellVal2.Contains("LÝ TRÌNH") || cellVal2.Contains("LY TRINH"))
                    {
                        headerRow = r;
                        break;
                    }
                }

                int lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;

                // Map tiêu đề cột tự động (hỗ trợ cả file 19 cột cũ và 21 cột mới)
                var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 25;
                for (int c = 1; c <= lastCol; c++)
                {
                    string h = ws.Cell(headerRow, c).GetString().Trim().ToUpperInvariant();
                    if (!string.IsNullOrEmpty(h) && !colMap.ContainsKey(h))
                    {
                        colMap[h] = c;
                    }
                }

                int FindCol(string[] aliases, int fallbackCol)
                {
                    foreach (var alias in aliases)
                    {
                        string aUpper = alias.ToUpperInvariant();
                        foreach (var kvp in colMap)
                        {
                            if (kvp.Key == aUpper || kvp.Key.Contains(aUpper))
                                return kvp.Value;
                        }
                    }
                    return fallbackCol;
                }

                int colSTT = FindCol(new[] { "STT" }, 1);
                int colLyTrinh = FindCol(new[] { "LYTRINH", "LÝ TRÌNH", "LY TRINH" }, 2);
                int colLoaiCong = FindCol(new[] { "LOAICONG", "LOẠI CỐNG", "LOAI CONG" }, 3);
                int colSoCua = FindCol(new[] { "SOCUA", "SỐ CỬA", "SO CUA" }, 4);
                int colKhauDo = FindCol(new[] { "KHAUDO", "KHẨU ĐỘ", "KHAU DO" }, 5);
                int colX1 = FindCol(new[] { "X1" }, 6);
                int colY1 = FindCol(new[] { "Y1" }, 7);
                int colZ1 = FindCol(new[] { "Z1" }, 8);
                int colX2 = FindCol(new[] { "X2" }, 9);
                int colY2 = FindCol(new[] { "Y2" }, 10);
                int colZ2 = FindCol(new[] { "Z2" }, 11);
                int colChieuDai = FindCol(new[] { "CHIEUDAI", "CHIỀU DÀI", "CHIEU DAI", "L_CONG" }, 12);
                int colDoDoc = FindCol(new[] { "DODOC", "ĐỘ DỐC", "DO DOC", "I_CONG" }, 13);
                int colGocXoay = FindCol(new[] { "GOCXOAY", "GÓC XOAY", "GOC XOAY", "AZIMUTH" }, 14);
                int colSoHopNoi = FindCol(new[] { "SOHOPNOI", "SỐ HỘP NỐI", "SO HOP NOI", "SO_HO_THU" }, 15);
                int colKC_HN1 = FindCol(new[] { "KC_HN1", "DIST_HN1", "KC_HT1", "DIST_HT1" }, 16);
                int colKC_HN2 = FindCol(new[] { "KC_HN2", "DIST_HN2", "KC_HT2", "DIST_HT2" }, 17);
                int colB_HT1 = FindCol(new[] { "B_HT1", "B_HN1", "BERONG_HT1", "BERONG_HN1", "B HỐ THU 1", "B HO THU 1" }, colMap.ContainsKey("B_HT1") ? colMap["B_HT1"] : (colMap.ContainsKey("B_HN1") ? colMap["B_HN1"] : (lastCol >= 21 ? 18 : 0)));
                int colB_HT2 = FindCol(new[] { "B_HT2", "B_HN2", "BERONG_HT2", "BERONG_HN2", "B HỐ THU 2", "B HO THU 2" }, colMap.ContainsKey("B_HT2") ? colMap["B_HT2"] : (colMap.ContainsKey("B_HN2") ? colMap["B_HN2"] : (lastCol >= 21 ? 19 : 0)));
                int colL_Ngam = FindCol(new[] { "L_NGAM_SAN", "L_NGAM", "NGAM_SAN" }, lastCol >= 21 ? 20 : 18);
                int colKheHo = FindCol(new[] { "KHE_HO_HN", "KHE_HO", "KHEHO" }, lastCol >= 21 ? 21 : 19);

                for (int r = headerRow + 1; r <= lastRow; r++)
                {
                    var row = ws.Row(r);
                    if (row.IsEmpty()) continue;

                    // Kiểm tra nếu ô STT hoặc Lý trình có giá trị
                    string sttStr = row.Cell(colSTT).GetString().Trim();
                    string lyTrinhStr = row.Cell(colLyTrinh).GetString().Trim();
                    if (string.IsNullOrEmpty(sttStr) && string.IsNullOrEmpty(lyTrinhStr))
                        continue;

                    var item = new CulvertRowData();

                    // Cột A: STT
                    item.STT = ParseInt(row.Cell(colSTT), r - headerRow);

                    // Cột B: LyTrinh
                    item.LyTrinh = row.Cell(colLyTrinh).GetString().Trim();

                    // Cột C: LoaiCong (CONG_TRON / CONG_HOP)
                    string loai = row.Cell(colLoaiCong).GetString().Trim().ToUpperInvariant();
                    item.LoaiCong = loai.Contains("HOP") ? "CONG_HOP" : "CONG_TRON";

                    // Cột D: SoCua
                    item.SoCua = ParseInt(row.Cell(colSoCua), 1);

                    // Cột E: KhauDo
                    item.KhauDo = row.Cell(colKhauDo).GetString().Trim();

                    // Cột F -> H: X1, Y1, Z1 (Thượng lưu - VN2000)
                    item.X1 = ParseDouble(row.Cell(colX1), 0);
                    item.Y1 = ParseDouble(row.Cell(colY1), 0);
                    item.Z1 = ParseDouble(row.Cell(colZ1), 0);

                    // Cột I -> K: X2, Y2, Z2 (Hạ lưu - VN2000)
                    item.X2 = ParseDouble(row.Cell(colX2), 0);
                    item.Y2 = ParseDouble(row.Cell(colY2), 0);
                    item.Z2 = ParseDouble(row.Cell(colZ2), 0);

                    // Cột L: ChieuDai (Nếu chưa có thì tính từ tọa độ 2D)
                    double len2D = item.TinhChieuDai2D();
                    item.ChieuDai = ParseDouble(row.Cell(colChieuDai), len2D);

                    // Cột M: DoDoc (Nếu để trống thì tự động tính từ chênh cao và chiều dài)
                    double docTinhToan = item.TinhDoDocThucTe();
                    item.DoDoc = ParseDouble(row.Cell(colDoDoc), docTinhToan);

                    // Cột N: GocXoay (TỰ ĐỘNG TÍNH TOÁN 100% từ tọa độ P1 -> P2 nếu người dùng để trống hoặc nhập 0)
                    double gocNhap = ParseDouble(row.Cell(colGocXoay), 0);
                    item.GocXoay = (gocNhap > 0.001) ? gocNhap : item.TinhGocAzimuthDeg();

                    // Cột O: SoHopNoi
                    item.SoHopNoi = ParseInt(row.Cell(colSoHopNoi), 0);

                    // Cột P -> Q: KC_HN1, KC_HN2 (Khoảng cách hố thu 1, 2)
                    item.KC_HN1 = ParseDouble(row.Cell(colKC_HN1), 0);
                    item.KC_HN2 = ParseDouble(row.Cell(colKC_HN2), 0);

                    // Bề rộng hố thu 1 và hố thu 2 (B_HT1, B_HT2)
                    double b1 = (colB_HT1 > 0) ? ParseDouble(row.Cell(colB_HT1), 1.50) : 1.50;
                    double b2 = (colB_HT2 > 0) ? ParseDouble(row.Cell(colB_HT2), 1.50) : 1.50;
                    item.B_HT1 = (b1 > 0.1) ? b1 : 1.50;
                    item.B_HT2 = (b2 > 0.1) ? b2 : 1.50;

                    // L_Ngam_San, Khe_Ho_HN
                    item.L_Ngam_San = ParseDouble(row.Cell(colL_Ngam), 0.30);
                    item.Khe_Ho_HN = ParseDouble(row.Cell(colKheHo), 0.05);

                    // Mặc định khoảng cách tim cống đôi = 2.0m
                    item.KhoangCachTim = 2.0;

                    // Kiểm tra sơ bộ tính hợp lệ
                    if (item.X1 == 0 && item.Y1 == 0)
                    {
                        item.HasError = true;
                        item.StatusNote = "Thiếu tọa độ Thượng lưu (P1)";
                    }
                    else if (item.X2 == 0 && item.Y2 == 0)
                    {
                        item.HasError = true;
                        item.StatusNote = "Thiếu tọa độ Hạ lưu (P2)";
                    }
                    else
                    {
                        double diff = Math.Abs(len2D - item.ChieuDai);
                        if (item.ChieuDai > 0 && diff > 0.5)
                        {
                            item.StatusNote = $"Lệch dài thiết kế {diff:F2}m";
                        }

                        // So sánh kiểm tra chéo độ dốc thiết kế vs độ dốc tọa độ
                        double diffSlope = Math.Abs(item.DoDoc - docTinhToan);
                        if (diffSlope > 0.2)
                        {
                            item.StatusNote = $"Lệch độ dốc: TK {item.DoDoc:F2}% vs Tọa độ {docTinhToan:F2}%";
                        }
                    }

                    list.Add(item);
                }
            }

            return list;
        }

        /// <summary>
        /// Tạo file mẫu Excel chuẩn 21 cột với dữ liệu thực tế và tên cột B_HT1, B_HT2
        /// </summary>
        public static void CreateSampleExcelTemplate(string filePath)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("DuLieuCongNgang");

                // Headers chuẩn hóa (21 cột bao gồm B_HT1, B_HT2)
                string[] headers = new[]
                {
                    "STT", "LyTrinh", "LoaiCong", "SoCua", "KhauDo",
                    "X1", "Y1", "Z1", "X2", "Y2", "Z2",
                    "ChieuDai", "DoDoc", "GocXoay", "SoHopNoi",
                    "KC_HN1", "KC_HN2", "B_HT1", "B_HT2", "L_Ngam_San", "Khe_Ho_HN"
                };

                for (int col = 0; col < headers.Length; col++)
                {
                    var cell = ws.Cell(1, col + 1);
                    cell.Value = headers[col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontSize = 11;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#16325C");
                    cell.Style.Font.FontColor = XLColor.FromHtml("#FFFFFF");
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#38BDF8");
                }

                // Dòng mẫu 1: Cống tròn đôi 2D1500, TH2: 2 hộp nối (Km1+250.50)
                object[] row1 = new object[]
                {
                    1, "Km1+250.50", "CONG_TRON", 2, "D1500",
                    587234.120, 1194562.890, 265.140,
                    587248.560, 1194558.120, 264.840,
                    15.21, 2.00, 108.30, 2,
                    5.38, 4.20, 1.50, 1.50, 0.30, 0.05
                };

                // Dòng mẫu 2: Cống tròn đơn 1D1000, TH1: Không hộp nối (Km1+680.00)
                object[] row2 = new object[]
                {
                    2, "Km1+680.00", "CONG_TRON", 1, "D1000",
                    587420.350, 1194605.100, 266.500,
                    587432.800, 1194601.200, 266.250,
                    13.05, 1.92, 107.40, 0,
                    0.00, 0.00, 1.50, 1.50, 0.30, 0.05
                };

                // Dòng mẫu 3: Cống hộp đơn 2000x2000, TH1: Không hộp nối (Km2+100.20)
                object[] row3 = new object[]
                {
                    3, "Km2+100.20", "CONG_HOP", 1, "2000x2000",
                    587750.800, 1194710.450, 268.000,
                    587768.200, 1194704.900, 267.650,
                    18.25, 1.92, 107.70, 0,
                    0.00, 0.00, 1.50, 1.50, 0.30, 0.05
                };

                // Dòng mẫu 4: Cống hộp đôi 2500x2000, TH2: 2 hộp nối (Km2+550.00)
                object[] row4 = new object[]
                {
                    4, "Km2+550.00", "CONG_HOP", 2, "2500x2000",
                    588120.400, 1194830.150, 269.800,
                    588142.100, 1194823.300, 269.360,
                    22.75, 1.93, 107.50, 2,
                    6.50, 5.80, 1.60, 1.60, 0.35, 0.05
                };

                for (int c = 0; c < row1.Length; c++) ws.Cell(2, c + 1).Value = XLCellValue.FromObject(row1[c]);
                for (int c = 0; c < row2.Length; c++) ws.Cell(3, c + 1).Value = XLCellValue.FromObject(row2[c]);
                for (int c = 0; c < row3.Length; c++) ws.Cell(4, c + 1).Value = XLCellValue.FromObject(row3[c]);
                for (int c = 0; c < row4.Length; c++) ws.Cell(5, c + 1).Value = XLCellValue.FromObject(row4[c]);

                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents(15.0, 30.0);
                workbook.SaveAs(filePath);
            }
        }

        private static int ParseInt(IXLCell cell, int defaultVal)
        {
            if (cell.TryGetValue<int>(out int val)) return val;
            string s = cell.GetString().Trim();
            if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out int parsed)) return parsed;
            return defaultVal;
        }

        private static double ParseDouble(IXLCell cell, double defaultVal)
        {
            if (cell.TryGetValue<double>(out double val)) return val;
            string s = cell.GetString().Trim().Replace(',', '.');
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)) return parsed;
            return defaultVal;
        }
    }
}
