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
                    // Pass 1: So khớp tuyệt đối (Exact match) trước
                    foreach (var alias in aliases)
                    {
                        string aUpper = alias.ToUpperInvariant();
                        foreach (var kvp in colMap)
                        {
                            if (kvp.Key == aUpper)
                                return kvp.Value;
                        }
                    }
                    // Pass 2: So khớp chứa (chỉ dành cho alias từ 3 ký tự trở lên để tránh nhầm "S" với "STT", "SOCUA")
                    foreach (var alias in aliases)
                    {
                        if (alias.Length < 3) continue;
                        string aUpper = alias.ToUpperInvariant();
                        foreach (var kvp in colMap)
                        {
                            if (kvp.Key.Contains(aUpper))
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
                int colKC_Tim = FindCol(new[] { "KC_TIM", "KCTIM", "KC TIM", "KHOANGCACHTIM", "KHOẢNG CÁCH TIM", "KHOANG CACH TIM", "KC_TIM_M", "KC TIM M", "S" }, -1);
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
                int colL_SGC_TL = FindCol(new[] { "L_SGC_TL", "L SGC TL", "LSGC_TL", "L_SANGIACO_TL", "L SÂN GIA CỐ TL", "L SAN GIA CO TL", "L_SGC_THUONGLUU" }, -1);
                int colL_SGC_HL = FindCol(new[] { "L_SGC_HL", "L SGC HL", "LSGC_HL", "L_SANGIACO_HL", "L SÂN GIA CỐ HL", "L SAN GIA CO HL", "L_SGC_HALUU" }, -1);
                int colZ_SGC_TL = FindCol(new[] { "Z_SGC_TL", "Z SGC TL", "ZSGC_TL", "CD_SGC_TL", "CD SGC TL", "CAO DO SGC TL", "CAODO_SGC_TL", "Z2_SGC_TL", "Z_SGC_TL2", "CD_DIEM2_SGC_TL" }, -1);
                int colZ_SGC_HL = FindCol(new[] { "Z_SGC_HL", "Z SGC HL", "ZSGC_HL", "CD_SGC_HL", "CD SGC HL", "CAO DO SGC HL", "CAODO_SGC_HL", "Z2_SGC_HL", "Z_SGC_HL2", "CD_DIEM2_SGC_HL" }, -1);
                int colChuoiDot = FindCol(new[] { "CHUOIDOT", "CHUỖI ĐỐT", "CHUOIDOT", "QUYCACHDOT", "QUY CÁCH ĐỐT", "DOT_TUY_CHINH", "DOT_TY_CHINH", "QUY CACH DOT" }, -1);
                int colGhiChu = FindCol(new[] { "GHICHU", "GHI CHÚ", "GHI CHU", "LOAI RAI", "LOAIRAI", "PHAN LOAI", "CHUNGLOAI" }, 22 + offsetCK);

                for (int r = headerRow + 1; r <= lastRow; r++)
                {
                    var row = ws.Row(r);
                    if (row.IsEmpty()) continue;

                    // Kiểm tra nếu ô STT hoặc Lý trình có giá trị
                    string sttStr = GetCellStringSafe(row.Cell(colSTT));
                    string lyTrinhStr = GetCellStringSafe(row.Cell(colLyTrinh));
                    if (string.IsNullOrEmpty(sttStr) && string.IsNullOrEmpty(lyTrinhStr))
                        continue;

                    var item = new CulvertRowData();

                    // Cột A: STT
                    item.STT = ParseInt(row.Cell(colSTT), r - headerRow);

                    // Cột B: LyTrinh
                    item.LyTrinh = lyTrinhStr;

                    // Cột C: LoaiCong (Cống hộp / Cống tròn / Cống kỹ thuật)
                    string loaiRaw = GetCellStringSafe(row.Cell(colLoaiCong));
                    bool isSheetCkt = (!string.IsNullOrEmpty(sheetName) && (sheetName.ToUpperInvariant().Contains("KỸ THUẬT") || sheetName.ToUpperInvariant().Contains("KY THUAT") || sheetName.ToUpperInvariant().Contains("CKT")));
                    if (isSheetCkt || loaiRaw.ToUpperInvariant().Contains("KỸ THUẬT") || loaiRaw.ToUpperInvariant().Contains("KY THUAT") || loaiRaw.ToUpperInvariant().Contains("CKT"))
                        item.LoaiCong = "Cống kỹ thuật";
                    else if (loaiRaw.ToUpperInvariant().Contains("TRÒN") || loaiRaw.ToUpperInvariant().Contains("TRON") || loaiRaw.ToUpperInvariant().Contains("CT"))
                        item.LoaiCong = "Cống tròn";
                    else
                        item.LoaiCong = "Cống hộp";

                    // Cột D: CauKien (Đúc sẵn / Đổ tại chỗ)
                    if (colCauKien > 0)
                    {
                        string ckRaw = GetCellStringSafe(row.Cell(colCauKien));
                        item.CauKien = (ckRaw.ToUpperInvariant().Contains("ĐỔ") || ckRaw.ToUpperInvariant().Contains("DO")) ? "Đổ tại chỗ" : "Đúc sẵn";
                    }
                    else
                    {
                        item.CauKien = "Đúc sẵn";
                    }

                    // Cột SoCua
                    item.SoCua = ParseInt(row.Cell(colSoCua), 1);

                    // Cột KhauDo
                    item.KhauDo = GetCellStringSafe(row.Cell(colKhauDo));

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

                    // Bề rộng hố thu 1 và hố thu 2 (B_HT1, B_HT2) - Giữ nguyên 100% giá trị Excel kể cả 0.0
                    double b1 = (colB_HT1 > 0) ? ParseDouble(row.Cell(colB_HT1), 0.0) : 0.0;
                    double b2 = (colB_HT2 > 0) ? ParseDouble(row.Cell(colB_HT2), 0.0) : 0.0;
                    item.B_HT1 = b1;
                    item.B_HT2 = b2;

                    // L_Ngam_San, Khe_Ho_HN
                    item.L_Ngam_San = (colL_Ngam > 0) ? ParseDouble(row.Cell(colL_Ngam), 0.0) : 0.0;
                    item.Khe_Ho_HN = (colKheHo > 0) ? ParseDouble(row.Cell(colKheHo), 0.0) : 0.0;

                    // Chiều dài Sân gia cố Thượng lưu & Hạ lưu (m) - Giữ nguyên chính xác từ Excel
                    if (colL_SGC_TL > 0)
                    {
                        item.L_SGC_TL = ParseDouble(row.Cell(colL_SGC_TL), 0.0);
                    }
                    if (colL_SGC_HL > 0)
                    {
                        item.L_SGC_HL = ParseDouble(row.Cell(colL_SGC_HL), 0.0);
                    }

                    // Cao độ Điểm 2 Sân gia cố Thượng lưu & Hạ lưu (m)
                    if (colZ_SGC_TL > 0)
                    {
                        item.Z_SGC_TL = ParseDouble(row.Cell(colZ_SGC_TL), 0.0);
                    }
                    if (colZ_SGC_HL > 0)
                    {
                        item.Z_SGC_HL = ParseDouble(row.Cell(colZ_SGC_HL), 0.0);
                    }

                    // Cột GhiChu (Quyết định loại rải cống tự động)
                    if (colGhiChu > 0)
                    {
                        item.GhiChu = GetCellStringSafe(row.Cell(colGhiChu));
                    }
                    if (string.IsNullOrEmpty(item.GhiChu))
                    {
                        item.GhiChu = item.DetermineCulvertType();
                    }

                    // Cột Chuỗi đốt tùy chỉnh (ví dụ: 6@2.5, 2@3.0)
                    if (colChuoiDot > 0)
                    {
                        item.CustomPattern = GetCellStringSafe(row.Cell(colChuoiDot));
                    }
                    if (string.IsNullOrEmpty(item.CustomPattern) && !string.IsNullOrEmpty(item.GhiChu) && item.GhiChu.Contains("@"))
                    {
                        item.CustomPattern = item.GhiChu;
                    }

                    // Đối với Cống kỹ thuật: Mặc định chỉ có 1 hộp nối (SoHopNoi = 1), và không dùng các thông số cống ngang
                    if (item.LoaiCong == "Cống kỹ thuật" || isSheetCkt)
                    {
                        if (item.SoHopNoi <= 0) item.SoHopNoi = 1;
                        item.KC_HN2 = 0;
                        item.B_HT2 = 0;
                        item.L_Ngam_San = 0;
                        item.Khe_Ho_HN = 0;
                        item.L_SGC_TL = 0;
                        item.L_SGC_HL = 0;
                        if (string.IsNullOrEmpty(item.GhiChu) || item.GhiChu.Contains("HỘP"))
                        {
                            item.GhiChu = "Cống kỹ thuật";
                        }
                    }

                    // Khoảng cách tim cống: Đọc chuẩn từ Excel nếu có cột
                    if (colKC_Tim > 0)
                    {
                        double kc = ParseDouble(row.Cell(colKC_Tim), 0.0);
                        item.KhoangCachTim = kc;
                    }
                    else
                    {
                        // Nếu Excel không có cột KC_Tim:
                        // Cống tròn đôi (SoCua >= 2): mặc định 2.12m theo chuẩn thiết kế CAD
                        // Cống tròn đơn (SoCua == 1): 0m
                        // Cống hộp đôi: 2.0m
                        if (item.LoaiCong == "Cống tròn")
                        {
                            item.KhoangCachTim = (item.SoCua >= 2) ? 2.12 : 0.0;
                        }
                        else
                        {
                            item.KhoangCachTim = (item.SoCua >= 2) ? 2.0 : 0.0;
                        }
                    }

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

                // Headers chuẩn hóa (27 cột bao gồm CauKien, L_SGC_TL, L_SGC_HL, Z_SGC_TL, Z_SGC_HL và GhiChu)
                string[] headers = new[]
                {
                    "STT", "LyTrinh", "LoaiCong", "CauKien", "SoCua", "KhauDo",
                    "X1", "Y1", "Z1", "X2", "Y2", "Z2",
                    "ChieuDai", "DoDoc", "GocXoay", "SoHopNoi",
                    "KC_HN1", "KC_HN2", "B_HT1", "B_HT2", "L_Ngam_San", "Khe_Ho_HN", "L_SGC_TL", "L_SGC_HL", "Z_SGC_TL", "Z_SGC_HL", "GhiChu"
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
                    5.38, 4.20, 1.50, 1.50, 0.30, 0.05, 3.00, 3.00, 264.970, 264.670, "Cống tròn đôi đúc sẵn"
                };

                // Dòng mẫu 2: Cống tròn đơn 1D1000 đúc sẵn (Km1+680.00)
                object[] row2 = new object[]
                {
                    2, "Km1+680.00", "Cống tròn", "Đúc sẵn", 1, "D1000",
                    587420.350, 1194605.100, 266.500,
                    587432.800, 1194601.200, 266.250,
                    13.05, 1.92, 107.40, 0,
                    0.00, 0.00, 1.50, 1.50, 0.30, 0.05, 3.00, 3.00, 266.330, 266.080, "Cống tròn đúc sẵn"
                };

                // Dòng mẫu 3: Cống hộp đơn 1.5x1.5 đúc sẵn (Km2+100.20)
                object[] row3 = new object[]
                {
                    3, "Km2+100.20", "Cống hộp", "Đúc sẵn", 1, "1.5x1.5",
                    587750.800, 1194710.450, 268.000,
                    587768.200, 1194704.900, 267.650,
                    18.25, 1.92, 107.70, 0,
                    0.00, 0.00, 1.50, 1.50, 0.30, 0.05, 3.00, 3.00, 267.830, 267.480, "Cống hộp đúc sẵn"
                };

                // Dòng mẫu 4: Cống hộp đôi 2.5x2.0 đổ tại chỗ (Km2+550.00)
                object[] row4 = new object[]
                {
                    4, "Km2+550.00", "Cống hộp", "Đổ tại chỗ", 2, "2.5x2.0",
                    588120.400, 1194830.150, 269.800,
                    588142.100, 1194823.300, 269.360,
                    22.75, 1.93, 107.50, 2,
                    6.50, 5.80, 1.60, 1.60, 0.35, 0.05, 3.00, 3.00, 269.630, 269.190, "Cống hộp đổ tại chỗ"
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

                // ========================================================
                // SHEET 2: CỐNG KỸ THUẬT (19 CỘT - BỎ 6 CỘT KHÔNG DÙNG THEO HÌNH 2)
                // ========================================================
                var wsCkt = workbook.Worksheets.Add("CongKyThuat");
                string[] headersCkt =
                {
                    "STT", "LyTrinh", "LoaiCong", "CauKien", "SoCua", "KhauDo",
                    "X1", "Y1", "Z1",
                    "X2", "Y2", "Z2",
                    "ChieuDai", "DoDoc", "GocXoay", "SoHopNoi",
                    "KC_HN1", "B_HT1", "GhiChu"
                };

                for (int i = 0; i < headersCkt.Length; i++)
                {
                    var cell = wsCkt.Cell(1, i + 1);
                    cell.Value = headersCkt[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#164E63"); // Cyan Dark
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                object[] cktRow1 =
                {
                    1, "Km0+100.00", "Cống kỹ thuật", "Đúc sẵn", 1, "1.5x1.5",
                    580838.203, 1477280.506, 2.070,
                    580860.889, 1477323.339, 1.870,
                    48.47, 0.45, 62.09, 1,
                    16.15, 1.40, "Cống kỹ thuật"
                };
                for (int c = 0; c < cktRow1.Length; c++) wsCkt.Cell(2, c + 1).Value = XLCellValue.FromObject(cktRow1[c]);

                wsCkt.Range("G2:L100").Style.NumberFormat.Format = "0.000";
                wsCkt.Range("M2:O100").Style.NumberFormat.Format = "0.00";
                wsCkt.Range("Q2:R100").Style.NumberFormat.Format = "0.00";
                wsCkt.Range("A2:A100").Style.NumberFormat.Format = "0";
                wsCkt.Range("E2:E100").Style.NumberFormat.Format = "0";
                wsCkt.Range("P2:P100").Style.NumberFormat.Format = "0";
                wsCkt.SheetView.FreezeRows(1);
                wsCkt.Columns().AdjustToContents(15.0, 30.0);

                // ========================================================
                // SHEET 3: CỐNG TRÒN (23 CỘT CHUẨN KÈM CHUỖI ĐỐT VÀ MỐI NỐI)
                // ========================================================
                var wsCt = workbook.Worksheets.Add("CongTron");
                string[] headersCt =
                {
                    "STT", "LyTrinh", "LoaiCong", "CauKien", "SoCua", "KhauDo", "KC_Tim",
                    "X1", "Y1", "Z1",
                    "X2", "Y2", "Z2",
                    "ChieuDai", "DoDoc", "GocXoay",
                    "L_Ngam_San", "L SGC TL", "L SGC HL", "Z SGC TL", "Z SGC HL",
                    "ChuoiDot", "GhiChu"
                };

                for (int i = 0; i < headersCt.Length; i++)
                {
                    var cell = wsCt.Cell(1, i + 1);
                    cell.Value = headersCt[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0E7490"); // Teal/Cyan
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                object[] ctRow1 =
                {
                    1, "Km1+050.00", "Cống tròn", "Đúc sẵn", 1, "D1500", 0.0,
                    580858.203, 1477280.506, 2.070,
                    580880.889, 1477323.339, 1.870,
                    48.47, 0.41, 62.09,
                    0.18, 3.00, 3.00, 2.07, 1.70,
                    "6@2.5, 2@3.0", "Cống tròn đúc sẵn đơn"
                };
                for (int c = 0; c < ctRow1.Length; c++) wsCt.Cell(2, c + 1).Value = XLCellValue.FromObject(ctRow1[c]);

                object[] ctRow2 =
                {
                    2, "Km1+100.00", "Cống tròn", "Đúc sẵn", 2, "2xD1500", 2.12,
                    580878.203, 1477280.506, 2.070,
                    580900.889, 1477323.339, 1.870,
                    48.47, 0.41, 62.09,
                    0.18, 3.00, 3.00, 2.07, 1.70,
                    "8@2.5", "Cống tròn đúc sẵn đôi"
                };
                for (int c = 0; c < ctRow2.Length; c++) wsCt.Cell(3, c + 1).Value = XLCellValue.FromObject(ctRow2[c]);

                object[] ctRow3 =
                {
                    3, "Km1+150.00", "Cống tròn", "Đúc sẵn", 1, "D1500", 0.0,
                    580898.203, 1477280.506, 2.070,
                    580920.889, 1477323.339, 1.870,
                    48.47, 0.41, 62.09,
                    0.18, 3.00, 3.00, 2.07, 1.70,
                    "6@2.5, 2@3.0", "Mối nối P2"
                };
                for (int c = 0; c < ctRow3.Length; c++) wsCt.Cell(4, c + 1).Value = XLCellValue.FromObject(ctRow3[c]);

                wsCt.Range("H2:M100").Style.NumberFormat.Format = "0.000";
                wsCt.Range("N2:P100").Style.NumberFormat.Format = "0.00";
                wsCt.Range("Q2:U100").Style.NumberFormat.Format = "0.00";
                wsCt.Range("A2:A100").Style.NumberFormat.Format = "0";
                wsCt.Range("E2:E100").Style.NumberFormat.Format = "0";
                wsCt.SheetView.FreezeRows(1);
                wsCt.Columns().AdjustToContents(15.0, 30.0);

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
            int colL_SGC_TL = FindCol(new[] { "L_SGC_TL", "L SGC TL", "L_SANGIACO_TL", "L SÂN GIA CỐ TL", "L_SGC_UPSTREAM" }, 21 + offsetCK);
            int colL_SGC_HL = FindCol(new[] { "L_SGC_HL", "L SGC HL", "L_SANGIACO_HL", "L SÂN GIA CỐ HL", "L_SGC_DOWNSTREAM" }, 22 + offsetCK);
            int colZ_SGC_TL = FindCol(new[] { "Z_SGC_TL", "Z SGC TL", "ZSGC_TL", "CD_SGC_TL", "CD SGC TL", "CAO DO SGC TL", "CAODO_SGC_TL", "Z2_SGC_TL", "Z_SGC_TL2" }, -1);
            int colZ_SGC_HL = FindCol(new[] { "Z_SGC_HL", "Z SGC HL", "ZSGC_HL", "CD_SGC_HL", "CD SGC HL", "CAO DO SGC HL", "CAODO_SGC_HL", "Z2_SGC_HL", "Z_SGC_HL2" }, -1);
            int offsetSGC = (colL_SGC_TL >= 0 || colL_SGC_HL >= 0) ? 2 : 0;
            if (colZ_SGC_TL >= 0 || colZ_SGC_HL >= 0) offsetSGC += 2;
            int colGhiChu = FindCol(new[] { "GHICHU", "GHI CHÚ", "GHI CHU", "LOAI RAI", "LOAIRAI" }, 21 + offsetCK + offsetSGC);

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

                if (colL_SGC_TL >= 0)
                {
                    item.L_SGC_TL = ParseDoubleString(GetCell(colL_SGC_TL), 3.0);
                }
                if (item.L_SGC_TL <= 0.01) item.L_SGC_TL = 3.0;

                if (colL_SGC_HL >= 0)
                {
                    item.L_SGC_HL = ParseDoubleString(GetCell(colL_SGC_HL), 3.0);
                }
                if (item.L_SGC_HL <= 0.01) item.L_SGC_HL = 3.0;

                if (colZ_SGC_TL >= 0)
                {
                    item.Z_SGC_TL = ParseDoubleString(GetCell(colZ_SGC_TL), 0.0);
                }
                if (colZ_SGC_HL >= 0)
                {
                    item.Z_SGC_HL = ParseDoubleString(GetCell(colZ_SGC_HL), 0.0);
                }

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

        private static string GetCellStringSafe(IXLCell cell)
        {
            try
            {
                return cell.GetString().Trim();
            }
            catch
            {
                try
                {
                    return cell.CachedValue.ToString().Trim();
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        private static int ParseInt(IXLCell cell, int defaultVal)
        {
            try
            {
                if (cell.TryGetValue<int>(out int val)) return val;
                string s = GetCellStringSafe(cell);
                if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out int parsed)) return parsed;
            }
            catch
            {
                try
                {
                    if (cell.CachedValue.IsNumber) return (int)cell.CachedValue.GetNumber();
                    string s = cell.CachedValue.ToString().Trim();
                    if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out int parsed)) return parsed;
                }
                catch { }
            }
            return defaultVal;
        }

        private static double ParseDouble(IXLCell cell, double defaultVal)
        {
            try
            {
                if (cell.TryGetValue<double>(out double val)) return val;
                return ParseDoubleString(GetCellStringSafe(cell), defaultVal);
            }
            catch
            {
                try
                {
                    if (cell.CachedValue.IsNumber) return cell.CachedValue.GetNumber();
                    return ParseDoubleString(cell.CachedValue.ToString(), defaultVal);
                }
                catch { }
            }
            return defaultVal;
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
