using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClosedXML.Excel;

namespace GenerateSampleExcel
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string baseDir = @"c:\Users\ADMIN\Desktop\PHAT TRIEN TOOL_HTKT";
            string resDir = Path.Combine(baseDir, @"src\InfraBIM.CulvertTool\Resources");
            Directory.CreateDirectory(resDir);

            if (args.Length > 0)
            {
                if (args[0] == "crop-profile")
                {
                    CropProfileNumbers();
                    return;
                }
                if (args[0] == "scan-dwg")
                {
                    ScanDwgText(baseDir);
                    return;
                }
                if (args[0] == "annotate")
                {
                    AnnotateCulvertImage();
                    return;
                }
                if (args[0] == "inspect-lap-ghep")
                {
                    InspectLapGhep(baseDir);
                    return;
                }
                if (args[0] == "create-tool-excel")
                {
                    CreateToolExcel(baseDir);
                    return;
                }
                if (args[0] == "create-elevation-sheet")
                {
                    CreateElevationWorkbook(baseDir);
                    return;
                }
                if (args[0] == "convert")
                {
                    ConvertTvtkToProjectExcel(baseDir);
                    return;
                }
                if (File.Exists(args[0]))
                {
                    InspectExcel(args[0]);
                    return;
                }
            }

            // 1. Tạo Icon cho Ribbon Revit (32x32 và 16x16)
            GenerateCulvertIcon(Path.Combine(resDir, "culvert_32.png"), 32);
            GenerateCulvertIcon(Path.Combine(resDir, "culvert_64.png"), 64);
            GenerateCulvertIcon(Path.Combine(resDir, "culvert_16.png"), 16);
            Console.WriteLine("[SUCCESS] Đã tạo bộ icon Ribbon cho Revit!");

            // 2. Tạo Excel mẫu
            GenerateExcel(baseDir);
        }

        static void InspectExcel(string filePath)
        {
            Console.WriteLine($"=== INSPECTING: {filePath} ===");
            using var wb = new XLWorkbook(filePath);
            foreach (var ws in wb.Worksheets)
            {
                Console.WriteLine($"\n--- Sheet: {ws.Name} (LastRow={ws.LastRowUsed()?.RowNumber()}, LastCol={ws.LastColumnUsed()?.ColumnNumber()}) ---");
                int maxRow = Math.Min(ws.LastRowUsed()?.RowNumber() ?? 0, 30);
                int maxCol = Math.Min(ws.LastColumnUsed()?.ColumnNumber() ?? 0, 30);
                for (int r = 1; r <= maxRow; r++)
                {
                    var rowVals = new System.Collections.Generic.List<string>();
                    for (int c = 1; c <= maxCol; c++)
                    {
                        string val = ws.Cell(r, c).GetFormattedString().Trim();
                        rowVals.Add(val);
                    }
                    if (rowVals.Any(v => !string.IsNullOrEmpty(v)))
                    {
                        Console.WriteLine($"R{r:D2}: " + string.Join(" | ", rowVals.Take(15)));
                    }
                }
            }
        }

        static void GenerateCulvertIcon(string outputPath, int size)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // Màu sắc theo đúng yêu cầu: Xanh đậm, Xanh lơ (Cyan), Xanh lá nhạt (Mint)
                var colorDarkNavy = Color.FromRgb(11, 25, 44);     // Xanh đậm #0B192C
                var colorDeepBlue = Color.FromRgb(30, 58, 138);    // Xanh dương đậm #1E3A8A
                var colorCyan = Color.FromRgb(56, 189, 248);       // Xanh lơ #38BDF8
                var colorCyanLight = Color.FromRgb(186, 230, 253);  // Xanh lơ nhạt #BAE6FD
                var colorMintGreen = Color.FromRgb(74, 222, 128);  // Xanh lá nhạt #4ADE80
                var colorMintSoft = Color.FromRgb(167, 243, 208);   // Xanh lá rất nhạt #A7F3D0

                double scale = size / 32.0;

                // Nền bo góc mềm mại màu xanh đậm sang trọng
                var bgBrush = new LinearGradientBrush(
                    colorDarkNavy,
                    Color.FromRgb(15, 23, 42),
                    new Point(0, 0),
                    new Point(1, 1));
                dc.DrawRoundedRectangle(bgBrush, new Pen(new SolidColorBrush(colorCyan), 1.0 * scale),
                    new Rect(0.5 * scale, 0.5 * scale, (size - 1.0), (size - 1.0)), 6 * scale, 6 * scale);

                // Đường ngang mặt đường (Road surface / Embankment)
                var roadPen = new Pen(new SolidColorBrush(colorDeepBlue), 3.0 * scale);
                dc.DrawLine(roadPen, new Point(3 * scale, 8 * scale), new Point(29 * scale, 8 * scale));

                var roadLinePen = new Pen(new SolidColorBrush(colorCyanLight), 1.0 * scale);
                dc.DrawLine(roadLinePen, new Point(4 * scale, 7 * scale), new Point(28 * scale, 7 * scale));

                // Ống cống tròn (Culvert pipe) ở trung tâm
                var pipeCenter = new Point(16 * scale, 19 * scale);
                double outerRadius = 8.5 * scale;
                double innerRadius = 6.2 * scale;

                // Vỏ bê tông ngoài cống (Màu xanh lơ)
                var pipePen = new Pen(new SolidColorBrush(colorCyan), 2.2 * scale);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(80, 56, 189, 248)), pipePen, pipeCenter, outerRadius, outerRadius);

                // Lòng cống trong
                var innerPen = new Pen(new SolidColorBrush(colorCyanLight), 1.0 * scale);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(8, 20, 36)), innerPen, pipeCenter, innerRadius, innerRadius);

                // Dòng nước chảy trong cống (Màu xanh lá nhạt / mint green)
                var waterBrush = new SolidColorBrush(colorMintGreen);
                var streamGeom = new StreamGeometry();
                using (var ctx = streamGeom.Open())
                {
                    ctx.BeginFigure(new Point(pipeCenter.X - innerRadius * 0.95, pipeCenter.Y + innerRadius * 0.2), true, true);
                    ctx.BezierTo(
                        new Point(pipeCenter.X - innerRadius * 0.3, pipeCenter.Y - innerRadius * 0.1),
                        new Point(pipeCenter.X + innerRadius * 0.3, pipeCenter.Y + innerRadius * 0.5),
                        new Point(pipeCenter.X + innerRadius * 0.95, pipeCenter.Y + innerRadius * 0.2),
                        true, false);
                    ctx.LineTo(new Point(pipeCenter.X + innerRadius * 0.7, pipeCenter.Y + innerRadius * 0.85), true, false);
                    ctx.LineTo(new Point(pipeCenter.X - innerRadius * 0.7, pipeCenter.Y + innerRadius * 0.85), true, false);
                }
                dc.DrawGeometry(waterBrush, null, streamGeom);

                // Mũi tên hướng tuyến / dòng chảy (Directional indicator) màu xanh lá nhạt
                var arrowPen = new Pen(new SolidColorBrush(colorMintSoft), 1.4 * scale);
                dc.DrawLine(arrowPen, new Point(25 * scale, 19 * scale), new Point(29 * scale, 19 * scale));
                dc.DrawLine(arrowPen, new Point(27.5 * scale, 17.5 * scale), new Point(29 * scale, 19 * scale));
                dc.DrawLine(arrowPen, new Point(27.5 * scale, 20.5 * scale), new Point(29 * scale, 19 * scale));
            }

            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using (var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(stream);
            }
        }

        static void GenerateExcel(string baseDir)
        {
            string filePath = Path.Combine(baseDir, "Bang_Du_Lieu_Cong_Ngang_Mau.xlsx");
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("DuLieuCongNgang");

                string[] headers = new[]
                {
                    "STT", "LyTrinh", "LoaiCong", "SoCua", "KhauDo",
                    "X1", "Y1", "Z1", "X2", "Y2", "Z2",
                    "ChieuDai", "DoDoc", "GocXoay", "SoHopNoi",
                    "Dist_HN1", "Dist_HN2", "L_Ngam_San", "Khe_Ho_HN"
                };

                for (int col = 0; col < headers.Length; col++)
                {
                    var cell = ws.Cell(1, col + 1);
                    cell.Value = headers[col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontSize = 11;
                    cell.Style.Font.FontColor = XLColor.FromHtml("#38BDF8"); // Xanh lơ
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0B192C"); // Xanh đậm
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#1E293B");
                }

                object[][] sampleData = new object[][]
                {
                    new object[] { 1, "Km1+250.50", "CONG_TRON", 2, "D1500", 587234.120, 1194562.890, 265.140, 587248.560, 1194558.120, 264.840, 15.21, 2.00, 108.30, 2, 5.38, 4.20, 0.30, 0.05 },
                    new object[] { 2, "Km1+680.00", "CONG_TRON", 1, "D1000", 587420.350, 1194605.100, 266.500, 587432.800, 1194601.200, 266.250, 13.05, 1.92, 107.40, 0, 0.00, 0.00, 0.30, 0.05 },
                    new object[] { 3, "Km2+100.20", "CONG_HOP", 1, "2000x2000", 587750.800, 1194710.450, 268.000, 587768.200, 1194704.900, 267.650, 18.25, 1.92, 107.70, 0, 0.00, 0.00, 0.30, 0.05 },
                    new object[] { 4, "Km2+550.00", "CONG_HOP", 2, "2500x2000", 588120.400, 1194830.150, 269.800, 588142.100, 1194823.300, 269.360, 22.75, 1.93, 107.50, 2, 6.50, 5.80, 0.35, 0.05 },
                    new object[] { 5, "Km3+020.15", "CONG_TRON", 1, "D1200", 588510.600, 1194950.200, 271.200, 588525.000, 1194945.700, 270.910, 15.09, 1.92, 107.30, 1, 5.20, 0.00, 0.30, 0.05 },
                    new object[] { 6, "Km3+450.80", "CONG_HOP", 1, "1500x1500", 588900.250, 1195070.800, 272.500, 588916.500, 1195065.700, 272.170, 17.03, 1.94, 107.40, 0, 0.00, 0.00, 0.30, 0.05 }
                };

                for (int r = 0; r < sampleData.Length; r++)
                {
                    int rowNum = r + 2;
                    var rowData = sampleData[r];

                    for (int c = 0; c < rowData.Length; c++)
                    {
                        var cell = ws.Cell(rowNum, c + 1);
                        cell.Value = XLCellValue.FromObject(rowData[c]);
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");

                        if (c == 0 || c == 3 || c == 14) cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        else if (c == 1 || c == 2 || c == 4) cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        else if (c >= 5 && c <= 10)
                        {
                            cell.Style.NumberFormat.Format = "#,##0.000";
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        }
                        else
                        {
                            cell.Style.NumberFormat.Format = "#,##0.00";
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        }
                    }

                    if (r % 2 == 1) ws.Range(rowNum, 1, rowNum, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
                }

                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents(15.0, 30.0);
                workbook.SaveAs(filePath);
                Console.WriteLine($"[SUCCESS] Đã lưu bảng tính: {filePath}");

                string altFilePath = Path.Combine(baseDir, "Mau_Du_Lieu_Cong_Ngang.xlsx");
                workbook.SaveAs(altFilePath);
            }
        }

        class TvtkCulvert
        {
            public int STT { get; set; }
            public string LyTrinh { get; set; } = "";
            public string LoaiCong { get; set; } = "CONG_HOP";
            public int SoCua { get; set; } = 1;
            public string KhauDo { get; set; } = "1.5x1.5";
            public double X1 { get; set; }
            public double Y1 { get; set; }
            public double Z1 { get; set; }
            public double X2 { get; set; }
            public double Y2 { get; set; }
            public double Z2 { get; set; }
            public double ChieuDai { get; set; }
            public double DoDoc { get; set; }
            public double GocXoay { get; set; }
            public int SoHopNoi { get; set; }
            public double Dist_HN1 { get; set; }
            public double Dist_HN2 { get; set; }
            public double L_Ngam_San { get; set; } = 0.30;
            public double Khe_Ho_HN { get; set; } = 0.05;
            public string GhiChu { get; set; } = "";
            public bool HasCoord { get; set; }
        }

        static void ConvertTvtkToProjectExcel(string baseDir)
        {
            string cadDir = Path.Combine(baseDir, @"CAD\Cap Bim");
            string tvtkExcel = Path.Combine(cadDir, "THONG KE CONG.xlsx");
            string csvCoord = Path.Combine(cadDir, "TOA_DO_CONG.csv");
            string outputExcel = Path.Combine(cadDir, "DU_LIEU_CONG_DU_AN_THUC_TE.xlsx");

            if (!File.Exists(tvtkExcel))
            {
                Console.WriteLine($"[ERROR] Không tìm thấy file: {tvtkExcel}");
                return;
            }

            Console.WriteLine($"=== BẮT ĐẦU CHUYỂN ĐỔI DỮ LIỆU TỪ: {Path.GetFileName(tvtkExcel)} ===");

            var culverts = new System.Collections.Generic.List<TvtkCulvert>();

            using (var wb = new XLWorkbook(tvtkExcel))
            {
                var ws = wb.Worksheet("TKC");
                int currentGroup = 0; // 1 = Cống trên tuyến, 2 = Cống kỹ thuật
                int sttCounter = 1;

                for (int r = 1; r <= ws.LastRowUsed()?.RowNumber(); r++)
                {
                    string rText = ws.Cell(r, 1).GetFormattedString().Trim() + " " + ws.Cell(r, 2).GetFormattedString().Trim();
                    if (rText.IndexOf("THỐNG KÊ CỐNG TRÊN TUYẾN", StringComparison.OrdinalIgnoreCase) >= 0 || 
                        rText.IndexOf("THONG KE CONG TREN TUYEN", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        currentGroup = 1;
                        continue;
                    }
                    if (rText.IndexOf("THỐNG KÊ CỐNG KỸ THUẬT", StringComparison.OrdinalIgnoreCase) >= 0 || 
                        rText.IndexOf("THONG KE CONG KY THUAT", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        currentGroup = 2;
                        continue;
                    }

                    string sttVal = ws.Cell(r, 1).GetFormattedString().Trim();
                    string lyTrinhVal = ws.Cell(r, 2).GetFormattedString().Trim();

                    if (!int.TryParse(sttVal, out int num) || string.IsNullOrEmpty(lyTrinhVal) || !lyTrinhVal.Contains("Km"))
                    {
                        continue;
                    }

                    var c = new TvtkCulvert();
                    c.STT = sttCounter++;
                    c.LyTrinh = lyTrinhVal.Replace("Cống ", "").Replace("cống ", "").Trim();

                    if (currentGroup == 1)
                    {
                        // Nhóm 1: Cống trên tuyến
                        string loaiCongRaw = ws.Cell(r, 8).GetFormattedString().Trim();
                        string khauDoRaw = ws.Cell(r, 9).GetFormattedString().Trim();
                        string soCuaRaw = ws.Cell(r, 10).GetFormattedString().Trim();
                        string chieuDaiRaw = ws.Cell(r, 11).GetFormattedString().Trim();
                        string cuaThuRaw = ws.Cell(r, 12).GetFormattedString().Trim();

                        c.LoaiCong = "CONG_HOP";
                        c.SoCua = int.TryParse(soCuaRaw, out int sc) ? sc : 1;

                        if (khauDoRaw.Contains("300x200")) c.KhauDo = "3.0x2.0";
                        else if (khauDoRaw.Contains("150x150")) c.KhauDo = "1.5x1.5";
                        else c.KhauDo = khauDoRaw;

                        if (double.TryParse(chieuDaiRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out double cd))
                        {
                            c.ChieuDai = cd;
                        }

                        if (cuaThuRaw.IndexOf("Hố thu", StringComparison.OrdinalIgnoreCase) >= 0 || cuaThuRaw.IndexOf("Ho thu", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            c.SoHopNoi = 1;
                            c.Dist_HN1 = 0.0;
                            c.GhiChu = "Có hố thu thượng lưu";
                        }
                        else
                        {
                            c.SoHopNoi = 0;
                            c.GhiChu = cuaThuRaw;
                        }
                    }
                    else
                    {
                        // Nhóm 2: Cống kỹ thuật
                        string khauDoRaw = ws.Cell(r, 9).GetFormattedString().Trim();
                        c.LoaiCong = "CONG_HOP";
                        c.SoCua = 1;
                        if (khauDoRaw.Contains("1500x1500") || khauDoRaw.Contains("150x150")) c.KhauDo = "1.5x1.5";
                        else c.KhauDo = khauDoRaw;
                        c.SoHopNoi = 0;
                        c.GhiChu = "Cống kỹ thuật ngang đường";
                    }

                    culverts.Add(c);
                }
            }

            Console.WriteLine($"[INFO] Đã đọc được {culverts.Count} cống từ {Path.GetFileName(tvtkExcel)}");

            // 2. Kiểm tra nếu có file TOA_DO_CONG.csv xuất từ CAD LISP
            if (File.Exists(csvCoord))
            {
                Console.WriteLine($"[INFO] Tìm thấy file tọa độ CAD: {Path.GetFileName(csvCoord)}. Đang ghép tọa độ...");
                var csvLines = File.ReadAllLines(csvCoord);
                foreach (var line in csvLines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    if (parts.Length < 7) continue;

                    string csvStt = parts[0].Trim();
                    string csvLyTrinh = parts[1].Trim();
                    if (double.TryParse(parts[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double x1) &&
                        double.TryParse(parts[3].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double y1) &&
                        double.TryParse(parts[4].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double x2) &&
                        double.TryParse(parts[5].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double y2))
                    {
                        double cd = (parts.Length > 6 && double.TryParse(parts[6].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double lVal)) ? lVal : 0;
                        double ang = (parts.Length > 7 && double.TryParse(parts[7].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double aVal)) ? aVal : 0;

                        // Khớp theo Lý trình hoặc STT
                        var matched = culverts.FirstOrDefault(c => 
                            (!string.IsNullOrEmpty(csvLyTrinh) && c.LyTrinh.IndexOf(csvLyTrinh, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (int.TryParse(csvStt, out int sNum) && c.STT == sNum));

                        if (matched != null)
                        {
                            matched.X1 = x1;
                            matched.Y1 = y1;
                            matched.X2 = x2;
                            matched.Y2 = y2;
                            if (matched.ChieuDai <= 0) matched.ChieuDai = cd;
                            matched.GocXoay = ang;
                            matched.HasCoord = true;
                            Console.WriteLine($"  -> Khớp thành công: STT {matched.STT} ({matched.LyTrinh})");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"[NOTE] Chưa có file {Path.GetFileName(csvCoord)}. Cột tọa độ sẽ để giá trị 0 hoặc hướng dẫn để anh điền.");
            }

            // 3. Xuất file Excel chuẩn 19 cột cho Tool Revit
            using (var outWb = new XLWorkbook())
            {
                var ws = outWb.Worksheets.Add("DuLieuCongNgang");

                string[] headers = new[]
                {
                    "STT", "LyTrinh", "LoaiCong", "SoCua", "KhauDo",
                    "X1", "Y1", "Z1", "X2", "Y2", "Z2",
                    "ChieuDai", "DoDoc", "GocXoay", "SoHopNoi",
                    "Dist_HN1", "Dist_HN2", "L_Ngam_San", "Khe_Ho_HN", "GhiChu_KiemTra"
                };

                for (int col = 0; col < headers.Length; col++)
                {
                    var cell = ws.Cell(1, col + 1);
                    cell.Value = headers[col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontSize = 11;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0284C7"); // Sky blue
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }
                ws.Row(1).Height = 28;

                for (int i = 0; i < culverts.Count; i++)
                {
                    var item = culverts[i];
                    int rowNum = i + 2;

                    ws.Cell(rowNum, 1).Value = item.STT;
                    ws.Cell(rowNum, 2).Value = item.LyTrinh;
                    ws.Cell(rowNum, 3).Value = item.LoaiCong;
                    ws.Cell(rowNum, 4).Value = item.SoCua;
                    ws.Cell(rowNum, 5).Value = item.KhauDo;

                    ws.Cell(rowNum, 6).Value = item.X1;
                    ws.Cell(rowNum, 7).Value = item.Y1;
                    ws.Cell(rowNum, 8).Value = item.Z1; // Cao độ đáy thượng lưu

                    ws.Cell(rowNum, 9).Value = item.X2;
                    ws.Cell(rowNum, 10).Value = item.Y2;
                    ws.Cell(rowNum, 11).Value = item.Z2; // Cao độ đáy hạ lưu

                    ws.Cell(rowNum, 12).Value = item.ChieuDai;
                    ws.Cell(rowNum, 13).Value = item.DoDoc;
                    ws.Cell(rowNum, 14).Value = item.GocXoay;
                    ws.Cell(rowNum, 15).Value = item.SoHopNoi;

                    ws.Cell(rowNum, 16).Value = item.Dist_HN1;
                    ws.Cell(rowNum, 17).Value = item.Dist_HN2;
                    ws.Cell(rowNum, 18).Value = item.L_Ngam_San;
                    ws.Cell(rowNum, 19).Value = item.Khe_Ho_HN;

                    string note = item.GhiChu;
                    if (!item.HasCoord) note = (string.IsNullOrEmpty(note) ? "" : note + " | ") + "Cần chạy LISP XTC lấy X, Y";
                    if (item.Z1 == 0 && item.Z2 == 0) note = (string.IsNullOrEmpty(note) ? "" : note + " | ") + "Cần tra trắc dọc lấy Z1, Z2";
                    ws.Cell(rowNum, 20).Value = note;

                    // Định dạng số
                    for (int c = 6; c <= 11; c++)
                    {
                        ws.Cell(rowNum, c).Style.NumberFormat.Format = "#,##0.000";
                    }
                    ws.Cell(rowNum, 12).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(rowNum, 13).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(rowNum, 14).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(rowNum, 18).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(rowNum, 19).Style.NumberFormat.Format = "#,##0.00";

                    // Tô màu hướng dẫn
                    // Cột Z1, Z2 (cần tra trắc dọc) tô màu vàng nhạt
                    ws.Cell(rowNum, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7");
                    ws.Cell(rowNum, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7");

                    if (item.HasCoord)
                    {
                        ws.Cell(rowNum, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7"); // Xanh lá nhạt
                        ws.Cell(rowNum, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7");
                        ws.Cell(rowNum, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7");
                        ws.Cell(rowNum, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7");
                    }
                    else
                    {
                        ws.Cell(rowNum, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2"); // Đỏ nhạt
                        ws.Cell(rowNum, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");
                        ws.Cell(rowNum, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");
                        ws.Cell(rowNum, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");
                    }

                    ws.Range(rowNum, 1, rowNum, 20).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Range(rowNum, 1, rowNum, 20).Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
                }

                ws.SheetView.FreezeRows(1);
                ws.Columns().AdjustToContents(12.0, 35.0);

                outWb.SaveAs(outputExcel);
                Console.WriteLine($"\n[THÀNH CÔNG] Đã tạo file Excel dự án thực tế chuẩn 19 cột tại:");
                Console.WriteLine($"==> {outputExcel}");
            }
        }

        static void AnnotateCulvertImage()
        {
            string userImgPath = @"C:\Users\ADMIN\.gemini\antigravity\brain\3829e2fd-b6e6-46de-9443-93a35207030f\.user_uploaded\media_1790751529705.png";
            string outputImgPath = @"C:\Users\ADMIN\.gemini\antigravity\brain\3829e2fd-b6e6-46de-9443-93a35207030f\huong_dan_pick_diem_cad.png";

            if (!File.Exists(userImgPath))
            {
                Console.WriteLine($"[ERROR] Không tìm thấy file ảnh: {userImgPath}");
                return;
            }

            var uri = new Uri(userImgPath);
            var srcBitmap = new BitmapImage(uri);
            int width = srcBitmap.PixelWidth;
            int height = srcBitmap.PixelHeight;
            Console.WriteLine($"[INFO] Kích thước ảnh: Width = {width}, Height = {height}");

            var wb = new WriteableBitmap(srcBitmap);
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            wb.CopyPixels(pixels, stride, 0);

            var magentaPts = new System.Collections.Generic.List<System.Drawing.Point>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * stride + x * 4;
                    byte b = pixels[idx];
                    byte g = pixels[idx + 1];
                    byte r = pixels[idx + 2];
                    // Magenta is high R, low G, high B
                    if (r > 180 && g < 100 && b > 180)
                    {
                        magentaPts.Add(new System.Drawing.Point(x, y));
                    }
                }
            }
            // Tạo ảnh chú thích chuyên nghiệp với WPF
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // 1. Vẽ ảnh gốc nền
                dc.DrawImage(srcBitmap, new Rect(0, 0, width, height));

                // 2. Định nghĩa các màu sắc và font chữ
                var fontTypeface = new Typeface(new FontFamily("Segoe UI, Arial, sans-serif"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var yellowBrush = new SolidColorBrush(Color.FromRgb(254, 240, 138));
                var yellowPen = new Pen(new SolidColorBrush(Color.FromRgb(234, 179, 8)), 2.5);
                var redBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                var greenBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                var whiteBrush = new SolidColorBrush(Colors.White);
                var cyanBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                var boxBgBrush = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)); // Dark navy bg #0F172A semi-transparent
                var boxBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 2.0);

                // --- VỊ TRÍ ĐIỂM CHUẨN ---
                // P1: Điểm đầu Thượng lưu (Inlet) - Tim tiếp giáp giữa tường đầu và đốt cống: (208, 615)
                Point pt1 = new Point(208, 615);
                // P2: Điểm đầu Hạ lưu (Outlet) - Tim tiếp giáp giữa đốt cống và tường đầu: (462, 135)
                Point pt2 = new Point(462, 135);

                // Đường dóng trục cống P1 -> P2
                var axisPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 250, 204, 21)), 2.0)
                {
                    DashStyle = DashStyles.Dash
                };
                dc.DrawLine(axisPen, pt1, pt2);

                // --- VẼ ĐIỂM 1 (P1 - THƯỢNG LƯU) ---
                // Vòng tròn hào quang
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(80, 239, 68, 68)), null, pt1, 24, 24);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(239, 68, 68)), new Pen(whiteBrush, 3.0), pt1, 14, 14);
                dc.DrawEllipse(whiteBrush, null, pt1, 4, 4);

                // Callout Box cho Điểm 1
                Rect box1 = new Rect(20, 680, 310, 72);
                dc.DrawRoundedRectangle(boxBgBrush, new Pen(redBrush, 2.0), box1, 6, 6);
                dc.DrawLine(new Pen(redBrush, 2.0), new Point(box1.Right - 50, box1.Top), pt1);

                var ft1_title = new FormattedText(
                    "🔴 ĐIỂM 1 (PICK THỨ 1): THƯỢNG LƯU",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    fontTypeface,
                    14,
                    new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                    1.25);
                dc.DrawText(ft1_title, new Point(box1.Left + 10, box1.Top + 8));

                var ft1_desc = new FormattedText(
                    "• Vị trí: Tim cống tại mép Tường đầu / Cửa thu\n• Hướng: Nơi dòng nước bắt đầu chảy VÀO cống",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    fontTypeface,
                    11.5,
                    whiteBrush,
                    1.25);
                dc.DrawText(ft1_desc, new Point(box1.Left + 10, box1.Top + 30));

                // --- VẼ ĐIỂM 2 (P2 - HẠ LƯU) ---
                // Vòng tròn hào quang
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(80, 34, 197, 94)), null, pt2, 24, 24);
                dc.DrawEllipse(greenBrush, new Pen(whiteBrush, 3.0), pt2, 14, 14);
                dc.DrawEllipse(whiteBrush, null, pt2, 4, 4);

                // Callout Box cho Điểm 2
                Rect box2 = new Rect(400, 20, 320, 72);
                dc.DrawRoundedRectangle(boxBgBrush, new Pen(greenBrush, 2.0), box2, 6, 6);
                dc.DrawLine(new Pen(greenBrush, 2.0), new Point(box2.Left + 60, box2.Bottom), pt2);

                var ft2_title = new FormattedText(
                    "🟢 ĐIỂM 2 (PICK THỨ 2): HẠ LƯU",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    fontTypeface,
                    14,
                    new SolidColorBrush(Color.FromRgb(74, 222, 128)),
                    1.25);
                dc.DrawText(ft2_title, new Point(box2.Left + 10, box2.Top + 8));

                var ft2_desc = new FormattedText(
                    "• Vị trí: Tim cống tại mép Tường đầu / Cửa xả\n• Hướng: Nơi dòng nước chảy RA KHỎI cống",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    fontTypeface,
                    11.5,
                    whiteBrush,
                    1.25);
                dc.DrawText(ft2_desc, new Point(box2.Left + 10, box2.Top + 30));

                // --- HỘP CHỈ HƯỚNG DÒNG CHẢY Ở GIỮA ĐƯỜNG ---
                Rect boxMid = new Rect(20, 340, 260, 68);
                dc.DrawRoundedRectangle(boxBgBrush, boxBorderPen, boxMid, 6, 6);
                var ftFlow = new FormattedText(
                    "➡ NGUYÊN TẮC QUAN TRỌNG:\nLuôn Pick theo chiều dòng chảy:\nTừ THƯỢNG LƯU (P1) ➔ HẠ LƯU (P2)",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    fontTypeface,
                    12,
                    yellowBrush,
                    1.25);
                dc.DrawText(ftFlow, new Point(boxMid.Left + 10, boxMid.Top + 8));
                dc.DrawLine(new Pen(cyanBrush, 1.5), new Point(boxMid.Right, boxMid.Top + 34), new Point(310, 370));

                // --- ĐÁNH DẤU 2 HỐ GA VỈA HÈ ---
                Point ptHg1 = new Point(235, 556);
                Point ptHg2 = new Point(433, 197);
                var hgPen = new Pen(cyanBrush, 1.5) { DashStyle = DashStyles.Dash };
                dc.DrawEllipse(null, hgPen, ptHg1, 16, 16);
                dc.DrawEllipse(null, hgPen, ptHg2, 16, 16);

                var ftHgNote = new FormattedText(
                    "Hố ga vỉa hè\n(Hộp nối)",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    fontTypeface,
                    10.5,
                    cyanBrush,
                    1.25);
                dc.DrawText(ftHgNote, new Point(ptHg1.X + 22, ptHg1.Y - 10));
                dc.DrawText(ftHgNote, new Point(ptHg2.X - 85, ptHg2.Y - 10));
            }

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = new FileStream(outputImgPath, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(fs);
            }
            Console.WriteLine($"[SUCCESS] Đã tạo ảnh chú thích tại: {outputImgPath}");
        }

        static void ScanDwgText(string baseDir)
        {
            string dwgProfile = Path.Combine(baseDir, @"CAD\Cap Bim\00a. TRAC DOC THOAT NUOC.dwg");
            string dwgPlan = Path.Combine(baseDir, @"CAD\Cap Bim\00.BINH DO THOAT NUOC-Sua TD.dwg");

            SearchInDwg(dwgProfile, "00a. TRAC DOC THOAT NUOC.dwg");
            SearchInDwg(dwgPlan, "00.BINH DO THOAT NUOC-Sua TD.dwg");
        }

        static void SearchInDwg(string filePath, string label)
        {
            if (!File.Exists(filePath)) return;
            Console.WriteLine($"\n=== QUÉT TÌM CHUỖI TRONG {label} ({new FileInfo(filePath).Length / 1024 / 1024} MB) ===");
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            byte[] bytes = ms.ToArray();

            // Tìm các đoạn text UTF-16LE hoặc ASCII chứa "1+370" hoặc "C1" hoặc "đáy" hoặc "CĐ"
            var encodings = new[] { System.Text.Encoding.Unicode, System.Text.Encoding.UTF8, System.Text.Encoding.ASCII };
            var keywords = new[] { "1+370", "C1", "GT-6", "300x200", "150x150" };

            foreach (var kw in keywords)
            {
                byte[] kwBytesUni = System.Text.Encoding.Unicode.GetBytes(kw);
                int foundCount = 0;
                for (int i = 0; i <= bytes.Length - kwBytesUni.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < kwBytesUni.Length; j++)
                    {
                        if (bytes[i + j] != kwBytesUni[j]) { match = false; break; }
                    }
                    if (match)
                    {
                        foundCount++;
                        // Lấy ngữ cảnh xung quanh 200 bytes
                        int start = Math.Max(0, i - 100);
                        int len = Math.Min(bytes.Length - start, 300);
                        string snippet = System.Text.Encoding.Unicode.GetString(bytes, start, len);
                        string clean = new string(snippet.Where(c => !char.IsControl(c) || c == ' ' || c == '\n').ToArray());
                        Console.WriteLine($"[MATCH '{kw}'] Offset {i}: {clean.Trim()}");
                        if (foundCount >= 3) break;
                    }
                }
                if (foundCount == 0)
                {
                    // Thử ASCII
                    byte[] kwBytesAscii = System.Text.Encoding.ASCII.GetBytes(kw);
                    for (int i = 0; i <= bytes.Length - kwBytesAscii.Length; i++)
                    {
                        bool match = true;
                        for (int j = 0; j < kwBytesAscii.Length; j++)
                        {
                            if (bytes[i + j] != kwBytesAscii[j]) { match = false; break; }
                        }
                        if (match)
                        {
                            foundCount++;
                            int start = Math.Max(0, i - 100);
                            int len = Math.Min(bytes.Length - start, 300);
                            string snippet = System.Text.Encoding.ASCII.GetString(bytes, start, len);
                            string clean = new string(snippet.Where(c => !char.IsControl(c) || c == ' ' || c == '\n').ToArray());
                            Console.WriteLine($"[ASCII MATCH '{kw}'] Offset {i}: {clean.Trim()}");
                            if (foundCount >= 3) break;
                        }
                    }
                }
            }
        }

        static void CropProfileNumbers()
        {
            string profileImg = @"C:\Users\ADMIN\.gemini\antigravity\brain\3829e2fd-b6e6-46de-9443-93a35207030f\.user_uploaded\media_1790753927249.png";
            string outputImg = @"C:\Users\ADMIN\.gemini\antigravity\brain\3829e2fd-b6e6-46de-9443-93a35207030f\huong_dan_doc_trac_doc.png";

            if (!File.Exists(profileImg)) return;

            var uri = new Uri(profileImg);
            var src = new BitmapImage(uri);
            int w = src.PixelWidth;
            int h = src.PixelHeight;
            Console.WriteLine($"[PROFILE IMG] Width={w}, Height={h}");

            int outW = w + 450;
            int outH = h;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(15, 23, 42)), null, new Rect(0, 0, outW, outH));
                dc.DrawImage(src, new Rect(20, 0, w, h));

                var fontTypeface = new Typeface(new FontFamily("Segoe UI, Arial, sans-serif"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var whiteBrush = new SolidColorBrush(Colors.White);
                var yellowBrush = new SolidColorBrush(Color.FromRgb(254, 240, 138));
                var greenBrush = new SolidColorBrush(Color.FromRgb(74, 222, 128));
                var redBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                var cyanBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                var cardBg = new SolidColorBrush(Color.FromRgb(30, 41, 59));

                // 1. CHÚ THÍCH Z1: 2.35 (Cao độ đáy Thượng lưu)
                double y_Z1 = h * 0.76;
                Rect boxZ1 = new Rect(w + 40, y_Z1 - 45, 380, 100);
                dc.DrawRoundedRectangle(cardBg, new Pen(redBrush, 2.0), boxZ1, 8, 8);
                dc.DrawLine(new Pen(redBrush, 2.0), new Point(w * 0.42 + 20, y_Z1), new Point(boxZ1.Left, boxZ1.Top + 30));
                dc.DrawEllipse(redBrush, null, new Point(w * 0.42 + 20, y_Z1), 6, 6);

                var ftZ1 = new FormattedText(
                    "🔴 Z1 (ĐÁY THƯỢNG LƯU) = 2.35 m\n• Vị trí: Km 1+370.00 (Đầu cống C1)\n• Hàng màu xanh lá: Cao độ đáy cống\n➔ ĐIỀN CON SỐ 2.35 VÀO CỘT Z1",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    fontTypeface,
                    12.5,
                    whiteBrush,
                    1.25);
                dc.DrawText(ftZ1, new Point(boxZ1.Left + 12, boxZ1.Top + 10));

                // 2. CHÚ THÍCH Z2: 2.07 (Cao độ đáy Hạ lưu)
                double y_Z2 = h * 0.86;
                Rect boxZ2 = new Rect(w + 40, y_Z2 - 35, 380, 100);
                dc.DrawRoundedRectangle(cardBg, new Pen(greenBrush, 2.0), boxZ2, 8, 8);
                dc.DrawLine(new Pen(greenBrush, 2.0), new Point(w * 0.56 + 20, y_Z1), new Point(boxZ2.Left, boxZ2.Top + 30));
                dc.DrawEllipse(greenBrush, null, new Point(w * 0.56 + 20, y_Z1), 6, 6);

                var ftZ2 = new FormattedText(
                    "🟢 Z2 (ĐÁY HẠ LƯU) = 2.07 m\n• Vị trí: Km 1+378.13 (Cuối cống C1)\n• Hàng màu xanh lá: Cao độ đáy cống\n➔ ĐIỀN CON SỐ 2.07 VÀO CỘT Z2",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    fontTypeface,
                    12.5,
                    whiteBrush,
                    1.25);
                dc.DrawText(ftZ2, new Point(boxZ2.Left + 12, boxZ2.Top + 10));

                // 3. CHÚ THÍCH CHIỀU DÀI & LÝ TRÌNH (8.13m, Km1+370.00)
                double y_L = h * 0.94;
                Rect boxL = new Rect(w + 40, y_L - 60, 380, 95);
                dc.DrawRoundedRectangle(cardBg, new Pen(cyanBrush, 1.5), boxL, 8, 8);
                dc.DrawLine(new Pen(cyanBrush, 1.5), new Point(w * 0.48 + 20, h * 0.83), new Point(boxL.Left, boxL.Top + 30));

                var ftL = new FormattedText(
                    "ℹ CHIỀU DÀI CỐNG L = 8.13 m\n• Đầu cống: Km 1+370.00 (Tên: C1)\n• Cuối cống: Km 1+378.13 (1370 + 8.13)\n• Khớp 100% với THONG KE CONG.xlsx",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    fontTypeface,
                    12,
                    yellowBrush,
                    1.25);
                dc.DrawText(ftL, new Point(boxL.Left + 12, boxL.Top + 10));

                // 4. CHÚ THÍCH CAO ĐỘ THIẾT KẾ MẶT ĐƯỜNG (4.36 / 4.34)
                double y_TK = h * 0.55;
                Rect boxTK = new Rect(w + 40, y_TK - 40, 380, 85);
                dc.DrawRoundedRectangle(cardBg, new Pen(yellowBrush, 1.5), boxTK, 8, 8);
                dc.DrawLine(new Pen(yellowBrush, 1.5), new Point(w * 0.42 + 20, y_TK + 15), new Point(boxTK.Left, boxTK.Top + 30));

                var ftTK = new FormattedText(
                    "📐 HÀNG MÀU VÀNG: CAO ĐỘ MẶT ĐƯỜNG\n• Cọc 1370.00: 4.36 m\n• Cọc 1378.13: 4.34 m\n(Đây là cao độ mặt đường, KHÔNG PHẢI đáy cống)",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    fontTypeface,
                    11.5,
                    new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    1.25);
                dc.DrawText(ftTK, new Point(boxTK.Left + 12, boxTK.Top + 10));
            }

            var rtb = new RenderTargetBitmap(outW, outH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = new FileStream(outputImg, FileMode.Create, FileAccess.Write))
            {
                enc.Save(fs);
            }
            Console.WriteLine($"[SUCCESS] Đã tạo ảnh chú thích trắc dọc tại: {outputImg}");
        }

        static void CreateElevationWorkbook(string baseDir)
        {
            string cadDir = Path.Combine(baseDir, @"CAD\Cap Bim");
            var csvFiles = Directory.GetFiles(cadDir, "*.csv");
            string csvPath = csvFiles.FirstOrDefault(f => Path.GetFileName(f).Contains("TOA_DO_CONG")) 
                          ?? Path.Combine(cadDir, "TOA_DO_CONG_300902026.csv");

            string outExcel = Path.Combine(cadDir, "TOA_DO_CONG_300902026.xlsx");

            // Sao lưu sang TOA_DO_CONG.csv
            string stdCsv = Path.Combine(cadDir, "TOA_DO_CONG.csv");
            if (File.Exists(csvPath) && csvPath != stdCsv)
            {
                File.Copy(csvPath, stdCsv, true);
            }

            Console.WriteLine($"=== TẠO FILE EXCEL TỌA ĐỘ VÀ SHEET NHẬP CAO ĐỘ ===");
            using var wb = new XLWorkbook();

            // SHEET 1: TỌA ĐỘ TỪ CAD
            var ws1 = wb.Worksheets.Add("Toa_Do_CAD");
            Console.WriteLine($"[INFO] Đọc file CSV: {csvPath} (Exists={File.Exists(csvPath)})");
            if (File.Exists(csvPath))
            {
                var lines = File.ReadAllLines(csvPath);
                Console.WriteLine($"[INFO] Số dòng trong CSV: {lines.Length}");
                for (int r = 0; r < lines.Length; r++)
                {
                    if (string.IsNullOrWhiteSpace(lines[r])) continue;
                    var parts = lines[r].Split(',');
                    for (int c = 0; c < parts.Length; c++)
                    {
                        var cell = ws1.Cell(r + 1, c + 1);
                        string val = parts[c].Trim();
                        if (r == 0)
                        {
                            cell.Value = val;
                            cell.Style.Font.Bold = true;
                            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0284C7");
                            cell.Style.Font.FontColor = XLColor.White;
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }
                        else
                        {
                            if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
                            {
                                cell.Value = num;
                                if (c >= 2 && c <= 5) cell.Style.NumberFormat.Format = "#,##0.000";
                                else cell.Style.NumberFormat.Format = "#,##0.00";
                                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                            }
                            else
                            {
                                cell.Value = val;
                                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                            }
                        }
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
                    }
                }
                ws1.SheetView.FreezeRows(1);
                ws1.Columns().AdjustToContents(12.0, 30.0);
            }

            // SHEET 2: NHẬP CAO ĐỘ CỐNG (CHỈ 4 CỘT THEO YÊU CẦU: STT, Lý trình, Z1, Z2)
            var ws2 = wb.Worksheets.Add("Nhap_Cao_Do");

            string[] headers2 = new[] { "STT", "Lý trình", "Z1", "Z2" };

            for (int c = 0; c < headers2.Length; c++)
            {
                var cell = ws2.Cell(1, c + 1);
                cell.Value = headers2[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 12;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#0F172A");

                if (c >= 2)
                {
                    // Cột Z1, Z2 highlight cam rực rỡ
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F59E0B"); // Amber/Orange
                    cell.Style.Font.FontColor = XLColor.White;
                }
                else
                {
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F766E"); // Teal
                    cell.Style.Font.FontColor = XLColor.White;
                }
            }
            ws2.Row(1).Height = 30;

            // 8 cống trên tuyến
            var rowsData = new[]
            {
                new { STT = 1, LyTrinh = "Km 1+370.00", Z1 = (double?)2.35, Z2 = (double?)2.07 },
                new { STT = 2, LyTrinh = "Km 1+817.00", Z1 = (double?)null, Z2 = (double?)null },
                new { STT = 3, LyTrinh = "Km 1+910.00", Z1 = (double?)null, Z2 = (double?)null },
                new { STT = 4, LyTrinh = "Km 2+105.00", Z1 = (double?)null, Z2 = (double?)null },
                new { STT = 5, LyTrinh = "Km 2+595.00", Z1 = (double?)null, Z2 = (double?)null },
                new { STT = 6, LyTrinh = "Km 2+975.00", Z1 = (double?)null, Z2 = (double?)null },
                new { STT = 7, LyTrinh = "Km 3+170.00", Z1 = (double?)null, Z2 = (double?)null },
                new { STT = 8, LyTrinh = "Km 3+308.00", Z1 = (double?)null, Z2 = (double?)null },
            };

            for (int i = 0; i < rowsData.Length; i++)
            {
                var r = rowsData[i];
                int rowIdx = i + 2;

                ws2.Cell(rowIdx, 1).Value = r.STT;
                ws2.Cell(rowIdx, 2).Value = r.LyTrinh;

                // Z1
                var cellZ1 = ws2.Cell(rowIdx, 3);
                if (r.Z1.HasValue) cellZ1.Value = r.Z1.Value;
                cellZ1.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7"); // Vàng nhạt
                cellZ1.Style.Font.Bold = true;
                cellZ1.Style.NumberFormat.Format = "#,##0.000";
                cellZ1.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                cellZ1.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                cellZ1.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D97706");

                // Z2
                var cellZ2 = ws2.Cell(rowIdx, 4);
                if (r.Z2.HasValue) cellZ2.Value = r.Z2.Value;
                cellZ2.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7"); // Vàng nhạt
                cellZ2.Style.Font.Bold = true;
                cellZ2.Style.NumberFormat.Format = "#,##0.000";
                cellZ2.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                cellZ2.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                cellZ2.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D97706");

                ws2.Cell(rowIdx, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws2.Cell(rowIdx, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws2.Range(rowIdx, 1, rowIdx, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws2.Range(rowIdx, 1, rowIdx, 4).Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
                ws2.Row(rowIdx).Height = 26;
            }

            ws2.SheetView.FreezeRows(1);
            ws2.Columns().AdjustToContents(15.0, 30.0);

            wb.SaveAs(outExcel);
            Console.WriteLine($"[SUCCESS] Đã tạo file Excel với 2 sheet tại: {outExcel}");
        }

        static void CreateToolExcel(string baseDir)
        {
            string cadDir = Path.Combine(baseDir, @"CAD\Cap Bim");
            string sourceExcel = Path.Combine(cadDir, "TOA_DO_CONG_300902026.xlsx");
            string toolExcel = Path.Combine(cadDir, "DU_LIEU_CONG_NAP_TOOL.xlsx");

            if (!File.Exists(sourceExcel))
            {
                Console.WriteLine($"[ERROR] Không tìm thấy file: {sourceExcel}");
                return;
            }

            Console.WriteLine($"=== TẠO FILE EXCEL NẠP TOOL LINK DỮ LIỆU TỰ ĐỘNG ===");

            using var wb = new XLWorkbook(sourceExcel);

            // Kiểm tra các sheet nguồn
            var wsToaDo = wb.Worksheet("Toa_Do_CAD");
            var wsCaoDo = wb.Worksheet("Nhap_Cao_Do");

            // Tạo hoặc cập nhật sheet DuLieuCongNgang
            IXLWorksheet wsTool;
            if (wb.Worksheets.Contains("DuLieuCongNgang"))
            {
                wb.Worksheet("DuLieuCongNgang").Delete();
            }
            wsTool = wb.Worksheets.Add("DuLieuCongNgang", 1); // Đặt làm Sheet 1

            string[] headers = new[]
            {
                "STT", "LyTrinh", "LoaiCong", "SoCua", "KhauDo",
                "X1", "Y1", "Z1", "X2", "Y2", "Z2",
                "ChieuDai", "DoDoc", "GocXoay", "SoHopNoi",
                "KC_HN1", "KC_HN2", "L_Ngam_San", "Khe_Ho_HN"
            };

            for (int col = 0; col < headers.Length; col++)
            {
                var cell = wsTool.Cell(1, col + 1);
                cell.Value = headers[col];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 11;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0284C7"); // Sky blue
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#0369A1");
            }
            wsTool.Row(1).Height = 28;

            int lastRow = wsCaoDo.LastRowUsed()?.RowNumber() ?? 8;
            Console.WriteLine($"[INFO] Số hàng cần link: {lastRow - 1}");

            for (int r = 2; r <= lastRow; r++)
            {
                int stt = r - 1;
                string lyTrinh = wsCaoDo.Cell(r, 2).GetString().Trim();
                if (string.IsNullOrEmpty(lyTrinh)) continue;

                // Xác định khẩu độ và số cửa theo TVTK
                string khauDo = "1.5x1.5";
                int soCua = 1;
                if (stt == 6)
                {
                    khauDo = "3.0x2.0";
                    soCua = 2; // Cống đôi 2 cửa Km 2+975.00
                }
                else if (stt == 7)
                {
                    khauDo = "3.0x2.0";
                    soCua = 1; // Cống đơn 3.0x2.0 Km 3+170.00
                }

                // Cột 1: STT (Link sang Nhap_Cao_Do)
                wsTool.Cell(r, 1).FormulaA1 = $"=Nhap_Cao_Do!A{r}";
                wsTool.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Cột 2: LyTrinh (Link sang Nhap_Cao_Do)
                wsTool.Cell(r, 2).FormulaA1 = $"=Nhap_Cao_Do!B{r}";
                wsTool.Cell(r, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Cột 3: LoaiCong
                wsTool.Cell(r, 3).Value = "CONG_HOP";
                wsTool.Cell(r, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Cột 4: SoCua
                wsTool.Cell(r, 4).Value = soCua;
                wsTool.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Cột 5: KhauDo
                wsTool.Cell(r, 5).Value = khauDo;
                wsTool.Cell(r, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Cột 6: X1 (Link sang Toa_Do_CAD cột C)
                wsTool.Cell(r, 6).FormulaA1 = $"=Toa_Do_CAD!C{r}";
                wsTool.Cell(r, 6).Style.NumberFormat.Format = "#,##0.000";
                wsTool.Cell(r, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Cột 7: Y1 (Link sang Toa_Do_CAD cột D)
                wsTool.Cell(r, 7).FormulaA1 = $"=Toa_Do_CAD!D{r}";
                wsTool.Cell(r, 7).Style.NumberFormat.Format = "#,##0.000";
                wsTool.Cell(r, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Cột 8: Z1 (Link sang Nhap_Cao_Do cột C)
                wsTool.Cell(r, 8).FormulaA1 = $"=Nhap_Cao_Do!C{r}";
                wsTool.Cell(r, 8).Style.NumberFormat.Format = "#,##0.000";
                wsTool.Cell(r, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                wsTool.Cell(r, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7"); // Highlight vàng để dễ nhận biết

                // Cột 9: X2 (Link sang Toa_Do_CAD cột E)
                wsTool.Cell(r, 9).FormulaA1 = $"=Toa_Do_CAD!E{r}";
                wsTool.Cell(r, 9).Style.NumberFormat.Format = "#,##0.000";
                wsTool.Cell(r, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Cột 10: Y2 (Link sang Toa_Do_CAD cột F)
                wsTool.Cell(r, 10).FormulaA1 = $"=Toa_Do_CAD!F{r}";
                wsTool.Cell(r, 10).Style.NumberFormat.Format = "#,##0.000";
                wsTool.Cell(r, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Cột 11: Z2 (Link sang Nhap_Cao_Do cột D)
                wsTool.Cell(r, 11).FormulaA1 = $"=Nhap_Cao_Do!D{r}";
                wsTool.Cell(r, 11).Style.NumberFormat.Format = "#,##0.000";
                wsTool.Cell(r, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                wsTool.Cell(r, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7");

                // Cột 12: ChieuDai (Link sang Toa_Do_CAD cột G)
                wsTool.Cell(r, 12).FormulaA1 = $"=Toa_Do_CAD!G{r}";
                wsTool.Cell(r, 12).Style.NumberFormat.Format = "#,##0.00";
                wsTool.Cell(r, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Cột 13: DoDoc (Tính từ Z1, Z2, ChieuDai)
                wsTool.Cell(r, 13).FormulaA1 = $"=IF(OR(ISBLANK(H{r}),ISBLANK(K{r})),0,ROUND(ABS(H{r}-K{r})/L{r}*100,2))";
                wsTool.Cell(r, 13).Style.NumberFormat.Format = "#,##0.00";
                wsTool.Cell(r, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                wsTool.Cell(r, 13).Style.Font.Bold = true;
                wsTool.Cell(r, 13).Style.Font.FontColor = XLColor.FromHtml("#047857");

                // Cột 14: GocXoay (Link sang Toa_Do_CAD cột H)
                wsTool.Cell(r, 14).FormulaA1 = $"=Toa_Do_CAD!H{r}";
                wsTool.Cell(r, 14).Style.NumberFormat.Format = "#,##0.00";
                wsTool.Cell(r, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Cột 15: SoHopNoi
                wsTool.Cell(r, 15).Value = 0;
                wsTool.Cell(r, 15).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Cột 16: KC_HN1
                wsTool.Cell(r, 16).Value = 0.00;
                wsTool.Cell(r, 16).Style.NumberFormat.Format = "#,##0.00";
                wsTool.Cell(r, 16).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Cột 17: KC_HN2
                wsTool.Cell(r, 17).Value = 0.00;
                wsTool.Cell(r, 17).Style.NumberFormat.Format = "#,##0.00";
                wsTool.Cell(r, 17).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Cột 18: L_Ngam_San
                wsTool.Cell(r, 18).Value = 0.30;
                wsTool.Cell(r, 18).Style.NumberFormat.Format = "#,##0.00";
                wsTool.Cell(r, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Cột 19: Khe_Ho_HN
                wsTool.Cell(r, 19).Value = 0.05;
                wsTool.Cell(r, 19).Style.NumberFormat.Format = "#,##0.00";
                wsTool.Cell(r, 19).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                wsTool.Range(r, 1, r, 19).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                wsTool.Range(r, 1, r, 19).Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
                wsTool.Row(r).Height = 24;
            }

            wsTool.SheetView.FreezeRows(1);
            wsTool.Columns().AdjustToContents(12.0, 30.0);

            // Tự động tính toán lại các công thức trong workbook
            wb.RecalculateAllFormulas();

            // 1. Lưu lại file TOA_DO_CONG_300902026.xlsx (Có thêm Sheet 1 DuLieuCongNgang được link tự động)
            wb.SaveAs(sourceExcel);
            Console.WriteLine($"[SUCCESS] Đã cập nhật Sheet 1 link công thức vào file: {sourceExcel}");

            // 2. Lưu file riêng biệt DU_LIEU_CONG_NAP_TOOL.xlsx
            wb.SaveAs(toolExcel);
            Console.WriteLine($"[SUCCESS] Đã tạo file riêng biệt nạp tool tại: {toolExcel}");
        }

        static void InspectLapGhep(string baseDir)
        {
            string dwgPath = Path.Combine(baseDir, @"CAD\Cap Bim\03. CONG HOP LAP GHEP.dwg");
            Console.WriteLine($"=== REVIEW FILE: {Path.GetFileName(dwgPath)} ===");

            if (!File.Exists(dwgPath))
            {
                Console.WriteLine($"[ERROR] Không tìm thấy file: {dwgPath}");
                return;
            }

            ScanBinaryDwgStrings(dwgPath);
        }

        static void ExtractTextsFromAcadDoc(dynamic doc)
        {
            var textItems = new System.Collections.Generic.List<string>();

            // Quét ModelSpace
            try
            {
                dynamic ms = doc.ModelSpace;
                int count = ms.Count;
                Console.WriteLine($"[INFO] ModelSpace có {count} đối tượng.");
                for (int i = 0; i < count; i++)
                {
                    dynamic ent = ms.Item(i);
                    string entType = ent.ObjectName;
                    if (entType == "AcDbText" || entType == "AcDbMText")
                    {
                        string txt = ent.TextString;
                        if (!string.IsNullOrWhiteSpace(txt)) textItems.Add(txt);
                    }
                    else if (entType == "AcDbTable")
                    {
                        int rows = ent.Rows;
                        int cols = ent.Columns;
                        for (int r = 0; r < rows; r++)
                        {
                            var rowVals = new System.Collections.Generic.List<string>();
                            for (int c = 0; c < cols; c++)
                            {
                                try
                                {
                                    string cellText = ent.GetCellValue(r, c)?.ToString() ?? ent.GetText(r, c);
                                    if (!string.IsNullOrWhiteSpace(cellText)) rowVals.Add(cellText.Trim());
                                }
                                catch {}
                            }
                            if (rowVals.Count > 0)
                            {
                                textItems.Add("[BẢNG] " + string.Join(" | ", rowVals));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERR ModelSpace] {ex.Message}");
            }

            // Quét PaperSpace / Layouts
            try
            {
                dynamic layouts = doc.Layouts;
                for (int l = 0; l < layouts.Count; l++)
                {
                    dynamic layout = layouts.Item(l);
                    string lName = layout.Name;
                    if (lName.Equals("Model", StringComparison.OrdinalIgnoreCase)) continue;

                    dynamic block = layout.Block;
                    for (int i = 0; i < block.Count; i++)
                    {
                        dynamic ent = block.Item(i);
                        string entType = ent.ObjectName;
                        if (entType == "AcDbText" || entType == "AcDbMText")
                        {
                            string txt = ent.TextString;
                            if (!string.IsNullOrWhiteSpace(txt)) textItems.Add(txt);
                        }
                        else if (entType == "AcDbTable")
                        {
                            int rows = ent.Rows;
                            int cols = ent.Columns;
                            for (int r = 0; r < rows; r++)
                            {
                                var rowVals = new System.Collections.Generic.List<string>();
                                for (int c = 0; c < cols; c++)
                                {
                                    try
                                    {
                                        string cellText = ent.GetCellValue(r, c)?.ToString() ?? ent.GetText(r, c);
                                        if (!string.IsNullOrWhiteSpace(cellText)) rowVals.Add(cellText.Trim());
                                    }
                                    catch {}
                                }
                                if (rowVals.Count > 0)
                                {
                                    textItems.Add($"[{lName} BẢNG] " + string.Join(" | ", rowVals));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERR Layouts] {ex.Message}");
            }

            Console.WriteLine($"\n=== TÌM THẤY {textItems.Count} TEXT TRONG BẢN VẼ ===");
            // In các text liên quan đến cống, lý trình, chiều dài, đốt cống
            foreach (var t in textItems)
            {
                string clean = CleanMText(t);
                if (clean.IndexOf("Km", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clean.IndexOf("cống", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clean.IndexOf("cong", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clean.IndexOf("chiều dài", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clean.IndexOf("chieu dai", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clean.IndexOf("L =", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clean.IndexOf("L=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clean.IndexOf("khẩu độ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clean.IndexOf("150x150", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clean.IndexOf("đốt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clean.IndexOf("BẢNG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    clean.IndexOf("thống kê", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine(">> " + clean);
                }
            }
        }

        static string CleanMText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string s = raw;
            // Bỏ các tag định dạng MText như \A1;, \C1;, \fArial..., {\L...}
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\\[A-Za-z0-9]+;?", "");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\{|\}", "");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\\P", "\n");
            return s.Trim();
        }

        static void ScanBinaryDwgStrings(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            byte[] bytes = ms.ToArray();

            var list = new System.Collections.Generic.List<string>();
            // Quét UTF-16LE
            for (int i = 0; i < bytes.Length - 4; i += 2)
            {
                int len = 0;
                while (i + len + 1 < bytes.Length)
                {
                    char c = (char)(bytes[i + len] | (bytes[i + len + 1] << 8));
                    if (char.IsLetterOrDigit(c) || char.IsPunctuation(c) || c == ' ' || c == '+' || c == '=' || c == '-' || c == '/' || c == '%' || c == '\n')
                    {
                        len += 2;
                    }
                    else break;
                }
                if (len >= 8) // Ít nhất 4 ký tự
                {
                    string s = System.Text.Encoding.Unicode.GetString(bytes, i, len).Trim();
                    if (s.Length >= 4 && (s.Contains("Km") || s.Contains("cong") || s.Contains("L=") || s.Contains("150") || s.Contains("BẢNG")))
                    {
                        list.Add(s);
                    }
                    i += len;
                }
            }

            Console.WriteLine($"[BINARY SCAN] Tìm thấy {list.Count} chuỗi liên quan:");
            foreach (var item in list.Distinct().Take(40))
            {
                Console.WriteLine("  -- " + item);
            }
        }
    }
}
