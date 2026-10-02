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
        /// <summary>
        /// Mở XLWorkbook an toàn qua MemoryStream với FileShare.ReadWrite & retry, không bị khóa file khi Excel đang mở
        /// </summary>
        public static XLWorkbook OpenWorkbookSafe(string filePath)
        {
            Exception? lastEx = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var ms = new MemoryStream();
                    fs.CopyTo(ms);
                    ms.Position = 0;
                    return new XLWorkbook(ms);
                }
                catch (IOException ex)
                {
                    lastEx = ex;
                    System.Threading.Thread.Sleep(80);
                }
            }
            if (lastEx != null) throw lastEx;
            throw new IOException($"Không thể mở file: {filePath}");
        }

        public static List<string> GetSheetNames(string filePath)
        {
            var sheetNames = new List<string>();
            if (!File.Exists(filePath)) return sheetNames;

            if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                sheetNames.Add(Path.GetFileNameWithoutExtension(filePath));
                return sheetNames;
            }

            using (var workbook = OpenWorkbookSafe(filePath))
            {
                foreach (var ws in workbook.Worksheets)
                {
                    sheetNames.Add(ws.Name);
                }
            }
            return sheetNames;
        }

        /// <summary>
        /// Đọc bảng dữ liệu cống từ Sheet được chỉ định (hoặc từ file CSV) - Chống khóa file khi Excel đang mở
        /// </summary>
        public static List<CulvertRowData> ReadCulvertRows(string filePath, string sheetName)
        {
            var list = new List<CulvertRowData>();
            if (!File.Exists(filePath)) return list;

            if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return ReadCsvRows(filePath);
            }

            using (var workbook = OpenWorkbookSafe(filePath))
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
                int colCauKien = FindCol(new[] { "CAUKIEN", "CẤU KIỆN", "CAU KIEN", "PHUONG PHAP", "THI CONG", "DUC_SAN" }, -1);
                int offsetCK = (colCauKien > 0) ? 1 : 0;
                int colSoCua = FindCol(new[] { "SOCUA", "SỐ CỬA", "SO CUA" }, 4 + offsetCK);
                int colKhauDo = FindCol(new[] { "KHAUDO", "KHẨU ĐỘ", "KHAU DO" }, 5 + offsetCK);
                int colX1 = FindCol(new[] { "X1" }, 6 + offsetCK);
                int colY1 = FindCol(new[] { "Y1" }, 7 + offsetCK);
                int colZ1 = FindCol(new[] { "Z1" }, 8 + offsetCK);
                int colX2 = FindCol(new[] { "X2" }, 9 + offsetCK);
                int colY2 = FindCol(new[] { "Y2" }, 10 + offsetCK);
                int colZ2 = FindCol(new[] { "Z2" }, 11 + offsetCK);
                int colChieuDai = FindCol(new[] { "CHIEUDAI", "CHIỀU DÀI", "CHIEU DAI", "L_CONG" }, 12 + offsetCK);
                int colDoDoc = FindCol(new[] { "DODOC", "ĐỘ DỐC", "DO DOC", "I_CONG" }, 13 + offsetCK);
                int colGocXoay = FindCol(new[] { "GOCXOAY", "GÓC XOAY", "GOC XOAY", "AZIMUTH" }, 14 + offsetCK);
                int colSoHopNoi = FindCol(new[] { "SOHOPNOI", "SỐ HỘP NỐI", "SO HOP NOI", "SO_HO_THU" }, 15 + offsetCK);
                int colKC_HN1 = FindCol(new[] { "KC_HN1", "DIST_HN1", "KC_HT1", "DIST_HT1" }, 16 + offsetCK);
                int colKC_HN2 = FindCol(new[] { "KC_HN2", "DIST_HN2", "KC_HT2", "DIST_HT2" }, 17 + offsetCK);
                int colB_HT1 = FindCol(new[] { "B_HT1", "B_HN1", "BERONG_HT1", "BERONG_HN1", "B HỐ THU 1", "B HO THU 1" }, colMap.ContainsKey("B_HT1") ? colMap["B_HT1"] : (18 + offsetCK));
                int colB_HT2 = FindCol(new[] { "B_HT2", "B_HN2", "BERONG_HT2", "BERONG_HN2", "B HỐ THU 2", "B HO THU 2" }, colMap.ContainsKey("B_HT2") ? colMap["B_HT2"] : (19 + offsetCK));
                int colL_Ngam = FindCol(new[] { "L_NGAM_SAN", "L_NGAM", "NGAM_SAN" }, 20 + offsetCK);
                int colKheHo = FindCol(new[] { "KHE_HO_HN", "KHE_HO", "KHEHO" }, 21 + offsetCK);
                int colGhiChu = FindCol(new[] { "GHICHU", "GHI CHÚ", "GHI CHU", "LOAI RAI", "LOAIRAI", "PHAN LOAI", "CHUNGLOAI" }, 22 + offsetCK);

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

                    // Cột C: LoaiCong (Cống hộp / Cống tròn / Cống kỹ thuật)
                    string loaiRaw = row.Cell(colLoaiCong).GetString().Trim();
                    if (loaiRaw.ToUpperInvariant().Contains("KỸ THUẬT") || loaiRaw.ToUpperInvariant().Contains("KY THUAT"))
                        item.LoaiCong = "Cống kỹ thuật";
                    else if (loaiRaw.ToUpperInvariant().Contains("TRÒN") || loaiRaw.ToUpperInvariant().Contains("TRON") || loaiRaw.ToUpperInvariant().Contains("CT"))
                        item.LoaiCong = "Cống tròn";
                    else
                        item.LoaiCong = "Cống hộp";

                    // Cột D: CauKien (Đúc sẵn / Đổ tại chỗ)
                    if (colCauKien > 0)
                    {
                        string ckRaw = row.Cell(colCauKien).GetString().Trim();
                        item.CauKien = (ckRaw.ToUpperInvariant().Contains("ĐỔ") || ckRaw.ToUpperInvariant().Contains("DO")) ? "Đổ tại chỗ" : "Đúc sẵn";
                    }
                    else
                    {
                        item.CauKien = "Đúc sẵn";
                    }

                    // Cột SoCua
                    item.SoCua = ParseInt(row.Cell(colSoCua), 1);

                    // Cột KhauDo
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

                    // Cột GhiChu (Quyết định loại rải cống tự động)
                    if (colGhiChu > 0)
                    {
                        item.GhiChu = row.Cell(colGhiChu).GetString().Trim();
                    }
                    if (string.IsNullOrEmpty(item.GhiChu))
                    {
                        item.GhiChu = item.DetermineCulvertType();
                    }

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
        /// Tạo file mẫu Excel chuẩn 23 cột với dữ liệu thực tế, Drop-down validation và cột GhiChu tự động
        /// </summary>
        public static void CreateSampleExcelTemplate(string filePath)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("DuLieuCongNgang");

                // Headers chuẩn hóa (23 cột bao gồm CauKien và GhiChu)
                string[] headers = new[]
                {
                    "STT", "LyTrinh", "LoaiCong", "CauKien", "SoCua", "KhauDo",
                    "X1", "Y1", "Z1", "X2", "Y2", "Z2",
                    "ChieuDai", "DoDoc", "GocXoay", "SoHopNoi",
                    "KC_HN1", "KC_HN2", "B_HT1", "B_HT2", "L_Ngam_San", "Khe_Ho_HN", "GhiChu"
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

                // Dòng mẫu 1: Cống tròn đôi 2D1500 đúc sẵn (Km1+250.50)
                object[] row1 = new object[]
                {
                    1, "Km1+250.50", "Cống tròn", "Đúc sẵn", 2, "D1500",
                    587234.120, 1194562.890, 265.140,
                    587248.560, 1194558.120, 264.840,
                    15.21, 2.00, 108.30, 2,
                    5.38, 4.20, 1.50, 1.50, 0.30, 0.05, "Cống tròn đôi đúc sẵn"
                };

                // Dòng mẫu 2: Cống tròn đơn 1D1000 đúc sẵn (Km1+680.00)
                object[] row2 = new object[]
                {
                    2, "Km1+680.00", "Cống tròn", "Đúc sẵn", 1, "D1000",
                    587420.350, 1194605.100, 266.500,
                    587432.800, 1194601.200, 266.250,
                    13.05, 1.92, 107.40, 0,
                    0.00, 0.00, 1.50, 1.50, 0.30, 0.05, "Cống tròn đúc sẵn"
                };

                // Dòng mẫu 3: Cống hộp đơn 1.5x1.5 đúc sẵn (Km2+100.20)
                object[] row3 = new object[]
                {
                    3, "Km2+100.20", "Cống hộp", "Đúc sẵn", 1, "1.5x1.5",
                    587750.800, 1194710.450, 268.000,
                    587768.200, 1194704.900, 267.650,
                    18.25, 1.92, 107.70, 0,
                    0.00, 0.00, 1.50, 1.50, 0.30, 0.05, "Cống hộp đúc sẵn"
                };

                // Dòng mẫu 4: Cống hộp đôi 2.5x2.0 đổ tại chỗ (Km2+550.00)
                object[] row4 = new object[]
                {
                    4, "Km2+550.00", "Cống hộp", "Đổ tại chỗ", 2, "2.5x2.0",
                    588120.400, 1194830.150, 269.800,
                    588142.100, 1194823.300, 269.360,
                    22.75, 1.93, 107.50, 2,
                    6.50, 5.80, 1.60, 1.60, 0.35, 0.05, "Cống hộp đổ tại chỗ"
                };

                for (int c = 0; c < row1.Length; c++) ws.Cell(2, c + 1).Value = XLCellValue.FromObject(row1[c]);
                for (int c = 0; c < row2.Length; c++) ws.Cell(3, c + 1).Value = XLCellValue.FromObject(row2[c]);
                for (int c = 0; c < row3.Length; c++) ws.Cell(4, c + 1).Value = XLCellValue.FromObject(row3[c]);
                for (int c = 0; c < row4.Length; c++) ws.Cell(5, c + 1).Value = XLCellValue.FromObject(row4[c]);

                // Định dạng hiển thị chuẩn không có dấu phẩy hàng nghìn (đúng chuẩn CAD/Revit 0.000)
                ws.Range("G2:L100").Style.NumberFormat.Format = "0.000";
                ws.Range("M2:O100").Style.NumberFormat.Format = "0.00";
                ws.Range("Q2:V100").Style.NumberFormat.Format = "0.00";
                ws.Range("A2:A100").Style.NumberFormat.Format = "0";
                ws.Range("E2:E100").Style.NumberFormat.Format = "0";
                ws.Range("P2:P100").Style.NumberFormat.Format = "0";

                // Thêm Data Validation Drop-down list
                try
                {
                    // Cột C (LoaiCong): Cống hộp, Cống tròn, Cống kỹ thuật
                    var valLoai = ws.Range("C2:C500").CreateDataValidation();
                    valLoai.List("\"Cống hộp, Cống tròn, Cống kỹ thuật\"", true);

                    // Cột D (CauKien): Đúc sẵn, Đổ tại chỗ
                    var valCauKien = ws.Range("D2:D500").CreateDataValidation();
                    valCauKien.List("\"Đúc sẵn, Đổ tại chỗ\"", true);

                    // Cột W (GhiChu): Danh sách chủng loại rải tự động
                    var valGhiChu = ws.Range("W2:W500").CreateDataValidation();
                    valGhiChu.List("\"Cống hộp đúc sẵn, Cống tròn đúc sẵn, Cống tròn đôi đúc sẵn, Cống hộp đổ tại chỗ, Cống kỹ thuật\"", true);
                }
                catch
                {
                    // Tránh crash nếu ClosedXML version không hỗ trợ DataValidation
                }

                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents(15.0, 30.0);
                workbook.SaveAs(filePath);
            }
        }

        public static List<CulvertRowData> ReadCsvRows(string filePath)
        {
            var list = new List<CulvertRowData>();
            if (!File.Exists(filePath)) return list;

            List<string> lines = new List<string>();
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, System.Text.Encoding.UTF8))
            {
                string? l;
                while ((l = sr.ReadLine()) != null)
                {
                    lines.Add(l);
                }
            }

            if (lines.Count <= 1) return list;

            char delimiter = lines[0].Contains(';') ? ';' : ',';

            int headerLineIdx = 0;
            for (int i = 0; i < Math.Min(10, lines.Count); i++)
            {
                string upper = lines[i].ToUpperInvariant();
                if (upper.Contains("STT") || upper.Contains("LYTRINH") || upper.Contains("LÝ TRÌNH") || upper.Contains("X1"))
                {
                    headerLineIdx = i;
                    break;
                }
            }

            var headerCols = lines[headerLineIdx].Split(delimiter);
            var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headerCols.Length; c++)
            {
                string hName = headerCols[c].Trim().Trim('"');
                if (!string.IsNullOrEmpty(hName) && !colMap.ContainsKey(hName))
                {
                    colMap[hName] = c;
                }
            }

            int FindCol(string[] aliases, int defaultIdx)
            {
                foreach (var a in aliases)
                {
                    if (colMap.TryGetValue(a, out int idx)) return idx;
                }
                return (defaultIdx >= 0 && defaultIdx < headerCols.Length) ? defaultIdx : -1;
            }

            int colSTT = FindCol(new[] { "STT" }, 0);
            int colLyTrinh = FindCol(new[] { "LYTRINH", "LÝ TRÌNH", "LY TRINH", "LÝ_TRÌNH" }, 1);
            int colLoaiCong = FindCol(new[] { "LOAICONG", "LOẠI CỐNG", "LOAI CONG", "LOAI_CONG" }, 2);
            int colCauKien = FindCol(new[] { "CAUKIEN", "CẤU KIỆN", "CAU KIEN", "PHUONG PHAP", "THI CONG", "DUC_SAN" }, -1);
            int offsetCK = (colCauKien >= 0) ? 1 : 0;
            int colSoCua = FindCol(new[] { "SOCUA", "SỐ CỬA", "SO CUA", "SO_CUA" }, 3 + offsetCK);
            int colKhauDo = FindCol(new[] { "KHAUDO", "KHẨU ĐỘ", "KHAU DO", "KHAU_DO" }, 4 + offsetCK);
            int colX1 = FindCol(new[] { "X1" }, 5 + offsetCK);
            int colY1 = FindCol(new[] { "Y1" }, 6 + offsetCK);
            int colZ1 = FindCol(new[] { "Z1" }, 7 + offsetCK);
            int colX2 = FindCol(new[] { "X2" }, 8 + offsetCK);
            int colY2 = FindCol(new[] { "Y2" }, 9 + offsetCK);
            int colZ2 = FindCol(new[] { "Z2" }, 10 + offsetCK);
            int colChieuDai = FindCol(new[] { "CHIEUDAI", "CHIỀU DÀI", "CHIEU DAI", "L_CONG" }, 11 + offsetCK);
            int colDoDoc = FindCol(new[] { "DODOC", "ĐỘ DỐC", "DO DOC", "I_CONG" }, 12 + offsetCK);
            int colGocXoay = FindCol(new[] { "GOCXOAY", "GÓC XOAY", "GOC XOAY", "AZIMUTH" }, 13 + offsetCK);
            int colSoHopNoi = FindCol(new[] { "SOHOPNOI", "SỐ HỘP NỐI", "SO HOP NOI", "SO_HO_THU" }, 14 + offsetCK);
            int colKC_HN1 = FindCol(new[] { "KC_HN1", "DIST_HN1", "KC_HT1", "DIST_HT1" }, 15 + offsetCK);
            int colKC_HN2 = FindCol(new[] { "KC_HN2", "DIST_HN2", "KC_HT2", "DIST_HT2" }, 16 + offsetCK);
            int colB_HT1 = FindCol(new[] { "B_HT1", "B_HN1", "BERONG_HT1", "BERONG_HN1", "B HỐ THU 1" }, 17 + offsetCK);
            int colB_HT2 = FindCol(new[] { "B_HT2", "B_HN2", "BERONG_HT2", "BERONG_HN2", "B HỐ THU 2" }, 18 + offsetCK);
            int colL_Ngam = FindCol(new[] { "L_NGAM_SAN", "L_NGAM", "NGAM_SAN" }, 19 + offsetCK);
            int colKheHo = FindCol(new[] { "KHE_HO_HN", "KHE_HO", "KHEHO" }, 20 + offsetCK);
            int colGhiChu = FindCol(new[] { "GHICHU", "GHI CHÚ", "GHI CHU", "LOAI RAI", "LOAIRAI" }, 21 + offsetCK);

            for (int r = headerLineIdx + 1; r < lines.Count; r++)
            {
                string line = lines[r].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cells = line.Split(delimiter);
                string GetCell(int idx) => (idx >= 0 && idx < cells.Length) ? cells[idx].Trim().Trim('"') : string.Empty;

                string sttStr = GetCell(colSTT);
                string lyTrinhStr = GetCell(colLyTrinh);
                if (string.IsNullOrEmpty(sttStr) && string.IsNullOrEmpty(lyTrinhStr)) continue;

                var item = new CulvertRowData();
                item.STT = int.TryParse(sttStr, out int sttVal) ? sttVal : (r - headerLineIdx);
                item.LyTrinh = lyTrinhStr;

                string loaiRaw = GetCell(colLoaiCong);
                if (loaiRaw.ToUpperInvariant().Contains("KỸ THUẬT") || loaiRaw.ToUpperInvariant().Contains("KY THUAT"))
                    item.LoaiCong = "Cống kỹ thuật";
                else if (loaiRaw.ToUpperInvariant().Contains("TRÒN") || loaiRaw.ToUpperInvariant().Contains("TRON") || loaiRaw.ToUpperInvariant().Contains("CT"))
                    item.LoaiCong = "Cống tròn";
                else
                    item.LoaiCong = "Cống hộp";

                if (colCauKien >= 0)
                {
                    string ckRaw = GetCell(colCauKien);
                    item.CauKien = (ckRaw.ToUpperInvariant().Contains("ĐỔ") || ckRaw.ToUpperInvariant().Contains("DO")) ? "Đổ tại chỗ" : "Đúc sẵn";
                }
                else
                {
                    item.CauKien = "Đúc sẵn";
                }

                item.SoCua = int.TryParse(GetCell(colSoCua), out int sc) ? sc : 1;
                item.KhauDo = GetCell(colKhauDo);
                if (string.IsNullOrEmpty(item.KhauDo)) item.KhauDo = item.LoaiCong.Contains("hộp") ? "1.5x1.5" : "D1000";

                item.X1 = ParseDoubleString(GetCell(colX1), 0);
                item.Y1 = ParseDoubleString(GetCell(colY1), 0);
                item.Z1 = ParseDoubleString(GetCell(colZ1), 0);

                item.X2 = ParseDoubleString(GetCell(colX2), 0);
                item.Y2 = ParseDoubleString(GetCell(colY2), 0);
                item.Z2 = ParseDoubleString(GetCell(colZ2), 0);

                double len2D = item.TinhChieuDai2D();
                item.ChieuDai = ParseDoubleString(GetCell(colChieuDai), len2D);

                double docTinhToan = item.TinhDoDocThucTe();
                item.DoDoc = ParseDoubleString(GetCell(colDoDoc), docTinhToan);

                double gocNhap = ParseDoubleString(GetCell(colGocXoay), 0);
                item.GocXoay = (gocNhap > 0.001) ? gocNhap : item.TinhGocAzimuthDeg();

                item.SoHopNoi = int.TryParse(GetCell(colSoHopNoi), out int shn) ? shn : 0;
                item.KC_HN1 = ParseDoubleString(GetCell(colKC_HN1), 0);
                item.KC_HN2 = ParseDoubleString(GetCell(colKC_HN2), 0);

                item.B_HT1 = ParseDoubleString(GetCell(colB_HT1), 1.50);
                item.B_HT2 = ParseDoubleString(GetCell(colB_HT2), 1.50);
                if (item.B_HT1 <= 0.1) item.B_HT1 = 1.50;
                if (item.B_HT2 <= 0.1) item.B_HT2 = 1.50;

                item.L_Ngam_San = ParseDoubleString(GetCell(colL_Ngam), 0.30);
                item.Khe_Ho_HN = ParseDoubleString(GetCell(colKheHo), 0.05);

                if (colGhiChu >= 0)
                {
                    item.GhiChu = GetCell(colGhiChu);
                }
                if (string.IsNullOrEmpty(item.GhiChu))
                {
                    item.GhiChu = item.DetermineCulvertType();
                }

                item.KhoangCachTim = 2.0;
                list.Add(item);
            }

            return list;
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
            return ParseDoubleString(cell.GetString(), defaultVal);
        }

        public static double ParseDoubleString(string? text, double defaultVal)
        {
            if (string.IsNullOrWhiteSpace(text)) return defaultVal;
            string s = text.Trim();

            // Xử lý linh hoạt cả trường hợp có dấu phẩy phân cách hàng nghìn (vd: 580,860.018)
            // hoặc dấu phẩy kiểu Việt Nam (vd: 580860,018)
            if (s.Contains(",") && s.Contains("."))
            {
                s = s.Replace(",", ""); // Loại bỏ dấu phẩy phân cách hàng nghìn -> "580860.018"
            }
            else if (s.Contains(",") && !s.Contains("."))
            {
                s = s.Replace(',', '.'); // Dấu phẩy là dấu thập phân -> "580860.018"
            }

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
            {
                return parsed;
            }
            return defaultVal;
        }
    }
}
