using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using InfraBIM.ManholeTool.Models;

namespace InfraBIM.ManholeTool.Services
{
    /// <summary>
    /// Dịch vụ đọc và tạo file Excel chuẩn 7 cột 2 sheet cho công cụ Rải Hố Ga
    /// </summary>
    public static class ManholeExcelService
    {
        /// <summary>
        /// Mở workbook an toàn qua MemoryStream với FileShare.ReadWrite chống khóa file khi Excel đang mở
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
            throw new IOException($"Không thể mở file Excel: {filePath}");
        }

        /// <summary>
        /// Đọc toàn bộ dữ liệu hố ga từ 2 sheet HO GA TRAI và HO GA PHAI
        /// </summary>
        public static (List<ManholeRowData> Trai, List<ManholeRowData> Phai) ReadManholeExcel(string filePath)
        {
            var listTrai = new List<ManholeRowData>();
            var listPhai = new List<ManholeRowData>();

            if (!File.Exists(filePath)) return (listTrai, listPhai);

            using var workbook = OpenWorkbookSafe(filePath);

            // 1. Đọc Sheet Hố Ga Trái
            IXLWorksheet? wsTrai = FindWorksheet(workbook, "HO GA TRAI", "HỐ GA TRÁI", "TRAI", "HG_TRAI");
            if (wsTrai != null)
            {
                listTrai = ReadSheet(wsTrai, "HO GA TRAI");
            }
            else if (workbook.Worksheets.Count >= 1)
            {
                // Fallback nếu người dùng đặt tên sheet khác
                listTrai = ReadSheet(workbook.Worksheet(1), "HO GA TRAI");
            }

            // 2. Đọc Sheet Hố Ga Phải
            IXLWorksheet? wsPhai = FindWorksheet(workbook, "HO GA PHAI", "HỐ GA PHẢI", "PHAI", "HG_PHAI");
            if (wsPhai != null)
            {
                listPhai = ReadSheet(wsPhai, "HO GA PHAI");
            }
            else if (workbook.Worksheets.Count >= 2)
            {
                // Fallback sang Sheet 2
                listPhai = ReadSheet(workbook.Worksheet(2), "HO GA PHAI");
            }

            return (listTrai, listPhai);
        }

        private static IXLWorksheet? FindWorksheet(XLWorkbook wb, params string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                foreach (var ws in wb.Worksheets)
                {
                    if (string.Equals(ws.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return ws;
                    }
                }
            }
            // Thử tìm theo Contains
            foreach (var name in possibleNames)
            {
                foreach (var ws in wb.Worksheets)
                {
                    if (ws.Name.ToUpperInvariant().Contains(name.ToUpperInvariant()))
                    {
                        return ws;
                    }
                }
            }
            return null;
        }

        private static List<ManholeRowData> ReadSheet(IXLWorksheet ws, string sideName)
        {
            var result = new List<ManholeRowData>();
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow < 2) return result;

            // Tìm dòng tiêu đề (Header row)
            int headerRow = 1;
            for (int r = 1; r <= Math.Min(10, lastRow); r++)
            {
                string c1 = ws.Cell(r, 1).GetString().Trim().ToUpperInvariant();
                string c2 = ws.Cell(r, 2).GetString().Trim().ToUpperInvariant();
                if (c1.Contains("STT") || c2.Contains("TEN") || c2.Contains("TÊN") || c2.Contains("HO GA"))
                {
                    headerRow = r;
                    break;
                }
            }

            // Bản đồ vị trí cột
            int colStt = 1;
            int colTen = 2;
            int colX = 3;
            int colY = 4;
            int colCdDinh = 5;
            int colCdDay = 6;
            int colGocPv = 7;
            int colCdDayCong = -1;
            int colLyTrinh = -1;

            // Quét tìm vị trí cột theo tên tiêu đề nếu có (hỗ trợ cả chuẩn 7 cột và file dự án thực tế A, C, D)
            int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 7;
            for (int c = 1; c <= lastCol; c++)
            {
                string header = ws.Cell(headerRow, c).GetString().Trim().ToUpperInvariant();
                if (header.Contains("STT")) colStt = c;
                else if (header.Contains("LY TRINH") || header.Contains("LÝ TRÌNH") || header.Contains("KM")) colLyTrinh = c;
                else if (header.Contains("TEN") || header.Contains("TÊN")) colTen = c;
                else if (header.Contains("TOA DO X") || header.Contains("TỌA ĐỘ X") || header == "X" || header.Contains("X (M)")) colX = c;
                else if (header.Contains("TOA DO Y") || header.Contains("TỌA ĐỘ Y") || header == "Y" || header.Contains("Y (M)")) colY = c;
                else if (header.Contains("DINH") || header.Contains("ĐỈNH") || header.Contains("- A")) colCdDinh = c;
                else if (header.Contains("DAY CONG") || header.Contains("ĐÁY CỐNG") || header.Contains("- C")) colCdDayCong = c;
                else if (header.Contains("DAY HO") || header.Contains("ĐÁY HỐ") || header.Contains("DAY GA") || header.Contains("ĐÁY GA") || header.Contains("- D")) colCdDay = c;
                else if (header.Contains("DAY") || header.Contains("ĐÁY"))
                {
                    if (colCdDay == 6 || colCdDay == -1) colCdDay = c;
                }
                else if (header.Contains("GOC") || header.Contains("GÓC") || header.Contains("PV") || header.Contains("PHUONG VI")) colGocPv = c;
            }

            int autoStt = 1;
            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                if (row.IsEmpty()) continue;

                string tenHoGa = ws.Cell(r, colTen).GetString().Trim();
                string xStr = ws.Cell(r, colX).GetString().Trim();
                string yStr = ws.Cell(r, colY).GetString().Trim();

                // Nếu cả tên, X, Y đều rỗng thì bỏ qua
                if (string.IsNullOrWhiteSpace(tenHoGa) && string.IsNullOrWhiteSpace(xStr) && string.IsNullOrWhiteSpace(yStr))
                    continue;

                int stt = autoStt;
                if (int.TryParse(ws.Cell(r, colStt).GetString().Trim(), out int parsedStt))
                {
                    stt = parsedStt;
                }

                double x = ParseDouble(ws.Cell(r, colX));
                double y = ParseDouble(ws.Cell(r, colY));
                double cdDinh = ParseDouble(ws.Cell(r, colCdDinh));
                double cdDay = ParseDouble(ws.Cell(r, colCdDay));
                double cdDayCong = colCdDayCong > 0 ? ParseDouble(ws.Cell(r, colCdDayCong)) : 0.0;
                double gocPv = ParseDouble(ws.Cell(r, colGocPv));
                string lyTrinh = colLyTrinh > 0 ? ws.Cell(r, colLyTrinh).GetString().Trim() : string.Empty;

                if (string.IsNullOrWhiteSpace(tenHoGa))
                {
                    tenHoGa = sideName.Contains("TRAI") ? $"GT-{stt:D2}" : $"GP-{stt:D2}";
                }

                var item = new ManholeRowData
                {
                    STT = stt,
                    TenHoGa = tenHoGa,
                    ToaDoX = x,
                    ToaDoY = y,
                    CdDinh = cdDinh,
                    CdDay = cdDay,
                    CdDayCong = cdDayCong,
                    GocPv = gocPv,
                    LyTrinh = lyTrinh,
                    Side = sideName,
                    IsSelected = true
                };

                result.Add(item);
                autoStt++;
            }

            return result;
        }

        private static double ParseDouble(IXLCell cell)
        {
            if (cell.TryGetValue<double>(out double val))
            {
                return val;
            }
            string str = cell.GetString().Trim().Replace(",", ".");
            if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
            {
                return parsed;
            }
            return 0.0;
        }

        /// <summary>
        /// Tạo và xuất file Excel mẫu với 2 sheet HO GA TRAI và HO GA PHAI đúng 7 cột chuẩn
        /// </summary>
        public static void ExportSampleExcel(string filePath)
        {
            using var workbook = new XLWorkbook();

            // 1. Tạo Sheet HO GA TRAI
            var wsTrai = workbook.Worksheets.Add("HO GA TRAI");
            BuildSheetTemplate(wsTrai, "HO GA TRAI", "GT", 500000.0, 1150000.0, 5.5, 3.2, 45.0);

            // 2. Tạo Sheet HO GA PHAI
            var wsPhai = workbook.Worksheets.Add("HO GA PHAI");
            BuildSheetTemplate(wsPhai, "HO GA PHAI", "GP", 500015.0, 1150000.0, 5.6, 3.3, 45.0);

            workbook.SaveAs(filePath);
        }

        private static void BuildSheetTemplate(
            IXLWorksheet ws, 
            string sheetTitle, 
            string prefix, 
            double startX, 
            double startY, 
            double startDinh, 
            double startDay, 
            double gocPv)
        {
            // Thiết lập Header
            string[] headers = new[]
            {
                "STT",
                "TEN HO GA",
                "TOA DO X",
                "TOA DO Y",
                "CD DINH",
                "CD DAY",
                "GOC PV"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Font.FontSize = 11;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#16325C");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#38BDF8");
            }
            ws.Row(1).Height = 28;

            // Thêm dữ liệu mẫu 10 hố ga
            for (int i = 1; i <= 10; i++)
            {
                int r = i + 1;
                double x = startX + (i - 1) * 30.0;
                double y = startY + (i - 1) * 15.0;
                double cdDinh = Math.Round(startDinh + (i - 1) * 0.05, 3);
                double cdDay = Math.Round(startDay - (i - 1) * 0.08, 3);
                double angle = gocPv;

                ws.Cell(r, 1).Value = i;
                ws.Cell(r, 2).Value = $"{prefix}-{i:D2}";
                ws.Cell(r, 3).Value = x;
                ws.Cell(r, 4).Value = y;
                ws.Cell(r, 5).Value = cdDinh;
                ws.Cell(r, 6).Value = cdDay;
                ws.Cell(r, 7).Value = angle;

                // Format số
                ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 3).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(r, 4).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(r, 5).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(r, 6).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(r, 7).Style.NumberFormat.Format = "#,##0.0";

                // Border mỏng
                for (int c = 1; c <= 7; c++)
                {
                    ws.Cell(r, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Cell(r, c).Style.Border.OutsideBorderColor = XLColor.FromHtml("#D1D5DB");
                }
                ws.Row(r).Height = 22;
            }

            // Tự động căn chỉnh độ rộng cột
            ws.Columns(1, 7).AdjustToContents();
            ws.Column(1).Width = 8;
            ws.Column(2).Width = 16;
            ws.Column(3).Width = 18;
            ws.Column(4).Width = 18;
            ws.Column(5).Width = 15;
            ws.Column(6).Width = 15;
            ws.Column(7).Width = 15;
        }
    }
}
