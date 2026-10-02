using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using InfraBIM.CulvertTool.Models;

namespace InfraBIM.CulvertTool.Views
{
    /// <summary>
    /// Canvas đồ họa vector trực quan hiển thị bản vẽ mô phỏng 2D Mặt cắt dọc, Mặt bằng & Phối cảnh cống
    /// </summary>
    public class CulvertPreviewCanvas : FrameworkElement
    {
        #region Dependency Properties
        public static readonly DependencyProperty GeometryDataProperty =
            DependencyProperty.Register(
                nameof(GeometryData),
                typeof(CulvertPreviewGeometry),
                typeof(CulvertPreviewCanvas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryDataChanged));

        public static readonly DependencyProperty ViewModeProperty =
            DependencyProperty.Register(
                nameof(ViewMode),
                typeof(PreviewViewMode),
                typeof(CulvertPreviewCanvas),
                new FrameworkPropertyMetadata(PreviewViewMode.Profile2D, FrameworkPropertyMetadataOptions.AffectsRender));

        public CulvertPreviewGeometry? GeometryData
        {
            get => (CulvertPreviewGeometry?)GetValue(GeometryDataProperty);
            set => SetValue(GeometryDataProperty, value);
        }

        public PreviewViewMode ViewMode
        {
            get => (PreviewViewMode)GetValue(ViewModeProperty);
            set => SetValue(ViewModeProperty, value);
        }

        private static void OnGeometryDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CulvertPreviewCanvas canvas)
            {
                canvas.FitView();
            }
        }
        #endregion

        #region Interaction Fields (Pan & Zoom)
        private double _zoom = 1.0;
        private Point _panOffset = new Point(0, 0);
        private Point _lastMousePos;
        private bool _isDragging = false;
        #endregion

        #region Brushes & Pens
        private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(10, 17, 32)); // #0A1120
        private static readonly Brush GridPenBrush = new SolidColorBrush(Color.FromArgb(35, 56, 189, 248)); // #38BDF8 low alpha
        private static readonly Pen GridPen = new Pen(GridPenBrush, 0.8);

        private static readonly Brush StdSegBrush = new SolidColorBrush(Color.FromArgb(220, 22, 45, 78)); // Deep Navy
        private static readonly Pen StdSegPen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.5); // Cyan outline

        private static readonly Brush CompSegBrush = new SolidColorBrush(Color.FromArgb(230, 22, 101, 74)); // Mint Dark
        private static readonly Pen CompSegPen = new Pen(new SolidColorBrush(Color.FromRgb(134, 239, 172)), 1.8); // Mint outline

        private static readonly Brush WarnSegBrush = new SolidColorBrush(Color.FromArgb(230, 153, 27, 27)); // Red Dark
        private static readonly Pen WarnSegPen = new Pen(new SolidColorBrush(Color.FromRgb(248, 113, 113)), 2.0); // Red outline

        private static readonly Brush ApronBrush = new SolidColorBrush(Color.FromArgb(230, 39, 64, 102));
        private static readonly Pen ApronPen = new Pen(new SolidColorBrush(Color.FromRgb(96, 165, 250)), 1.5);

        private static readonly Brush ManholeBrush = new SolidColorBrush(Color.FromArgb(240, 71, 85, 105));
        private static readonly Pen ManholePen = new Pen(new SolidColorBrush(Color.FromRgb(148, 163, 184)), 1.5);

        private static readonly Brush BtlBrush = new SolidColorBrush(Color.FromArgb(180, 51, 65, 85)); // Xám bê tông lót
        private static readonly Brush CatBrush = new SolidColorBrush(Color.FromArgb(160, 217, 119, 6)); // Vàng cát

        private static readonly Pen DimPen = new Pen(new SolidColorBrush(Color.FromRgb(148, 163, 184)), 1.0);
        private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
        private static readonly Brush CyanTextBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        private static readonly Brush MintTextBrush = new SolidColorBrush(Color.FromRgb(134, 239, 172));
        private static readonly Brush YellowTextBrush = new SolidColorBrush(Color.FromRgb(253, 224, 71));
        private static readonly Brush RedTextBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113));
        #endregion

        public CulvertPreviewCanvas()
        {
            ClipToBounds = true;
            Focusable = true;
            Loaded += (s, e) => FitView();
            SizeChanged += (s, e) => FitView();

            MouseWheel += OnMouseWheel;
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseLeave += (s, e) => _isDragging = false;
        }

        #region Public Zoom / Pan Methods
        public void ZoomIn()
        {
            _zoom = Math.Min(_zoom * 1.25, 10.0);
            InvalidateVisual();
        }

        public void ZoomOut()
        {
            _zoom = Math.Max(_zoom / 1.25, 0.2);
            InvalidateVisual();
        }

        public void FitView()
        {
            _zoom = 1.0;
            _panOffset = new Point(0, 0);
            InvalidateVisual();
        }
        #endregion

        #region Mouse Event Handlers
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point mousePos = e.GetPosition(this);
            double zoomFactor = e.Delta > 0 ? 1.15 : 0.87;
            double newZoom = Math.Clamp(_zoom * zoomFactor, 0.2, 10.0);

            // Zoom quanh vị trí chuột
            _panOffset.X = mousePos.X - (mousePos.X - _panOffset.X) * (newZoom / _zoom);
            _panOffset.Y = mousePos.Y - (mousePos.Y - _panOffset.Y) * (newZoom / _zoom);
            _zoom = newZoom;

            InvalidateVisual();
            e.Handled = true;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Middle)
            {
                _isDragging = true;
                _lastMousePos = e.GetPosition(this);
                CaptureMouse();
                if (e.ClickCount == 2)
                {
                    FitView();
                }
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point cur = e.GetPosition(this);
                Vector delta = cur - _lastMousePos;
                _panOffset.X += delta.X;
                _panOffset.Y += delta.Y;
                _lastMousePos = cur;
                InvalidateVisual();
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
        }
        #endregion

        #region Rendering Pipeline
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 10 || h < 10) return;

            // 1. Vẽ nền Canvas
            dc.DrawRectangle(BgBrush, null, new Rect(0, 0, w, h));

            // 2. Vẽ lưới kỹ thuật (Engineering Grid)
            DrawGrid(dc, w, h);

            if (GeometryData == null || GeometryData.TotalLengthM <= 0.1)
            {
                DrawEmptyNotice(dc, w, h);
                return;
            }

            // 3. Áp dụng biến đổi Pan & Zoom
            dc.PushTransform(new MatrixTransform(_zoom, 0, 0, _zoom, _panOffset.X, _panOffset.Y));

            switch (ViewMode)
            {
                case PreviewViewMode.Profile2D:
                    RenderProfile2D(dc, w, h);
                    break;
                case PreviewViewMode.Plan2D:
                    RenderPlan2D(dc, w, h);
                    break;
                case PreviewViewMode.Isometric3D:
                    RenderIsometric3D(dc, w, h);
                    break;
            }

            dc.Pop();

            // 4. Vẽ la bàn / góc nhìn cố định ở góc trên
            DrawHeaderWatermark(dc, w, h);
        }

        private void DrawGrid(DrawingContext dc, double w, double h)
        {
            const double gridSize = 30.0;
            for (double x = 0; x < w; x += gridSize)
            {
                dc.DrawLine(GridPen, new Point(x, 0), new Point(x, h));
            }
            for (double y = 0; y < h; y += gridSize)
            {
                dc.DrawLine(GridPen, new Point(0, y), new Point(w, y));
            }
        }

        private void DrawEmptyNotice(DrawingContext dc, double w, double h)
        {
            var text = new FormattedText(
                "Chưa có dữ liệu cống để mô phỏng.\nVui lòng chọn một cống trong danh sách bên trái.",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                13.5,
                TextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                TextAlignment = TextAlignment.Center
            };
            dc.DrawText(text, new Point(w / 2.0, h / 2.0 - 20));
        }

        private void DrawHeaderWatermark(DrawingContext dc, double w, double h)
        {
            string modeName = ViewMode switch
            {
                PreviewViewMode.Profile2D => "MẶT CẮT DỌC CỐNG (LONGITUDINAL PROFILE)",
                PreviewViewMode.Plan2D => "MẶT BẰNG CỐNG (PLAN VIEW)",
                PreviewViewMode.Isometric3D => "PHỐI CẢNH 3D (AXONOMETRIC 3D)",
                _ => ""
            };

            var text = new FormattedText(
                $"BẢN VẼ: {modeName}  |  TỈ LỆ ZOOM: {_zoom:P0}",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                11.5,
                CyanTextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 15, 23, 42)), null, new Rect(12, 10, text.Width + 16, 24));
            dc.DrawText(text, new Point(20, 14));
        }
        #endregion

        #region Mode 1: Render Profile 2D (Mặt Cắt Dọc Kèm Dimension CAD)
        private void RenderProfile2D(DrawingContext dc, double viewW, double viewH)
        {
            var geom = GeometryData!;
            double totalL = geom.TotalLengthM;
            if (totalL <= 0.1) return;

            // Tính scale vẽ sao cho cống vừa vặn trong khung hình
            double marginX = 80;
            double marginY = 80;
            double availableW = Math.Max(viewW - (2 * marginX), 200);
            double availableH = Math.Max(viewH - (2 * marginY), 150);

            // Scale mét sang pixel
            double scaleX = availableW / (totalL + 4.0); // Chừa 2m mỗi bên cho sân cống
            double scaleY = Math.Min(scaleX, 40.0); // Giới hạn chiều đứng để cống không bị kéo méo quá

            double startX = marginX + (2.0 * scaleX);
            double centerY = viewH / 2.0;

            // Tính độ dốc trực quan (giới hạn độ lệch vẽ để dễ nhìn)
            double visualSlope = Math.Clamp(geom.DoDocPercent / 100.0, -0.15, 0.15);
            double culvertH = Math.Max(geom.BarrelHeightM, 1.0) * scaleY;

            // Hàm chuyển đổi (distanceM, relZ) sang tọa độ Canvas (X, Y)
            Point MapPoint(double distM, double offsetZM = 0)
            {
                double px = startX + (distM * scaleX);
                double py = centerY + (distM * visualSlope * scaleX) - (offsetZM * scaleY);
                return new Point(px, py);
            }

            // 1. VẼ LỚP ĐỆM CÁT / ĐÁ DĂM (DƯỚI CÙNG)
            double catThick = 0.20;
            double btlThick = 0.10;
            Point pCat1 = MapPoint(-2.5, -culvertH / scaleY - btlThick - catThick);
            Point pCat2 = MapPoint(totalL + 2.5, -culvertH / scaleY - btlThick - catThick);
            Point pCat3 = MapPoint(totalL + 2.5, -culvertH / scaleY - btlThick);
            Point pCat4 = MapPoint(-2.5, -culvertH / scaleY - btlThick);

            var catPath = new PathGeometry();
            var catFig = new PathFigure { StartPoint = pCat1, IsClosed = true };
            catFig.Segments.Add(new LineSegment(pCat2, true));
            catFig.Segments.Add(new LineSegment(pCat3, true));
            catFig.Segments.Add(new LineSegment(pCat4, true));
            catPath.Figures.Add(catFig);
            dc.DrawGeometry(CatBrush, new Pen(new SolidColorBrush(Color.FromRgb(217, 119, 6)), 1.0), catPath);
            DrawCenteredText(dc, "LỚP ĐÁ DĂM ĐỆM / CPDD", new Point((pCat1.X + pCat2.X) / 2.0, pCat1.Y + 8), 9.0, new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)));

            // 2. VẼ LỚP BÊ TÔNG LÓT (BTL) ĐỐT CỐNG (Tự động ngắt quãng trước hố thu)
            if (geom.HasBTL_Dot)
            {
                var btlPen = new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1.0);

                void DrawBtlBlock(double startM, double endM, double offsetZM)
                {
                    if (endM <= startM) return;
                    Point pb1 = MapPoint(startM, -culvertH / scaleY + offsetZM - btlThick);
                    Point pb2 = MapPoint(endM, -culvertH / scaleY + offsetZM - btlThick);
                    Point pb3 = MapPoint(endM, -culvertH / scaleY + offsetZM);
                    Point pb4 = MapPoint(startM, -culvertH / scaleY + offsetZM);

                    var path = new PathGeometry();
                    var fig = new PathFigure { StartPoint = pb1, IsClosed = true };
                    fig.Segments.Add(new LineSegment(pb2, true));
                    fig.Segments.Add(new LineSegment(pb3, true));
                    fig.Segments.Add(new LineSegment(pb4, true));
                    path.Figures.Add(fig);
                    dc.DrawGeometry(BtlBrush, btlPen, path);
                }

                if (geom.Manholes.Count == 0)
                {
                    DrawBtlBlock(0.0, totalL, geom.OffsetZ_BTL_Dot);
                }
                else
                {
                    double prevX = 0.0;
                    foreach (var mh in geom.Manholes)
                    {
                        double mhLeft = mh.DistanceFromP1M - (mh.WidthM / 2.0);
                        if (mhLeft > prevX)
                        {
                            DrawBtlBlock(prevX, mhLeft, geom.OffsetZ_BTL_Dot);
                        }
                        prevX = mh.DistanceFromP1M + (mh.WidthM / 2.0);
                    }
                    if (totalL > prevX)
                    {
                        DrawBtlBlock(prevX, totalL, geom.OffsetZ_BTL_Dot);
                    }
                }
            }

            // 3. NẾU LÀ CỐNG TRÒN: VẼ CÁC GỐI CỐNG & MÓNG TRÊN
            if (geom.IsRoundCulvert)
            {
                var goiBrush = new SolidColorBrush(Color.FromArgb(220, 100, 116, 139));
                var goiPen = new Pen(new SolidColorBrush(Color.FromRgb(148, 163, 184)), 1.0);
                double goiWidthM = 0.40;
                double goiHeightM = 0.20;

                foreach (var seg in geom.Segments)
                {
                    double centerM = (seg.StartDistanceM + seg.EndDistanceM) / 2.0;
                    Point pGoi1 = MapPoint(centerM - goiWidthM / 2.0, -culvertH / scaleY - goiHeightM);
                    Point pGoi2 = MapPoint(centerM + goiWidthM / 2.0, -culvertH / scaleY - goiHeightM);
                    Point pGoi3 = MapPoint(centerM + goiWidthM / 2.0, -culvertH / scaleY);
                    Point pGoi4 = MapPoint(centerM - goiWidthM / 2.0, -culvertH / scaleY);

                    var gPath = new PathGeometry();
                    var gFig = new PathFigure { StartPoint = pGoi1, IsClosed = true };
                    gFig.Segments.Add(new LineSegment(pGoi2, true));
                    gFig.Segments.Add(new LineSegment(pGoi3, true));
                    gFig.Segments.Add(new LineSegment(pGoi4, true));
                    gPath.Figures.Add(gFig);
                    dc.DrawGeometry(goiBrush, goiPen, gPath);
                }
            }

            // 4. VẼ CỬA XẢ & SÂN GIA CỐ THƯỢNG LƯU (P1, Z1)
            // Sân gia cố kéo dài ra ngoài
            Point pSgcTL_Top = MapPoint(-3.0, 0.0);
            Point pSgcTL_Bot = MapPoint(-1.5, -culvertH / scaleY);
            Rect rSgcTL = new Rect(pSgcTL_Top.X, pSgcTL_Top.Y, Math.Max(pSgcTL_Bot.X - pSgcTL_Top.X, 25), culvertH);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 30, 58, 95)), new Pen(new SolidColorBrush(Color.FromRgb(96, 165, 250)), 1.2), rSgcTL);
            DrawCenteredText(dc, "SÂN GIA CỐ", new Point(rSgcTL.X + rSgcTL.Width / 2, rSgcTL.Y + rSgcTL.Height / 2), 8.5, MintTextBrush);

            // Cửa xả thượng lưu (Tường đầu + Tường cánh + Sân cống)
            Point pSanTL_Top = MapPoint(-1.5, 0.45);
            Point pSanTL_Bot = MapPoint(0, -culvertH / scaleY);
            Rect rSanTL = new Rect(pSanTL_Top.X, pSanTL_Top.Y, Math.Max(pSanTL_Bot.X - pSanTL_Top.X, 30), culvertH + (0.45 * scaleY));
            dc.DrawRectangle(ApronBrush, ApronPen, rSanTL);
            DrawCenteredText(dc, "CỬA XẢ TL\n(Tường đầu)", new Point(rSanTL.X + rSanTL.Width / 2, rSanTL.Y + rSanTL.Height / 2), 9.5, CyanTextBrush);

            // 5. VẼ CỬA XẢ & SÂN GIA CỐ HẠ LƯU (P2, Z2)
            // Cửa xả hạ lưu
            Point pSanHL_Top = MapPoint(totalL, 0.45);
            Point pSanHL_Bot = MapPoint(totalL + 1.5, -culvertH / scaleY);
            Rect rSanHL = new Rect(pSanHL_Top.X, pSanHL_Top.Y, Math.Max(pSanHL_Bot.X - pSanHL_Top.X, 30), culvertH + (0.45 * scaleY));
            dc.DrawRectangle(ApronBrush, ApronPen, rSanHL);
            DrawCenteredText(dc, "CỬA XẢ HL\n(Tường đầu)", new Point(rSanHL.X + rSanHL.Width / 2, rSanHL.Y + rSanHL.Height / 2), 9.5, CyanTextBrush);

            // Sân gia cố hạ lưu
            Point pSgcHL_Top = MapPoint(totalL + 1.5, 0.0);
            Point pSgcHL_Bot = MapPoint(totalL + 3.0, -culvertH / scaleY);
            Rect rSgcHL = new Rect(pSgcHL_Top.X, pSgcHL_Top.Y, Math.Max(pSgcHL_Bot.X - pSgcHL_Top.X, 25), culvertH);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, 30, 58, 95)), new Pen(new SolidColorBrush(Color.FromRgb(96, 165, 250)), 1.2), rSgcHL);
            DrawCenteredText(dc, "SÂN GIA CỐ", new Point(rSgcHL.X + rSgcHL.Width / 2, rSgcHL.Y + rSgcHL.Height / 2), 8.5, MintTextBrush);

            // 6. VẼ CÁC ĐỐT CỐNG (STANDARD & COMPENSATING & JOINTS)
            double wallThickM = 0.15;
            double wallThickPx = wallThickM * scaleY;
            var innerVoidBrush = new SolidColorBrush(Color.FromArgb(120, 15, 23, 42)); // Lòng cống rỗng bên trong

            foreach (var seg in geom.Segments)
            {
                Point pt1 = MapPoint(seg.StartDistanceM, 0);
                Point pt2 = MapPoint(seg.EndDistanceM, 0);
                Point pt3 = MapPoint(seg.EndDistanceM, -culvertH / scaleY);
                Point pt4 = MapPoint(seg.StartDistanceM, -culvertH / scaleY);

                var segPath = new PathGeometry();
                var segFig = new PathFigure { StartPoint = pt1, IsClosed = true };
                segFig.Segments.Add(new LineSegment(pt2, true));
                segFig.Segments.Add(new LineSegment(pt3, true));
                segFig.Segments.Add(new LineSegment(pt4, true));
                segPath.Figures.Add(segFig);

                Brush fill = seg.IsStandard ? StdSegBrush : CompSegBrush;
                Pen stroke = seg.IsStandard ? StdSegPen : CompSegPen;

                dc.DrawGeometry(fill, stroke, segPath);

                // Lòng cống rỗng bên trong
                Point pi1 = MapPoint(seg.StartDistanceM, -wallThickM);
                Point pi2 = MapPoint(seg.EndDistanceM, -wallThickM);
                Point pi3 = MapPoint(seg.EndDistanceM, -culvertH / scaleY + wallThickM);
                Point pi4 = MapPoint(seg.StartDistanceM, -culvertH / scaleY + wallThickM);

                var voidPath = new PathGeometry();
                var voidFig = new PathFigure { StartPoint = pi1, IsClosed = true };
                voidFig.Segments.Add(new LineSegment(pi2, true));
                voidFig.Segments.Add(new LineSegment(pi3, true));
                voidFig.Segments.Add(new LineSegment(pi4, true));
                voidPath.Figures.Add(voidFig);
                dc.DrawGeometry(innerVoidBrush, new Pen(new SolidColorBrush(Color.FromArgb(60, 56, 189, 248)), 0.8), voidPath);

                // Nhãn đốt cống
                Point mid = new Point((pt1.X + pt2.X) / 2.0, (pt1.Y + pt3.Y) / 2.0);
                string segLabel = seg.IsStandard ? $"Đ{seg.Index}\n{seg.LengthM:N2}m" : $"Đ.Bù\n{seg.LengthM:N2}m";
                Brush lblBrush = seg.IsStandard ? TextBrush : MintTextBrush;
                DrawCenteredText(dc, segLabel, mid, 10, lblBrush);

                // Đường phân cách mối nối giữa các đốt
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(250, 204, 21)), 1.5), pt2, pt3);
            }

            // 7. VẼ HỘP NỐI / HỐ GA (NẾU CÓ)
            foreach (var mh in geom.Manholes)
            {
                double leftM = mh.DistanceFromP1M - (mh.WidthM / 2.0);
                double rightM = mh.DistanceFromP1M + (mh.WidthM / 2.0);
                Point pMhTop = MapPoint(leftM, 0.7);
                Point pMhBot = MapPoint(rightM, -culvertH / scaleY - btlThick);

                Rect rMh = new Rect(pMhTop.X, pMhTop.Y, Math.Max(pMhBot.X - pMhTop.X, 28), culvertH + (0.7 * scaleY) + (btlThick * scaleY));
                dc.DrawRectangle(ManholeBrush, ManholePen, rMh);

                // Nắp đan hố ga ở đỉnh
                Rect rCap = new Rect(rMh.X, rMh.Y - 6, rMh.Width, 8);
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(203, 213, 225)), new Pen(new SolidColorBrush(Color.FromRgb(248, 250, 252)), 1.2), rCap);

                DrawCenteredText(dc, $"{mh.Title}\nB={mh.WidthM:N2}m", new Point(rMh.X + rMh.Width / 2, rMh.Y + rMh.Height / 2), 10, YellowTextBrush);
            }

            // 8. VẼ ĐƯỜNG GIÓNG KÍCH THƯỚC (DIMENSION LINES) CHUẨN CAD
            // a) Chuỗi kích thước chi tiết từng đốt ở TRÊN ĐỈNH CỐNG
            double dimTopY = centerY - (0.9 * scaleY) - 25;
            foreach (var seg in geom.Segments)
            {
                Point pt1 = MapPoint(seg.StartDistanceM);
                Point pt2 = MapPoint(seg.EndDistanceM);
                DrawCadDimension(dc, new Point(pt1.X, dimTopY), new Point(pt2.X, dimTopY), $"{seg.LengthM:N2}m", 10, seg.IsStandard ? TextBrush : MintTextBrush);
            }

            // b) Kích thước tổng chiều dài cống ở PHÍA DƯỚI ĐÁY CỐNG
            Point pTotalStart = MapPoint(0);
            Point pTotalEnd = MapPoint(totalL);
            double dimBotY = centerY + (visualSlope * totalL * scaleX) + culvertH + 35;
            DrawCadDimension(dc, new Point(pTotalStart.X, dimBotY), new Point(pTotalEnd.X, dimBotY), $"TỔNG CHIỀU DÀI L = {totalL:N2} m", 11.5, CyanTextBrush, true);

            // c) Khoảng cách vị trí các hố ga (nếu có)
            if (geom.Manholes.Count > 0)
            {
                double dimMhY = dimBotY + 25;
                double curX = 0.0;
                foreach (var mh in geom.Manholes)
                {
                    Point pFrom = MapPoint(curX);
                    Point pTo = MapPoint(mh.DistanceFromP1M);
                    double distSeg = mh.DistanceFromP1M - curX;
                    DrawCadDimension(dc, new Point(pFrom.X, dimMhY), new Point(pTo.X, dimMhY), $"{distSeg:N2}m", 9.5, YellowTextBrush);
                    curX = mh.DistanceFromP1M;
                }
                Point pLast = MapPoint(curX);
                Point pEnd = MapPoint(totalL);
                DrawCadDimension(dc, new Point(pLast.X, dimMhY), new Point(pEnd.X, dimMhY), $"{totalL - curX:N2}m", 9.5, YellowTextBrush);
            }

            // d) Ký hiệu cao độ mốc mực nước / đỉnh cống / đáy cống (Elevation Level Markers ∇)
            // Thượng lưu (P1)
            DrawElevationMarker(dc, MapPoint(0, 0), $"∇ Đỉnh Z={geom.Z_Top1:N3}m", true, isTop: true, customBrush: CyanTextBrush);
            DrawElevationMarker(dc, MapPoint(0, -culvertH / scaleY), $"∇ Đáy Z1={geom.Z1:N3}m", true, isTop: false, customBrush: YellowTextBrush);
            DrawElevationMarker(dc, MapPoint(0, -culvertH / scaleY + (geom.OffsetZ_BTL_Dot * scaleY)), $"∇ BTL={geom.Z_Bot_BTL1:N3}m", true, isTop: false, isSmall: true, customBrush: TextBrush);

            // Hạ lưu (P2)
            DrawElevationMarker(dc, MapPoint(totalL, 0), $"∇ Đỉnh Z={geom.Z_Top2:N3}m", false, isTop: true, customBrush: CyanTextBrush);
            DrawElevationMarker(dc, MapPoint(totalL, -culvertH / scaleY), $"∇ Đáy Z2={geom.Z2:N3}m", false, isTop: false, customBrush: YellowTextBrush);
            DrawElevationMarker(dc, MapPoint(totalL, -culvertH / scaleY + (geom.OffsetZ_BTL_Dot * scaleY)), $"∇ BTL={geom.Z_Bot_BTL2:N3}m", false, isTop: false, isSmall: true, customBrush: TextBrush);

            // Cao độ tại các Hố ga (nếu có)
            foreach (var mh in geom.Manholes)
            {
                DrawElevationMarker(dc, MapPoint(mh.DistanceFromP1M, -culvertH / scaleY - 0.20), $"∇ Đáy HG={mh.BottomElevationZ:N3}m", false, isTop: false, isSmall: true, customBrush: YellowTextBrush);
            }

            // e) Mũi tên độ dốc cống i (%)
            Point pSlopeMid = MapPoint(totalL / 2.0, 0.35);
            DrawSlopeIndicator(dc, pSlopeMid, geom.CalculatedSlopePercent > 0 ? geom.CalculatedSlopePercent : geom.DoDocPercent, geom.IsReverseSlope);

            // f) Đường dóng đối chiếu cao độ đáy (Datum Level Line)
            Point pDatumZ1 = MapPoint(0, -culvertH / scaleY);
            Point pDatumZ2_Proj = new Point(MapPoint(totalL).X + 25, pDatumZ1.Y);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(90, 148, 163, 184)), 1.0) { DashStyle = DashStyles.Dash }, pDatumZ1, pDatumZ2_Proj);

            // g) Bảng tóm tắt thông số cao độ trên Canvas
            DrawElevationSummaryTable(dc, viewW, viewH, geom);

            // h) Cảnh báo nếu dốc ngược
            if (geom.IsReverseSlope)
            {
                DrawReverseSlopeBanner(dc, viewW, geom);
            }
        }
        #endregion

        #region Mode 2: Render Plan 2D (Mặt Bằng Cống)
        private void RenderPlan2D(DrawingContext dc, double viewW, double viewH)
        {
            var geom = GeometryData!;
            double totalL = geom.TotalLengthM;
            if (totalL <= 0.1) return;

            double marginX = 80;
            double availableW = Math.Max(viewW - (2 * marginX), 200);
            double scaleX = availableW / (totalL + 4.0);
            double scaleY = scaleX;

            double startX = marginX + (2.0 * scaleX);
            double centerY = viewH / 2.0;

            bool isDouble = (geom.SoCua >= 2 && geom.LoaiCong.ToUpperInvariant().Contains("TRON"));
            double barrelW = geom.BarrelWidthM * scaleY;
            double dTim = geom.KhoangCachTim * scaleY;

            // Tim đường và Tim cống
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(120, 248, 113, 113)), 1.2) { DashStyle = DashStyles.DashDot },
                new Point(startX - 40, centerY), new Point(startX + totalL * scaleX + 40, centerY));

            if (!isDouble)
            {
                // CỐNG ĐƠN / HỘP ĐÔI (1 TRỤC TIM)
                Rect rBarrel = new Rect(startX, centerY - (barrelW / 2.0), totalL * scaleX, barrelW);
                dc.DrawRectangle(StdSegBrush, StdSegPen, rBarrel);

                // Vẽ các vách ngăn từng đốt cống
                foreach (var seg in geom.Segments)
                {
                    double xLine = startX + (seg.EndDistanceM * scaleX);
                    dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.0), new Point(xLine, centerY - (barrelW / 2.0)), new Point(xLine, centerY + (barrelW / 2.0)));
                }

                DrawCenteredText(dc, $"THÂN CỐNG {geom.LoaiCong} ({geom.KhauDo})", new Point(rBarrel.X + rBarrel.Width / 2.0, centerY), 11, TextBrush);
            }
            else
            {
                // CỐNG TRÒN ĐÔI (2 NHÁNH ỐNG SONG SONG CÁCH NHAU D_TIM)
                double yPipe1 = centerY - (dTim / 2.0);
                double yPipe2 = centerY + (dTim / 2.0);

                Rect rPipe1 = new Rect(startX, yPipe1 - (barrelW / 2.0), totalL * scaleX, barrelW);
                Rect rPipe2 = new Rect(startX, yPipe2 - (barrelW / 2.0), totalL * scaleX, barrelW);

                dc.DrawRectangle(StdSegBrush, StdSegPen, rPipe1);
                dc.DrawRectangle(StdSegBrush, StdSegPen, rPipe2);

                DrawCenteredText(dc, $"NHÁNH TRÁI (D_tim = {geom.KhoangCachTim:N2}m)", new Point(rPipe1.X + rPipe1.Width / 2.0, yPipe1), 10.5, MintTextBrush);
                DrawCenteredText(dc, $"NHÁNH PHẢI (D_tim = {geom.KhoangCachTim:N2}m)", new Point(rPipe2.X + rPipe2.Width / 2.0, yPipe2), 10.5, MintTextBrush);

                // Đường đo D_tim giữa 2 tim ống
                double dimDx = startX + (totalL * scaleX / 2.0);
                DrawCadDimension(dc, new Point(dimDx, yPipe1), new Point(dimDx, yPipe2), $"D_tim = {geom.KhoangCachTim:N2}m", 10, YellowTextBrush);
            }

            // Sân cống Thượng lưu & Hạ lưu dạng loe (Apron Wings)
            DrawApronPlan(dc, new Point(startX, centerY), barrelW * (isDouble ? 2.5 : 1.6), true);
            DrawApronPlan(dc, new Point(startX + totalL * scaleX, centerY), barrelW * (isDouble ? 2.5 : 1.6), false);

            // Đường kích thước tổng thể
            DrawCadDimension(dc, new Point(startX, centerY + (barrelW * 1.8) + 25), new Point(startX + totalL * scaleX, centerY + (barrelW * 1.8) + 25), $"L = {totalL:N2} m (Góc xoay: {geom.GocXoayDeg:N1}°)", 11, CyanTextBrush, true);
        }

        private void DrawApronPlan(DrawingContext dc, Point pBase, double width, bool isUpstream)
        {
            double dx = isUpstream ? -25 : 25;
            var path = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(pBase.X, pBase.Y - width / 2.0), IsClosed = true };
            fig.Segments.Add(new LineSegment(new Point(pBase.X + dx, pBase.Y - width * 0.8), true));
            fig.Segments.Add(new LineSegment(new Point(pBase.X + dx, pBase.Y + width * 0.8), true));
            fig.Segments.Add(new LineSegment(new Point(pBase.X, pBase.Y + width / 2.0), true));
            path.Figures.Add(fig);

            dc.DrawGeometry(ApronBrush, ApronPen, path);
            DrawCenteredText(dc, isUpstream ? "SÂN TL" : "SÂN HL", new Point(pBase.X + dx / 1.5, pBase.Y), 9.5, CyanTextBrush);
        }
        #endregion

        #region Mode 3: Render Isometric 3D (Phối Cảnh 3D Trực Quan)
        private void RenderIsometric3D(DrawingContext dc, double viewW, double viewH)
        {
            var geom = GeometryData!;
            double totalL = geom.TotalLengthM;
            if (totalL <= 0.1) return;

            double cx = viewW / 2.0;
            double cy = viewH / 2.0 + 30;

            // Vector trục Isometric 3D: X trục dài (góc 25°), Y chiều cao (thẳng đứng), Z bề rộng (góc 155°)
            double angX = 22.0 * Math.PI / 180.0;
            double angZ = 158.0 * Math.PI / 180.0;

            double scale = Math.Min(viewW / (totalL * 1.5), 25.0);
            double lenPx = totalL * scale;
            double wPx = geom.BarrelWidthM * scale * 1.8;
            double hPx = geom.BarrelHeightM * scale * 1.8;

            Point Origin = new Point(cx - (lenPx * Math.Cos(angX) / 2.0) - (wPx * Math.Cos(angZ) / 2.0), cy);

            Point Project3D(double xM, double yM, double zM)
            {
                double px = Origin.X + (xM * scale * Math.Cos(angX)) + (zM * scale * Math.Cos(angZ));
                double py = Origin.Y + (xM * scale * Math.Sin(angX)) + (zM * scale * Math.Sin(angZ)) - (yM * scale);
                return new Point(px, py);
            }

            // 1. Khối bê tông lót nền 3D
            Draw3DBox(dc, Project3D(-1.0, -0.2, -0.4), totalL + 2.0, 0.2, geom.BarrelWidthM + 0.8, scale, angX, angZ, BtlBrush, new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1.0));

            // 2. Thân cống từng đốt 3D
            foreach (var seg in geom.Segments)
            {
                Brush b = seg.IsStandard ? StdSegBrush : (seg.LengthM < geom.L_Min ? WarnSegBrush : CompSegBrush);
                Pen p = seg.IsStandard ? StdSegPen : (seg.LengthM < geom.L_Min ? WarnSegPen : CompSegPen);
                Point ptBase = Project3D(seg.StartDistanceM, 0, 0);
                Draw3DBox(dc, ptBase, seg.LengthM, geom.BarrelHeightM, geom.BarrelWidthM, scale, angX, angZ, b, p);
            }

            // 3. Sân cống 2 đầu 3D
            Draw3DBox(dc, Project3D(-1.5, 0, -0.3), 1.5, geom.BarrelHeightM + 0.4, geom.BarrelWidthM + 0.6, scale, angX, angZ, ApronBrush, ApronPen);
            Draw3DBox(dc, Project3D(totalL, 0, -0.3), 1.5, geom.BarrelHeightM + 0.4, geom.BarrelWidthM + 0.6, scale, angX, angZ, ApronBrush, ApronPen);

            DrawCenteredText(dc, $"PHỐI CẢNH 3D: CỐNG {geom.LoaiCong} ({geom.KhauDo}) - L = {totalL:N2}m", new Point(cx, cy + 80), 12, CyanTextBrush);
        }

        private void Draw3DBox(DrawingContext dc, Point p0, double lenM, double hM, double wM, double scale, double angX, double angZ, Brush fill, Pen stroke)
        {
            double dx = lenM * scale * Math.Cos(angX);
            double dy = lenM * scale * Math.Sin(angX);
            double dz_x = wM * scale * Math.Cos(angZ);
            double dz_y = wM * scale * Math.Sin(angZ);
            double dh = hM * scale;

            Point p1 = new Point(p0.X + dx, p0.Y + dy);
            Point p2 = new Point(p1.X + dz_x, p1.Y + dz_y);
            Point p3 = new Point(p0.X + dz_x, p0.Y + dz_y);

            Point p0_top = new Point(p0.X, p0.Y - dh);
            Point p1_top = new Point(p1.X, p1.Y - dh);
            Point p2_top = new Point(p2.X, p2.Y - dh);
            Point p3_top = new Point(p3.X, p3.Y - dh);

            // Mặt trên (Top Face)
            var topPath = new PathGeometry();
            var topFig = new PathFigure { StartPoint = p0_top, IsClosed = true };
            topFig.Segments.Add(new LineSegment(p1_top, true));
            topFig.Segments.Add(new LineSegment(p2_top, true));
            topFig.Segments.Add(new LineSegment(p3_top, true));
            topPath.Figures.Add(topFig);
            dc.DrawGeometry(fill, stroke, topPath);

            // Mặt trước (Front Face)
            var frontPath = new PathGeometry();
            var frontFig = new PathFigure { StartPoint = p0, IsClosed = true };
            frontFig.Segments.Add(new LineSegment(p1, true));
            frontFig.Segments.Add(new LineSegment(p1_top, true));
            frontFig.Segments.Add(new LineSegment(p0_top, true));
            frontPath.Figures.Add(frontFig);
            dc.DrawGeometry(fill, stroke, frontPath);

            // Mặt bên (Side Face)
            var sidePath = new PathGeometry();
            var sideFig = new PathFigure { StartPoint = p1, IsClosed = true };
            sideFig.Segments.Add(new LineSegment(p2, true));
            sideFig.Segments.Add(new LineSegment(p2_top, true));
            sideFig.Segments.Add(new LineSegment(p1_top, true));
            sidePath.Figures.Add(sideFig);
            dc.DrawGeometry(fill, stroke, sidePath);
        }
        #endregion

        #region Helper Drawing Utilities (CAD Dimensions & Markers)
        private void DrawCadDimension(DrawingContext dc, Point p1, Point p2, string label, double fontSize, Brush textBrush, bool isBold = false)
        {
            double tick = 5.0;
            dc.DrawLine(DimPen, p1, p2);
            dc.DrawLine(DimPen, new Point(p1.X - tick, p1.Y - tick), new Point(p1.X + tick, p1.Y + tick));
            dc.DrawLine(DimPen, new Point(p2.X - tick, p2.Y - tick), new Point(p2.X + tick, p2.Y + tick));

            Point mid = new Point((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0 - (fontSize + 2));
            var fontW = isBold ? FontWeights.Bold : FontWeights.Normal;
            var text = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, fontW, FontStretches.Normal), fontSize, textBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                TextAlignment = TextAlignment.Center
            };
            dc.DrawText(text, mid);
        }

        private void DrawElevationMarker(DrawingContext dc, Point pt, string label, bool isLeft, bool isTop = false, bool isSmall = false, Brush? customBrush = null)
        {
            double s = isSmall ? 6.0 : 8.0;
            Brush brush = customBrush ?? YellowTextBrush;
            Pen pen = new Pen(brush, 1.0);

            // Ký hiệu cao độ: Tam giác ngược ∇ có đỉnh tại pt
            var tri = new PathGeometry();
            var fig = new PathFigure { StartPoint = pt, IsClosed = true };
            double topY = pt.Y - (s * 1.5);
            fig.Segments.Add(new LineSegment(new Point(pt.X - s, topY), true));
            fig.Segments.Add(new LineSegment(new Point(pt.X + s, topY), true));
            tri.Figures.Add(fig);
            dc.DrawGeometry(brush, pen, tri);

            // Gạch ngang và text cao độ
            double lineW = isSmall ? 70.0 : 90.0;
            Point pLineStart = isLeft ? new Point(pt.X + s, topY) : new Point(pt.X - s, topY);
            Point pLineEnd = isLeft ? new Point(pt.X - lineW, topY) : new Point(pt.X + lineW, topY);
            dc.DrawLine(pen, pLineStart, pLineEnd);

            double fontSize = isSmall ? 9.5 : 10.5;
            var text = new FormattedText(
                label,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                fontSize,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                TextAlignment = isLeft ? TextAlignment.Right : TextAlignment.Left
            };
            double tx = isLeft ? pt.X - s - 4 : pt.X + s + 4;
            dc.DrawText(text, new Point(tx, topY - fontSize - 3));
        }

        private void DrawSlopeIndicator(DrawingContext dc, Point pt, double slopePercent, bool isReverse)
        {
            string sText = isReverse ? $"⚠️ DỐC NGƯỢC! i = {slopePercent:N2}% ◀" : $"i = {slopePercent:N2}%  ➔";
            Brush textBrush = isReverse ? RedTextBrush : MintTextBrush;
            Brush bgBrush = isReverse ? new SolidColorBrush(Color.FromArgb(200, 153, 27, 27)) : new SolidColorBrush(Color.FromArgb(160, 6, 78, 59));
            Pen borderPen = isReverse ? new Pen(new SolidColorBrush(Color.FromRgb(248, 113, 113)), 1.2) : new Pen(new SolidColorBrush(Color.FromRgb(52, 211, 153)), 1.0);

            var text = new FormattedText(
                sText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                11.0,
                textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                TextAlignment = TextAlignment.Center
            };

            Rect rect = new Rect(pt.X - (text.Width / 2.0) - 8, pt.Y - 2, text.Width + 16, text.Height + 4);
            dc.DrawRoundedRectangle(bgBrush, borderPen, rect, 4, 4);
            dc.DrawText(text, pt);
        }

        private void DrawElevationSummaryTable(DrawingContext dc, double viewW, double viewH, CulvertPreviewGeometry geom)
        {
            double cardX = 12;
            double cardY = 40;
            double cardW = 340;
            double cardH = 115;

            // Semi-transparent background HUD
            Rect bgRect = new Rect(cardX, cardY, cardW, cardH);
            Brush cardBg = new SolidColorBrush(Color.FromArgb(210, 15, 23, 42)); // Slate-900
            Pen cardBorder = new Pen(new SolidColorBrush(Color.FromArgb(120, 56, 189, 248)), 1.0); // Cyan border
            dc.DrawRoundedRectangle(cardBg, cardBorder, bgRect, 6, 6);

            // Title
            var titleText = new FormattedText(
                "📐 KIỂM TRA CAO ĐỘ & ĐỘ DỐC THỦY LỰC",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                11.0,
                CyanTextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(titleText, new Point(cardX + 10, cardY + 8));

            // Content lines
            double deltaZ = geom.Z1 - geom.Z2;
            string line1 = $"Đáy cống: Z1 = {geom.Z1:N3}m  ➔  Z2 = {geom.Z2:N3}m  (ΔZ = {deltaZ:N3}m)";
            string line2 = $"Đỉnh cống: Zt1 = {geom.Z_Top1:N3}m  ➔  Zt2 = {geom.Z_Top2:N3}m";
            string line3 = $"Độ dốc: i_tính = {geom.CalculatedSlopePercent:N2}%  |  i_TK = {geom.DoDocPercent:N2}% (Δi = {geom.SlopeDiffPercent:N2}%)";
            string line4 = $"Kết luận: {geom.ElevationStatus} ({geom.ElevationNote})";

            Brush statusBrush = geom.IsReverseSlope ? RedTextBrush : (geom.IsFlatSlope || geom.IsSteepSlope ? YellowTextBrush : MintTextBrush);

            void DrawLine(string text, double yOffset, Brush brush, bool isBold = false)
            {
                var ft = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, isBold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
                    10.0,
                    brush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(ft, new Point(cardX + 10, cardY + yOffset));
            }

            DrawLine(line1, 28, TextBrush);
            DrawLine(line2, 48, TextBrush);
            DrawLine(line3, 68, TextBrush);
            DrawLine(line4, 88, statusBrush, isBold: true);
        }

        private void DrawReverseSlopeBanner(DrawingContext dc, double viewW, CulvertPreviewGeometry geom)
        {
            string bannerText = $"⚠️ CẢNH BÁO: DỐC NGƯỢC THỦY LỰC! Z1 ({geom.Z1:N3}m) < Z2 ({geom.Z2:N3}m) - ĐỘ DỐC i = {geom.CalculatedSlopePercent:N2}%";
            var ft = new FormattedText(
                bannerText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                12.5,
                new SolidColorBrush(Color.FromRgb(254, 242, 242)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                TextAlignment = TextAlignment.Center
            };

            double bannerW = Math.Max(ft.Width + 30, 420);
            double bannerH = 30;
            double bannerX = (viewW - bannerW) / 2.0;
            double bannerY = 10;

            Rect r = new Rect(bannerX, bannerY, bannerW, bannerH);
            Brush bg = new SolidColorBrush(Color.FromArgb(235, 185, 28, 28)); // Red banner
            Pen border = new Pen(new SolidColorBrush(Color.FromRgb(252, 165, 165)), 1.5);
            dc.DrawRoundedRectangle(bg, border, r, 6, 6);
            dc.DrawText(ft, new Point(viewW / 2.0, bannerY + 6));
        }

        private void DrawCenteredText(DrawingContext dc, string text, Point center, double fontSize, Brush brush)
        {
            var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                TextAlignment = TextAlignment.Center
            };
            dc.DrawText(ft, new Point(center.X, center.Y - (ft.Height / 2.0)));
        }
        #endregion
    }
}
