using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using InfraBIM.ManholeTool.Models;
using InfraBIM.ManholeTool.Services;

namespace InfraBIM.ManholeTool.Views
{
    /// <summary>
    /// Viewport 3D Kiểm Tra Mô Hình Hố Ga (Mẫu 1: Toàn Tuyến 3D + Focus Cận Cảnh Thông Minh):
    /// - Mặc định: Hiển thị TOÀN BỘ TUYẾN hố ga cùng đường ống cống ngầm liên kết theo đúng tọa độ thực tế.
    /// - Cận cảnh: Tự động zoom mượt vào hố ga đang chọn, hiển thị thước đo cao độ thông minh (Đỉnh, Đáy cống, Đáy ga, Chiều cao).
    /// - Tương tác: Click chọn trực tiếp hố ga trong không gian 3D, xoay 360°, kéo Pan, lăn chuột Zoom.
    /// </summary>
    public class Manhole3DViewportControl : Grid
    {
        private readonly Viewport3D _viewport;
        private readonly PerspectiveCamera _camera;
        private readonly ModelVisual3D _modelVisual;
        private readonly Model3DGroup _rootGroup;

        // Lưu vết ánh xạ từ Model 3D sang ManholeInspectionItem để bắt tương tác click
        private readonly Dictionary<Model3D, ManholeInspectionItem> _modelToItemMap = new();

        // Camera State
        private Point3D _targetCenter = new Point3D(0, 0, 0);
        private double _cameraDistance = 25.0;
        private double _yaw = 45.0;   // deg
        private double _pitch = 28.0; // deg

        private Point _mouseDownPos;
        private Point _lastMousePos;
        private bool _isOrbiting;
        private bool _isPanning;

        // HUD Elements
        private readonly TextBlock _hudTitle;
        private readonly TextBlock _hudInfo;
        private readonly Border _hudCard;
        private readonly Button _btnModeFull;
        private readonly Button _btnModeFocus;

        #region Dependency Properties

        public static readonly DependencyProperty InspectionItemProperty =
            DependencyProperty.Register(
                nameof(InspectionItem),
                typeof(ManholeInspectionItem),
                typeof(Manhole3DViewportControl),
                new FrameworkPropertyMetadata(null, OnDataChanged));

        public ManholeInspectionItem? InspectionItem
        {
            get => (ManholeInspectionItem?)GetValue(InspectionItemProperty);
            set => SetValue(InspectionItemProperty, value);
        }

        public static readonly DependencyProperty AllInspectionItemsProperty =
            DependencyProperty.Register(
                nameof(AllInspectionItems),
                typeof(IList<ManholeInspectionItem>),
                typeof(Manhole3DViewportControl),
                new FrameworkPropertyMetadata(null, OnDataChanged));

        public IList<ManholeInspectionItem>? AllInspectionItems
        {
            get => (IList<ManholeInspectionItem>?)GetValue(AllInspectionItemsProperty);
            set => SetValue(AllInspectionItemsProperty, value);
        }

        public static readonly DependencyProperty ViewModeProperty =
            DependencyProperty.Register(
                nameof(ViewMode),
                typeof(ManholePreviewMode),
                typeof(Manhole3DViewportControl),
                new FrameworkPropertyMetadata(ManholePreviewMode.FullPipeline, OnDataChanged));

        public ManholePreviewMode ViewMode
        {
            get => (ManholePreviewMode)GetValue(ViewModeProperty);
            set => SetValue(ViewModeProperty, value);
        }

        public static readonly DependencyProperty RealGeometryProperty =
            DependencyProperty.Register(
                nameof(RealGeometry),
                typeof(ExtractedManholeMeshGroup),
                typeof(Manhole3DViewportControl),
                new FrameworkPropertyMetadata(null, OnDataChanged));

        public ExtractedManholeMeshGroup? RealGeometry
        {
            get => (ExtractedManholeMeshGroup?)GetValue(RealGeometryProperty);
            set => SetValue(RealGeometryProperty, value);
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Manhole3DViewportControl ctrl)
            {
                ctrl.Rebuild3DScene();
            }
        }

        #endregion

        public Manhole3DViewportControl()
        {
            Background = new SolidColorBrush(Color.FromRgb(10, 17, 32)); // Dark Navy #0A1120

            // 1. Khởi tạo Camera 3D
            _camera = new PerspectiveCamera
            {
                FieldOfView = 45,
                NearPlaneDistance = 0.1,
                FarPlaneDistance = 1500
            };

            // 2. Khởi tạo Model Group và Viewport
            _rootGroup = new Model3DGroup();
            _modelVisual = new ModelVisual3D { Content = _rootGroup };

            _viewport = new Viewport3D
            {
                Camera = _camera,
                ClipToBounds = true
            };
            _viewport.Children.Add(_modelVisual);
            Children.Add(_viewport);

            // 3. Khởi tạo HUD Overlays (Bảng thông tin góc trên bên trái)
            _hudCard = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(225, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(14),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            var hudStack = new StackPanel();
            _hudTitle = new TextBlock
            {
                Text = "KIỂM TRA MÔ HÌNH TOÀN TUYẾN 3D",
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                FontWeight = FontWeights.Bold,
                FontSize = 13.5
            };
            _hudInfo = new TextBlock
            {
                Text = "Sẵn sàng hiển thị chuỗi hố ga trên tuyến.",
                Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0),
                LineHeight = 18
            };
            hudStack.Children.Add(_hudTitle);
            hudStack.Children.Add(_hudInfo);
            _hudCard.Child = hudStack;
            Children.Add(_hudCard);

            // 4. View Navigation Toolbar (Góc trên bên phải)
            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(14)
            };

            _btnModeFull = CreateToolButton("🌐 Toàn Tuyến", () => SetViewMode(ManholePreviewMode.FullPipeline), isHighlight: true);
            _btnModeFocus = CreateToolButton("🔍 Cận Cảnh", () => SetViewMode(ManholePreviewMode.SingleFocus), isHighlight: false);

            toolbar.Children.Add(_btnModeFull);
            toolbar.Children.Add(_btnModeFocus);

            // Các nút duyệt nhanh hố ga
            toolbar.Children.Add(CreateToolButton("◀ Hố Trước", SelectPreviousManhole));
            toolbar.Children.Add(CreateToolButton("Hố Sau ▶", SelectNextManhole));

            // Các nút góc nhìn chuẩn
            toolbar.Children.Add(CreateToolButton("3D Iso", () => SetViewAngle(45, 28)));
            toolbar.Children.Add(CreateToolButton("Mặt Đứng", () => SetViewAngle(0, 0)));
            toolbar.Children.Add(CreateToolButton("Mặt Bằng", () => SetViewAngle(0, 89)));
            toolbar.Children.Add(CreateToolButton("Căn Vừa", FitView));

            Children.Add(toolbar);

            // 5. Hint text (Góc dưới bên trái)
            var hintText = new TextBlock
            {
                Text = "🖱 Click hố ga: Chọn & Highlight | Double click: Cận cảnh | Chuột trái: Xoay 360° | Chuột phải: Kéo Pan | Lăn chuột: Thu phóng",
                Foreground = new SolidColorBrush(Color.FromArgb(190, 186, 230, 253)),
                FontSize = 11,
                Margin = new Thickness(16, 0, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                IsHitTestVisible = false
            };
            Children.Add(hintText);

            // Đăng ký tương tác chuột
            MouseLeftButtonDown += OnMouseDown;
            MouseRightButtonDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseUp;
            MouseRightButtonUp += OnMouseUp;
            MouseWheel += OnMouseWheel;

            UpdateCamera();
            Rebuild3DScene();
        }

        private Button CreateToolButton(string text, Action action, bool isHighlight = false)
        {
            var btn = new Button
            {
                Content = text,
                Background = new SolidColorBrush(isHighlight ? Color.FromRgb(2, 132, 199) : Color.FromArgb(220, 24, 42, 68)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(4, 0, 0, 0),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            btn.Click += (s, e) => action();
            return btn;
        }

        public void SetViewMode(ManholePreviewMode mode)
        {
            ViewMode = mode;
            UpdateModeButtonStyles();
            Rebuild3DScene();
            FitView();
        }

        private void UpdateModeButtonStyles()
        {
            if (ViewMode == ManholePreviewMode.FullPipeline)
            {
                _btnModeFull.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
                _btnModeFocus.Background = new SolidColorBrush(Color.FromArgb(220, 24, 42, 68));
            }
            else
            {
                _btnModeFull.Background = new SolidColorBrush(Color.FromArgb(220, 24, 42, 68));
                _btnModeFocus.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
            }
        }

        public void SelectPreviousManhole()
        {
            if (AllInspectionItems == null || AllInspectionItems.Count == 0) return;
            int idx = InspectionItem != null ? AllInspectionItems.IndexOf(InspectionItem) : 0;
            if (idx > 0)
            {
                InspectionItem = AllInspectionItems[idx - 1];
            }
            else
            {
                InspectionItem = AllInspectionItems[AllInspectionItems.Count - 1];
            }
        }

        public void SelectNextManhole()
        {
            if (AllInspectionItems == null || AllInspectionItems.Count == 0) return;
            int idx = InspectionItem != null ? AllInspectionItems.IndexOf(InspectionItem) : -1;
            if (idx >= 0 && idx < AllInspectionItems.Count - 1)
            {
                InspectionItem = AllInspectionItems[idx + 1];
            }
            else
            {
                InspectionItem = AllInspectionItems[0];
            }
        }

        public void SetViewAngle(double yaw, double pitch)
        {
            _yaw = yaw;
            _pitch = pitch;
            UpdateCamera();
        }

        public void FitView()
        {
            if (ViewMode == ManholePreviewMode.FullPipeline && AllInspectionItems != null && AllInspectionItems.Count > 0)
            {
                // Fit toàn tuyến
                double minX = AllInspectionItems.Min(i => i.ToaDoX);
                double maxX = AllInspectionItems.Max(i => i.ToaDoX);
                double minY = AllInspectionItems.Min(i => i.ToaDoY);
                double maxY = AllInspectionItems.Max(i => i.ToaDoY);
                double minZ = AllInspectionItems.Min(i => i.CdDay);
                double maxZ = AllInspectionItems.Max(i => i.CdDinh);

                double spanX = maxX - minX;
                double spanY = maxY - minY;
                double spanH = Math.Sqrt(spanX * spanX + spanY * spanY);

                _targetCenter = new Point3D(0, (minZ + maxZ) / 2.0, 0);
                _cameraDistance = Math.Clamp(spanH * 1.35, 15.0, 600.0);
            }
            else if (InspectionItem != null)
            {
                // Fit hố ga đơn
                double h = InspectionItem.ChieuCaoThan > 0.2 ? InspectionItem.ChieuCaoThan : 2.0;
                _targetCenter = new Point3D(0, (InspectionItem.CdDinh + InspectionItem.CdDay) / 2.0, 0);
                _cameraDistance = Math.Max(5.0, h * 2.8);
            }
            else
            {
                _targetCenter = new Point3D(0, 0, 0);
                _cameraDistance = 15.0;
            }

            UpdateCamera();
        }

        public void Rebuild3DScene()
        {
            _rootGroup.Children.Clear();
            _modelToItemMap.Clear();

            // 1. Ánh sáng đa hướng
            _rootGroup.Children.Add(new AmbientLight(Color.FromRgb(165, 180, 205)));
            _rootGroup.Children.Add(new DirectionalLight(Color.FromRgb(255, 255, 255), new Vector3D(-1, -2, -1)));
            _rootGroup.Children.Add(new DirectionalLight(Color.FromRgb(130, 145, 165), new Vector3D(1, 1, 1)));

            UpdateModeButtonStyles();

            // 2. Chế độ Xem Toàn Tuyến (Mẫu 1 mặc định)
            if (ViewMode == ManholePreviewMode.FullPipeline && AllInspectionItems != null && AllInspectionItems.Count > 0)
            {
                RenderFullPipelineScene();
            }
            else
            {
                // 3. Chế độ Xem Cận Cảnh 1 Hố Ga + Thước Cao Độ Thông Minh
                RenderSingleFocusScene();
            }
        }

        private void RenderFullPipelineScene()
        {
            if (AllInspectionItems == null || AllInspectionItems.Count == 0) return;

            var pipelineMeshes = ManholeMeshExtractor.BuildPipelineMeshes(
                items: AllInspectionItems,
                selectedItem: InspectionItem,
                out Point3D center,
                out Rect3D bounds);

            foreach (var p in pipelineMeshes)
            {
                var mat = new DiffuseMaterial(p.Brush);
                var model = new GeometryModel3D(p.Mesh, mat) { BackMaterial = mat };
                _rootGroup.Children.Add(model);

                if (p.Item != null)
                {
                    _modelToItemMap[model] = p.Item;
                }
            }

            // Cập nhật HUD
            int total = AllInspectionItems.Count;
            int warnings = AllInspectionItems.Count(i => i.HasWarning);
            string activeName = InspectionItem != null ? InspectionItem.TenHoGa : "(Chưa chọn)";
            string branchName = AllInspectionItems[0].Side;

            _hudTitle.Text = $"🌐 TOÀN TUYẾN 3D: {branchName} ({total} HỐ GA)";
            _hudInfo.Text = $"• Đang chọn: {activeName}  |  Tổng số hố: {total} hố ga\n" +
                            $"• Cảnh báo cao độ: {(warnings > 0 ? $"⚠ {warnings} hố ga lỗi!" : "✔ 100% Cao độ hợp lệ")}\n" +
                            $"• Nhấn [🔍 Cận Cảnh] hoặc Click trực tiếp vào hố ga để đo cao độ chi tiết.";
        }

        private void RenderSingleFocusScene()
        {
            if (InspectionItem == null)
            {
                _hudTitle.Text = "CHƯA CHỌN HỐ GA";
                _hudInfo.Text = "Vui lòng chọn một hố ga trong bảng dữ liệu để kiểm tra chi tiết.";
                return;
            }

            // 1. Cập nhật HUD chi tiết hố ga
            string statusStr = InspectionItem.HasWarning ? "⚠ " + InspectionItem.WarningMessage : "✔ HỢP LỆ (CĐ ĐỈNH > CĐ ĐÁY)";
            _hudTitle.Text = $"🔍 CẬN CẢNH: {InspectionItem.TenHoGa} ({InspectionItem.Side})";
            _hudInfo.Text = $"• CĐ Đỉnh (A): {InspectionItem.CdDinh:F3} m  |  CĐ Đáy (D): {InspectionItem.CdDay:F3} m\n" +
                            (InspectionItem.CdDayCong > 0.01 ? $"• CĐ Đáy cống (C): {InspectionItem.CdDayCong:F3} m  |  Lắng cặn: {InspectionItem.ChieuSauLangCan:F3} m\n" : "") +
                            $"• Chiều cao thân H: {InspectionItem.ChieuCaoThan:F3} m  |  Góc xoay: {InspectionItem.GocPv:F1}°\n" +
                            (!string.IsNullOrWhiteSpace(InspectionItem.LyTrinh) ? $"• Lý trình: {InspectionItem.LyTrinh}  |  " : "• ") +
                            $"Tọa độ: X={InspectionItem.ToaDoX:F2}, Y={InspectionItem.ToaDoY:F2}\n" +
                            $"• Tình trạng: {statusStr}";

            // 2. Render mô hình 3D
            if (RealGeometry != null && RealGeometry.Parts.Count > 0)
            {
                foreach (var part in RealGeometry.Parts)
                {
                    var mat = new DiffuseMaterial(part.Brush);
                    var geomModel = new GeometryModel3D(part.Mesh, mat) { BackMaterial = mat };
                    _rootGroup.Children.Add(geomModel);
                    _modelToItemMap[geomModel] = InspectionItem;
                }
            }
            else
            {
                // Mô hình tham số hóa chuẩn HO THU NUOC DVB_LOAI 1
                var parts = ManholeMeshExtractor.BuildParametricManholeMeshes(
                    widthM: 1.4,
                    lengthM: 1.4,
                    heightM: InspectionItem.ChieuCaoThan,
                    cdDinh: InspectionItem.CdDinh,
                    cdDay: InspectionItem.CdDay,
                    gocPvDeg: InspectionItem.GocPv,
                    offsetX: 0,
                    offsetZ: 0,
                    isHighlighted: true,
                    hasWarning: InspectionItem.HasWarning,
                    cdDayCong: InspectionItem.CdDayCong);

                foreach (var p in parts)
                {
                    var mat = new DiffuseMaterial(p.Brush);
                    var geomModel = new GeometryModel3D(p.Mesh, mat) { BackMaterial = mat };
                    _rootGroup.Children.Add(geomModel);
                    _modelToItemMap[geomModel] = InspectionItem;
                }
            }

            // 3. Xây dựng thước đo cao độ 3D thông minh
            BuildElevationRulers(InspectionItem.CdDinh, InspectionItem.CdDay, InspectionItem.CdDayCong);
        }

        private void BuildElevationRulers(double cdDinh, double cdDay, double cdDayCong)
        {
            var rulerBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));   // Cyan #38BDF8
            var markerDinhBrush = new SolidColorBrush(Color.FromRgb(74, 222, 128)); // Xanh lá cây #4ADE80 (Đỉnh)
            var markerCongBrush = new SolidColorBrush(Color.FromRgb(14, 165, 233)); // Xanh lơ #0EA5E9 (Đáy cống)
            var markerDayBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));  // Hổ phách #F59E0B (Đáy ga)

            if (rulerBrush.CanFreeze) rulerBrush.Freeze();
            if (markerDinhBrush.CanFreeze) markerDinhBrush.Freeze();
            if (markerCongBrush.CanFreeze) markerCongBrush.Freeze();
            if (markerDayBrush.CanFreeze) markerDayBrush.Freeze();

            double rulerX = 1.35; // Đặt lệch sang phải thân hố ga
            double hRuler = Math.Max(0.5, cdDinh - cdDay);

            // Cột thước đo thẳng đứng
            var colMesh = ManholeMeshExtractor.CreateBoxMesh(0.04, 0.04, hRuler + 0.4, rulerX, (cdDinh + cdDay) / 2.0, 0);
            _rootGroup.Children.Add(new GeometryModel3D(colMesh, new DiffuseMaterial(rulerBrush)));

            // 1. Mốc chỉ CĐ Đỉnh (A)
            var dinhMesh = ManholeMeshExtractor.CreateBoxMesh(0.45, 0.06, 0.06, rulerX - 0.2, cdDinh, 0);
            _rootGroup.Children.Add(new GeometryModel3D(dinhMesh, new DiffuseMaterial(markerDinhBrush)));

            // 2. Mốc chỉ CĐ Đáy cống ngang (C) nếu có
            if (cdDayCong > 0.01 && cdDayCong > cdDay && cdDayCong < cdDinh)
            {
                var congMesh = ManholeMeshExtractor.CreateBoxMesh(0.40, 0.05, 0.05, rulerX - 0.18, cdDayCong, 0);
                _rootGroup.Children.Add(new GeometryModel3D(congMesh, new DiffuseMaterial(markerCongBrush)));
            }

            // 3. Mốc chỉ CĐ Đáy hố thu (D)
            var dayMesh = ManholeMeshExtractor.CreateBoxMesh(0.45, 0.06, 0.06, rulerX - 0.2, cdDay, 0);
            _rootGroup.Children.Add(new GeometryModel3D(dayMesh, new DiffuseMaterial(markerDayBrush)));
        }

        #region Mouse Interactions & Camera Math

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _mouseDownPos = e.GetPosition(this);
            _lastMousePos = _mouseDownPos;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (e.ClickCount == 2)
                {
                    // Double click: Chuyển sang Cận Cảnh hố ga được click hoặc Toggle
                    TryPickManholeFromClick(_mouseDownPos, switchToFocus: true);
                    return;
                }

                _isOrbiting = true;
                CaptureMouse();
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                CaptureMouse();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isOrbiting && !_isPanning) return;

            Point current = e.GetPosition(this);
            double dx = current.X - _lastMousePos.X;
            double dy = current.Y - _lastMousePos.Y;
            _lastMousePos = current;

            if (_isOrbiting)
            {
                _yaw -= dx * 0.42;
                _pitch += dy * 0.42;
                _pitch = Math.Clamp(_pitch, -89.0, 89.0);
                UpdateCamera();
            }
            else if (_isPanning)
            {
                double panSpeed = _cameraDistance * 0.0016;
                double yawRad = _yaw * Math.PI / 180.0;
                Vector3D right = new Vector3D(Math.Cos(yawRad), 0, -Math.Sin(yawRad));
                Vector3D up = new Vector3D(0, 1, 0);

                _targetCenter -= right * (dx * panSpeed) - up * (dy * panSpeed);
                UpdateCamera();
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            Point upPos = e.GetPosition(this);
            double moveDist = (upPos - _mouseDownPos).Length;

            // Nếu người dùng chỉ click chuột (không rê chuột kéo xoay), thực hiện chọn hố ga
            if (e.ChangedButton == MouseButton.Left && moveDist < 4.0)
            {
                TryPickManholeFromClick(upPos, switchToFocus: false);
            }

            if (e.LeftButton == MouseButtonState.Released) _isOrbiting = false;
            if (e.RightButton == MouseButtonState.Released) _isPanning = false;

            if (!_isOrbiting && !_isPanning)
            {
                ReleaseMouseCapture();
            }
        }

        private void TryPickManholeFromClick(Point clickPos, bool switchToFocus)
        {
            var hitParams = new PointHitTestParameters(clickPos);
            VisualTreeHelper.HitTest(_viewport, null, result =>
            {
                if (result is RayMeshGeometry3DHitTestResult rayResult)
                {
                    if (_modelToItemMap.TryGetValue(rayResult.ModelHit, out var hitItem))
                    {
                        InspectionItem = hitItem;
                        if (switchToFocus)
                        {
                            SetViewMode(ManholePreviewMode.SingleFocus);
                        }
                        return HitTestResultBehavior.Stop;
                    }
                }
                return HitTestResultBehavior.Continue;
            }, hitParams);
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 0.88 : 1.14;
            _cameraDistance = Math.Clamp(_cameraDistance * zoomFactor, 1.0, 600.0);
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            double yawRad = _yaw * Math.PI / 180.0;
            double pitchRad = _pitch * Math.PI / 180.0;

            double x = _cameraDistance * Math.Cos(pitchRad) * Math.Sin(yawRad);
            double y = _cameraDistance * Math.Sin(pitchRad);
            double z = _cameraDistance * Math.Cos(pitchRad) * Math.Cos(yawRad);

            Point3D camPos = new Point3D(_targetCenter.X + x, _targetCenter.Y + y, _targetCenter.Z + z);
            Vector3D lookDir = _targetCenter - camPos;

            _camera.Position = camPos;
            _camera.LookDirection = lookDir;
            _camera.UpDirection = new Vector3D(0, 1, 0);
        }

        #endregion
    }
}
