using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using InfraBIM.CulvertTool.Models;

namespace InfraBIM.CulvertTool.Views
{
    /// <summary>
    /// Viewport 3D chuyên nghiệp mô phỏng cống ngang y chang Revit 3D View:
    /// - Không gian 3D tương tác thực: Xoay 360° (Orbit), Di chuyển (Pan), Thu phóng (Zoom)
    /// - Thân cống rỗng ruột (hollow box) từng đốt, phân biệt đốt tiêu chuẩn và đốt bù
    /// - Hộp nối cống (hố ga) nhô cao có nắp đan hố ga
    /// - Cửa xả thượng/hạ lưu với tường đầu vát dốc, tường cánh mở góc và bản đáy sân cống
    /// - Sân gia cố 3m nối tiếp ra dầm chân khay
    /// - Bê tông lót & đá dăm đệm ngắt chuẩn xác tại hộp nối
    /// - ViewCube điều khiển các góc nhìn chuẩn: Isometric 3D, Front, Top, Right, Fit
    /// </summary>
    public class Culvert3DViewportControl : Grid
    {
        #region Dependency Properties
        public static readonly DependencyProperty GeometryDataProperty =
            DependencyProperty.Register(
                nameof(GeometryData),
                typeof(CulvertPreviewGeometry),
                typeof(Culvert3DViewportControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryDataChanged));

        public CulvertPreviewGeometry? GeometryData
        {
            get => (CulvertPreviewGeometry?)GetValue(GeometryDataProperty);
            set => SetValue(GeometryDataProperty, value);
        }

        private static void OnGeometryDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Culvert3DViewportControl control)
            {
                control.RebuildModel();
                control.FitView();
            }
        }
        #endregion

        #region 3D Elements
        private readonly Viewport3D _viewport;
        private readonly PerspectiveCamera _camera;
        private readonly ModelVisual3D _modelVisual;
        private readonly Model3DGroup _rootModelGroup;

        // Camera Orbit & Pan State
        private Point3D _targetCenter = new Point3D(5.0, 0, 0);
        private double _cameraDistance = 18.0;
        private double _yaw = 42.0;    // degrees (rotation around Y)
        private double _pitch = 26.0;  // degrees (elevation angle)

        private Point _lastMousePos;
        private bool _isOrbiting = false;
        private bool _isPanning = false;
        private bool _showBedding = true;

        // UI Overlay Elements
        private readonly TextBlock _hudTitle;
        private readonly TextBlock _hudInfo;
        private readonly StackPanel _hudLegend;
        #endregion

        #region Materials
        // Vật liệu bê tông & kết cấu chuẩn Revit Precast Concrete Shaded (khớp 100% Hình 2)
        private static readonly Material MatStdSegment = CreateConcreteMaterial(Color.FromRgb(63, 68, 78), 20);      // Bê tông xám than đậm (#3F444E) như Hình 2
        private static readonly Material MatCompSegment = CreateConcreteMaterial(Color.FromRgb(71, 85, 105), 20);     // Đốt bù (Slate đậm)
        private static readonly Material MatWarnSegment = CreateConcreteMaterial(Color.FromRgb(239, 68, 68), 25);     // Đốt bù quá ngắn (Đỏ cảnh báo)
        private static readonly Material MatInnerVoid = CreateConcreteMaterial(Color.FromRgb(30, 41, 59), 5);          // Lòng trong cống rỗng
        private static readonly Material MatManhole = CreateConcreteMaterial(Color.FromRgb(2, 132, 199), 25);         // Hố ga / Hộp nối xanh dương (#0284C7) như Hình 2
        private static readonly Material MatManholeCover = CreateConcreteMaterial(Color.FromRgb(3, 105, 161), 30);       // Nắp hố ga xanh dương đậm (#0369A1)
        private static readonly Material MatHeadwall = CreateConcreteMaterial(Color.FromRgb(2, 132, 199), 25);       // Tường đầu & tường cánh xanh dương (#0284C7) như Hình 2
        private static readonly Material MatApronSlab = CreateConcreteMaterial(Color.FromRgb(3, 105, 161), 20);       // Bản đáy sân cống xanh dương (#0369A1)
        private static readonly Material MatReinforcedApron = CreateConcreteMaterial(Color.FromRgb(2, 132, 199), 20); // Sân gia cố xanh dương (#0284C7) như Hình 2
        private static readonly Material MatToeBeam = CreateConcreteMaterial(Color.FromRgb(3, 105, 161), 20);            // Dầm chân khay xanh dương đậm (#0369A1)
        private static readonly Material MatBTL = CreateConcreteMaterial(Color.FromRgb(148, 163, 184), 10);             // Bê tông lót đáy (#94A3B8)
        private static readonly Material MatCrushedStone = CreateConcreteMaterial(Color.FromRgb(202, 138, 4), 10);     // Đá dăm đệm (#CA8A04)
        private static readonly Material MatJoint = CreateConcreteMaterial(Color.FromRgb(15, 23, 42), 5);               // Khe nối / Gioăng
        private static readonly Material MatCenterline = CreateConcreteMaterial(Color.FromRgb(225, 29, 72), 60);        // Đường tim cống Magenta rực rỡ (#E11D48) như Hình 2
        private static readonly Material MatFlowArrow = CreateConcreteMaterial(Color.FromRgb(56, 189, 248), 50);        // Mũi tên dòng chảy
        private static readonly Material MatGrid = CreateConcreteMaterial(Color.FromArgb(120, 51, 65, 85), 0);          // Lưới mặt đất
        #endregion

        public Culvert3DViewportControl()
        {
            ClipToBounds = true;
            Background = new SolidColorBrush(Color.FromRgb(8, 13, 23)); // Dark engineering navy background

            // 1. Khởi tạo Camera 3D
            _camera = new PerspectiveCamera
            {
                FieldOfView = 45.0,
                NearPlaneDistance = 0.1,
                FarPlaneDistance = 1000.0,
                UpDirection = new Vector3D(0, 1, 0)
            };

            // 2. Khởi tạo Visual Model & Lights
            _rootModelGroup = new Model3DGroup();
            SetupLighting(_rootModelGroup);

            _modelVisual = new ModelVisual3D { Content = _rootModelGroup };

            // 3. Khởi tạo Viewport3D
            _viewport = new Viewport3D
            {
                Camera = _camera,
                ClipToBounds = true
            };
            _viewport.Children.Add(_modelVisual);
            Children.Add(_viewport);

            // 4. Tạo Overlay UI: Toolbar ViewCube, Thẻ thông tin HUD & Ghi chú phím tắt
            var overlayGrid = new Grid { IsHitTestVisible = true };
            Children.Add(overlayGrid);

            // a. HUD Thông tin ở góc trên bên trái
            var hudPanel = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(14, 14, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(215, 10, 17, 32)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(140, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 9, 14, 9)
            };

            var hudStack = new StackPanel();
            _hudTitle = new TextBlock
            {
                Text = "🧊 MÔ HÌNH 3D REVIT - REVIEW TRỰC QUAN CỐNG NGANG",
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                FontWeight = FontWeights.Bold,
                FontSize = 12.5,
                Margin = new Thickness(0, 0, 0, 4)
            };
            _hudInfo = new TextBlock
            {
                Text = "Chọn cống để hiển thị dữ liệu 3D...",
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                FontSize = 11.5,
                LineHeight = 16.0
            };
            var hudTips = new TextBlock
            {
                Text = "🖱️ Chuột trái: Xoay 360° (Orbit)  |  Chuột phải: Di chuyển (Pan)  |  Cuộn: Thu phóng (Zoom)  |  Đúp chuột: Fit",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 10.5,
                Margin = new Thickness(0, 5, 0, 0)
            };

            hudStack.Children.Add(_hudTitle);
            hudStack.Children.Add(_hudInfo);
            hudStack.Children.Add(hudTips);
            hudPanel.Child = hudStack;
            overlayGrid.Children.Add(hudPanel);

            // b. ViewCube Controls Toolbar ở góc trên bên phải
            var toolbarPanel = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 14, 14, 0),
                Background = new SolidColorBrush(Color.FromArgb(220, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 4, 6, 4)
            };

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal };

            Button CreateToolBtn(string label, string tooltip, RoutedEventHandler handler)
            {
                var btn = new Button
                {
                    Content = label,
                    ToolTip = tooltip,
                    Background = new SolidColorBrush(Color.FromArgb(230, 24, 42, 68)),
                    Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(180, 56, 189, 248)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(2, 0, 2, 0),
                    FontSize = 11.0,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = Cursors.Hand
                };
                btn.Click += handler;
                return btn;
            }

            btnStack.Children.Add(CreateToolBtn("🧊 3D ISO", "Phối cảnh góc Isometric 3D chuẩn", (s, e) => SetViewIsometric()));
            btnStack.Children.Add(CreateToolBtn("🏠 ĐỨNG", "Mặt đứng chiếu ngang (Front View)", (s, e) => SetViewFront()));
            btnStack.Children.Add(CreateToolBtn("📐 BẰNG", "Mặt bằng nhìn từ trên xuống (Top View)", (s, e) => SetViewTop()));
            btnStack.Children.Add(CreateToolBtn("⬛ BÊN", "Mặt cắt bên cống (Right Side View)", (s, e) => SetViewRight()));
            btnStack.Children.Add(CreateToolBtn("🎯 FIT", "Thu phóng vừa toàn bộ mô hình (hoặc đúp chuột)", (s, e) => FitView()));

            var btnToggleBedding = CreateToolBtn("👁️ BTL/ĐÁ", "Ẩn/Hiện lớp bê tông lót & đá dăm đệm", (s, e) =>
            {
                _showBedding = !_showBedding;
                RebuildModel();
            });
            btnStack.Children.Add(btnToggleBedding);

            toolbarPanel.Child = btnStack;
            overlayGrid.Children.Add(toolbarPanel);

            // c. Chú thích màu sắc (Legend HUD) ở góc dưới bên trái
            var legendPanel = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(14, 0, 0, 14),
                Background = new SolidColorBrush(Color.FromArgb(200, 10, 17, 32)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 148, 163, 184)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 6, 10, 6)
            };

            _hudLegend = new StackPanel { Orientation = Orientation.Horizontal };
            void AddLegendItem(string label, Color color)
            {
                var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
                var badge = new Border
                {
                    Width = 11,
                    Height = 11,
                    Background = new SolidColorBrush(color),
                    BorderBrush = new SolidColorBrush(Colors.White),
                    BorderThickness = new Thickness(0.5),
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 0, 5, 0)
                };
                var text = new TextBlock
                {
                    Text = label,
                    Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center
                };
                item.Children.Add(badge);
                item.Children.Add(text);
                _hudLegend.Children.Add(item);
            }

            AddLegendItem("Thân cống", Color.FromRgb(63, 68, 78));
            AddLegendItem("Hộp nối", Color.FromRgb(2, 132, 199));
            AddLegendItem("Cửa xả", Color.FromRgb(2, 132, 199));
            AddLegendItem("Sân gia cố", Color.FromRgb(2, 132, 199));
            AddLegendItem("Tim cống", Color.FromRgb(225, 29, 72));
            AddLegendItem("BTL & Đá", Color.FromRgb(202, 138, 4));

            legendPanel.Child = _hudLegend;
            overlayGrid.Children.Add(legendPanel);

            // 5. Đăng ký sự kiện chuột cho Orbit / Pan / Zoom
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseWheel += OnMouseWheel;
            MouseLeave += (s, e) => { _isOrbiting = false; _isPanning = false; };

            UpdateCamera();
        }

        #region Lighting Setup
        private static void SetupLighting(Model3DGroup group)
        {
            // Ánh sáng môi trường (Ambient)
            group.Children.Add(new AmbientLight(Color.FromRgb(95, 105, 120)));

            // Ánh sáng mặt trời chính (Key Light từ trên cao xuống)
            group.Children.Add(new DirectionalLight(Color.FromRgb(230, 235, 245), new Vector3D(-1.2, -2.5, -1.0)));

            // Ánh sáng phụ phản xạ (Fill Light từ hướng ngược lại làm nổi khối các mặt bóng râm)
            group.Children.Add(new DirectionalLight(Color.FromRgb(110, 120, 135), new Vector3D(1.2, 1.0, 1.5)));

            // Ánh sáng hắt chân (Rim Light) làm nổi lòng rỗng cống
            group.Children.Add(new DirectionalLight(Color.FromRgb(65, 75, 90), new Vector3D(0.0, -1.0, 2.0)));
        }
        #endregion

        #region Camera Navigation Methods (Orbit, Pan, Zoom)
        public void UpdateCamera()
        {
            double radYaw = _yaw * Math.PI / 180.0;
            double radPitch = _pitch * Math.PI / 180.0;

            double camX = _targetCenter.X + _cameraDistance * Math.Cos(radPitch) * Math.Sin(radYaw);
            double camY = _targetCenter.Y + _cameraDistance * Math.Sin(radPitch);
            double camZ = _targetCenter.Z + _cameraDistance * Math.Cos(radPitch) * Math.Cos(radYaw);

            _camera.Position = new Point3D(camX, camY, camZ);
            _camera.LookDirection = new Vector3D(_targetCenter.X - camX, _targetCenter.Y - camY, _targetCenter.Z - camZ);
            _camera.UpDirection = new Vector3D(0, 1, 0);
        }

        public void SetViewIsometric()
        {
            _yaw = 42.0;
            _pitch = 26.0;
            UpdateCamera();
        }

        public void SetViewFront()
        {
            _yaw = 0.0;
            _pitch = 0.0;
            UpdateCamera();
        }

        public void SetViewTop()
        {
            _yaw = 0.0;
            _pitch = 88.5; // Gần 90 độ để giữ UpDirection ổn định
            UpdateCamera();
        }

        public void SetViewRight()
        {
            _yaw = 90.0;
            _pitch = 0.0;
            UpdateCamera();
        }

        public void FitView()
        {
            if (GeometryData == null || GeometryData.TotalLengthM <= 0.1)
            {
                _targetCenter = new Point3D(5.0, 0, 0);
                _cameraDistance = 18.0;
            }
            else
            {
                double totalL = GeometryData.TotalLengthM;
                double midY = -GeometryData.DeltaH / 2.0;
                _targetCenter = new Point3D(totalL / 2.0, midY, 0.0);
                _cameraDistance = Math.Max(totalL * 1.35, 12.0);
            }
            UpdateCamera();
        }

        public void ZoomIn()
        {
            _cameraDistance = Math.Max(_cameraDistance * 0.82, 1.5);
            UpdateCamera();
        }

        public void ZoomOut()
        {
            _cameraDistance = Math.Min(_cameraDistance * 1.22, 500.0);
            UpdateCamera();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastMousePos = e.GetPosition(this);
            if (e.ChangedButton == MouseButton.Left)
            {
                _isOrbiting = true;
                CaptureMouse();
                if (e.ClickCount == 2)
                {
                    FitView();
                }
            }
            else if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = true;
                CaptureMouse();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point curPos = e.GetPosition(this);
            Vector delta = curPos - _lastMousePos;
            _lastMousePos = curPos;

            if (_isOrbiting)
            {
                _yaw += delta.X * 0.42;
                _pitch = Math.Clamp(_pitch - delta.Y * 0.42, -88.0, 88.0);
                UpdateCamera();
            }
            else if (_isPanning)
            {
                // Vector camera trục ngang & trục đứng
                Vector3D look = _camera.LookDirection;
                look.Normalize();
                Vector3D right = Vector3D.CrossProduct(look, _camera.UpDirection);
                right.Normalize();
                Vector3D up = _camera.UpDirection;

                double panSpeed = _cameraDistance * 0.0016;
                _targetCenter -= right * (delta.X * panSpeed);
                _targetCenter += up * (delta.Y * panSpeed);

                UpdateCamera();
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) _isOrbiting = false;
            if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle) _isPanning = false;

            if (!_isOrbiting && !_isPanning)
            {
                ReleaseMouseCapture();
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 0.86 : 1.16;
            _cameraDistance = Math.Clamp(_cameraDistance * factor, 1.5, 400.0);
            UpdateCamera();
            e.Handled = true;
        }
        #endregion

        #region 3D Model Construction Engine
        public void RebuildModel()
        {
            // Giữ lại các đèn chiếu sáng, xóa bỏ các mesh hình học cũ
            while (_rootModelGroup.Children.Count > 4)
            {
                _rootModelGroup.Children.RemoveAt(_rootModelGroup.Children.Count - 1);
            }

            var geom = GeometryData;
            if (geom == null || geom.TotalLengthM <= 0.1)
            {
                _hudInfo.Text = "Chưa có dữ liệu cống để mô phỏng 3D.";
                return;
            }

            double totalL = geom.TotalLengthM;
            double bW = geom.BarrelWidthM > 0 ? geom.BarrelWidthM : 1.5;   // Khẩu độ rộng trong
            double bH = geom.BarrelHeightM > 0 ? geom.BarrelHeightM : 1.5;   // Khẩu độ cao trong
            double tWall = geom.WallThicknessM > 0 ? geom.WallThicknessM : 0.20; // Chiều dày thành cống
            double tSlab = tWall;                                            // Chiều dày bản đáy và nắp
            double wOut = bW + (2.0 * tWall);                                // Bề rộng phủ bì cống
            double hOut = bH + (2.0 * tSlab);                                // Chiều cao phủ bì cống

            // Hàm tính độ hạ cao độ đáy tại vị trí X dọc tuyến (theo độ dốc thủy lực thực tế)
            double Drop(double x) => totalL > 0.01 ? (x / totalL) * geom.DeltaH : 0.0;

            // Cập nhật thẻ HUD
            _hudInfo.Text = $"📍 Cống STT {geom.STT} | Lý trình: {geom.LyTrinh} | {geom.LoaiCong} ({geom.KhauDo})\n" +
                            $"📏 Tổng chiều dài: L = {totalL:N2}m | Số đốt: {geom.Segments.Count} đốt | Hộp nối: {geom.Manholes.Count} hộp\n" +
                            $"📐 Đáy TL Z1 = {geom.Z1:N3}m ➔ Đáy HL Z2 = {geom.Z2:N3}m (ΔH = {geom.DeltaH:N3}m | i = {geom.CalculatedSlopePercent:N2}%)";

            // 0. NẾU CÓ MÔ HÌNH THỰC TẾ TRÍCH XUẤT TỪ REVIT DOCUMENT (REAL REVIT MESHES - YÊU CẦU 1 & 2)
            if (geom.RealRevitMeshes != null && geom.RealRevitMeshes.Count > 0)
            {
                _hudTitle.Text = "🧊 MÔ HÌNH 3D REVIT THỰC TẾ (REAL REVIT FAMILY GEOMETRY)";
                _hudInfo.Text = $"📍 Cống STT {geom.STT} | Lý trình: {geom.LyTrinh} | {geom.LoaiCong} ({geom.KhauDo})\n" +
                                $"⚡ Đang hiển thị 100% hình học thực tế trích xuất từ Revit ({geom.RealRevitMeshes.Count} cấu kiện Solid/Mesh)\n" +
                                $"📐 Đáy TL Z1 = {geom.Z1:N3}m ➔ Đáy HL Z2 = {geom.Z2:N3}m (ΔH = {geom.DeltaH:N3}m | i = {geom.CalculatedSlopePercent:N2}%)";

                foreach (var (mesh, brush) in geom.RealRevitMeshes)
                {
                    var matGrp = new MaterialGroup();
                    matGrp.Children.Add(new DiffuseMaterial(brush));
                    matGrp.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 20));
                    var model = new GeometryModel3D(mesh, matGrp) { BackMaterial = matGrp };
                    _rootModelGroup.Children.Add(model);
                }

                // Vẽ đường tim cống Magenta xuyên tâm như trong Revit Hình 2
                BuildCenterline3D(_rootModelGroup, totalL, bH, Drop);

                // Lưới mặt đất kỹ thuật tham chiếu
                BuildGroundGrid(_rootModelGroup, totalL, wOut, Drop(totalL));

                // Mũi tên dòng chảy
                BuildFlowArrow(_rootModelGroup, totalL, bH, Drop);

                return;
            }

            _hudTitle.Text = "🧊 MÔ HÌNH 3D REVIT - REVIEW TRỰC QUAN CỐNG NGANG";

            // 1. VẼ LƯỚI MẶT ĐẤT THAM CHIẾU (GROUND REFERENCE GRID)
            BuildGroundGrid(_rootModelGroup, totalL, wOut, Drop(totalL));

            // 2. VẼ THÂN CỐNG TỪNG ĐỐT RỖNG RUỘT 3D CHUẨN XÁC THEO ĐỘ DỐC THỰC TẾ (HÌNH 2)
            const double jointGap = 0.02; // Khe nối 20mm giữa các đốt cống
            foreach (var seg in geom.Segments)
            {
                double x0 = seg.StartDistanceM + (jointGap / 2.0);
                double x1 = seg.EndDistanceM - (jointGap / 2.0);
                if (x1 <= x0) continue;

                double y0 = -Drop(x0);
                double y1 = -Drop(x1);

                Material segMat = seg.IsStandard
                    ? MatStdSegment
                    : (seg.LengthM < geom.L_Min ? MatWarnSegment : MatCompSegment);

                // Bản đáy cống (Bottom Slab) vuốt dốc
                AddSlopedBoxMesh(_rootModelGroup, x0, y0 - tSlab, y0, x1, y1 - tSlab, y1, -wOut / 2.0, wOut / 2.0, segMat);

                // Bản nắp cống (Top Slab) vuốt dốc
                AddSlopedBoxMesh(_rootModelGroup, x0, y0 + bH, y0 + bH + tSlab, x1, y1 + bH, y1 + bH + tSlab, -wOut / 2.0, wOut / 2.0, segMat);

                // Vách bên trái (Left Sidewall)
                AddSlopedBoxMesh(_rootModelGroup, x0, y0, y0 + bH, x1, y1, y1 + bH, -wOut / 2.0, -wOut / 2.0 + tWall, segMat);

                // Vách bên phải (Right Sidewall)
                AddSlopedBoxMesh(_rootModelGroup, x0, y0, y0 + bH, x1, y1, y1 + bH, wOut / 2.0 - tWall, wOut / 2.0, segMat);

                // Nếu là cống đôi (cống hộp 2 cửa), thêm vách ngăn giữa
                if (geom.SoCua >= 2 && !geom.IsRoundCulvert)
                {
                    AddSlopedBoxMesh(_rootModelGroup, x0, y0, y0 + bH, x1, y1, y1 + bH, -tWall / 2.0, tWall / 2.0, segMat);
                }

                // Vành gioăng / ron mối nối giữa các đốt
                if (seg.EndDistanceM < totalL - 0.05)
                {
                    double jx0 = seg.EndDistanceM - (jointGap / 2.0);
                    double jx1 = seg.EndDistanceM + (jointGap / 2.0);
                    double jy0 = -Drop(jx0);
                    double jy1 = -Drop(jx1);
                    AddSlopedBoxMesh(_rootModelGroup, jx0, jy0 - tSlab - 0.02, jy0 + bH + tSlab + 0.02, jx1, jy1 - tSlab - 0.02, jy1 + bH + tSlab + 0.02, -wOut / 2.0 - 0.02, wOut / 2.0 + 0.02, MatJoint);
                }
            }

            // Vẽ đường tim cống Magenta rực rỡ xuyên qua tâm cống như Hình 2
            BuildCenterline3D(_rootModelGroup, totalL, bH, Drop);

            // 3. VẼ HỘP NỐI CỐNG (HỐ GA) 3D MÀU XANH DƯƠNG NHƯ HÌNH 2
            foreach (var mh in geom.Manholes)
            {
                double xMh = mh.DistanceFromP1M;
                double wMh = Math.Max(mh.WidthM, 1.2);
                double xMh0 = xMh - (wMh / 2.0);
                double xMh1 = xMh + (wMh / 2.0);
                double yMh = -Drop(xMh);
                double mhW = wOut + 0.45;                // Hộp nối rộng hơn thân cống
                double mhTopY = yMh + bH + tSlab + 0.45; // Nhô cao hơn đỉnh cống 0.45m
                double mhBotY = yMh - tSlab - 0.20;      // Đáy hố ga hạ sâu hơn đáy cống

                // Thân hố ga màu xanh dương (#0284C7)
                AddSolidBoxMesh(_rootModelGroup, xMh0, mhBotY, -mhW / 2.0, xMh1, mhTopY, mhW / 2.0, MatManhole);

                // Nắp đan hố ga phía trên cùng màu xanh đậm (#0369A1)
                double capThick = 0.12;
                AddSolidBoxMesh(_rootModelGroup, xMh0 - 0.03, mhTopY, -mhW / 2.0 - 0.03, xMh1 + 0.03, mhTopY + capThick, mhW / 2.0 + 0.03, MatManholeCover);

                // Các rãnh gân ngang trên nắp hộp nối như trong Hình 2
                int numGrooves = 4;
                double gStep = (xMh1 - xMh0) / (numGrooves + 1);
                for (int g = 1; g <= numGrooves; g++)
                {
                    double gx = xMh0 + g * gStep;
                    AddSolidBoxMesh(_rootModelGroup, gx - 0.02, mhTopY + capThick, -mhW / 2.0, gx + 0.02, mhTopY + capThick + 0.015, mhW / 2.0, MatManhole);
                }
            }

            // 4. VẼ CỬA XẢ THƯỢNG LƯU & HẠ LƯU (TƯỜNG ĐẦU, CỔNG PORTAL & TƯỜNG CÁNH TAM GIÁC MÀU XANH)
            // A. Thượng lưu (P1, X = 0)
            BuildInletOutlet3D(_rootModelGroup, 0.0, 0.0, wOut, bH, tSlab, tWall, isUpstream: true);

            // B. Hạ lưu (P2, X = totalL)
            BuildInletOutlet3D(_rootModelGroup, totalL, -Drop(totalL), wOut, bH, tSlab, tWall, isUpstream: false);

            // 5. VẼ SÂN GIA CỐ 3M & DẦM CHÂN KHAY KÈM LỚP BÊ TÔNG LÓT (ĐỦ CÁC CẤU KIỆN SÂN GIA CỐ)
            double sgcLen = 3.0;
            double sgcW = wOut + 2.2;
            double sgcThick = 0.20;
            double sgcBtlThick = 0.10;
            double toeDepth = 0.60;
            double toeThick = 0.35;

            // A. Thượng lưu (từ X = -2.0m nối tiếp ra -5.0m)
            // Cấu kiện 1: Bản sân gia cố
            AddSolidBoxMesh(_rootModelGroup, -2.0 - sgcLen, -tSlab - sgcThick, -sgcW / 2.0, -2.0, -tSlab, sgcW / 2.0, MatReinforcedApron);
            // Dầm chân khay thượng lưu cắm sâu xuống đất ở mép ngoài cùng
            AddSolidBoxMesh(_rootModelGroup, -2.0 - sgcLen - toeThick, -tSlab - sgcThick - toeDepth, -sgcW / 2.0, -2.0 - sgcLen, -tSlab, sgcW / 2.0, MatToeBeam);
            // Cấu kiện 2: Bê tông lót sân gia cố (BTL SGC) dày 100mm
            if (_showBedding)
            {
                AddSolidBoxMesh(_rootModelGroup, -2.0 - sgcLen - toeThick, -tSlab - sgcThick - sgcBtlThick, -sgcW / 2.0 - 0.10, -2.0, -tSlab - sgcThick, sgcW / 2.0 + 0.10, MatBTL);
            }

            // B. Hạ lưu (từ X = totalL + 2.0m nối tiếp ra totalL + 5.0m)
            double yHl = -Drop(totalL);
            // Cấu kiện 1: Bản sân gia cố
            AddSolidBoxMesh(_rootModelGroup, totalL + 2.0, yHl - tSlab - sgcThick, -sgcW / 2.0, totalL + 2.0 + sgcLen, yHl - tSlab, sgcW / 2.0, MatReinforcedApron);
            // Dầm chân khay hạ lưu cắm sâu xuống đất ở mép ngoài cùng
            AddSolidBoxMesh(_rootModelGroup, totalL + 2.0 + sgcLen, yHl - tSlab - sgcThick - toeDepth, -sgcW / 2.0, totalL + 2.0 + sgcLen + toeThick, yHl - tSlab, sgcW / 2.0, MatToeBeam);
            // Cấu kiện 2: Bê tông lót sân gia cố hạ lưu (BTL SGC) dày 100mm
            if (_showBedding)
            {
                AddSolidBoxMesh(_rootModelGroup, totalL + 2.0, yHl - tSlab - sgcThick - sgcBtlThick, -sgcW / 2.0 - 0.10, totalL + 2.0 + sgcLen + toeThick, yHl - tSlab - sgcThick, sgcW / 2.0 + 0.10, MatBTL);
            }

            // 6. VẼ BÊ TÔNG LÓT & ĐÁ DĂM ĐỆM (NGẮT QUÃNG CHUẨN XÁC TẠI HỘP NỐI)
            if (_showBedding)
            {
                BuildBeddingLayers(_rootModelGroup, geom, totalL, wOut, tSlab, Drop);
            }

            // 7. VẼ MŨI TÊN DÒNG CHẢY 3D TRỰC QUAN (WATER FLOW DIRECTION)
            BuildFlowArrow(_rootModelGroup, totalL, bH, Drop);
        }

        private static void BuildInletOutlet3D(Model3DGroup group, double xCenter, double yCenter, double wOut, double bH, double tSlab, double tWall, bool isUpstream)
        {
            double dir = isUpstream ? -1.0 : 1.0;
            double apronLen = 2.0;
            double xApron0 = isUpstream ? xCenter - apronLen : xCenter;
            double xApron1 = isUpstream ? xCenter : xCenter + apronLen;
            double apronW = wOut + 0.6;

            // 1. Bản đáy sân cống (Apron Slab - Cấu kiện 1) màu xanh (#0284C7)
            AddSolidBoxMesh(group, xApron0, yCenter - tSlab, -apronW / 2.0, xApron1, yCenter, apronW / 2.0, MatHeadwall);

            // 2. Tường đầu (Headwall - Cấu kiện 2) dạng cổng portal khoét rỗng cho lòng cống đi qua màu xanh (#0284C7)
            double hwThick = 0.40;
            double xHw0 = isUpstream ? xCenter - hwThick : xCenter;
            double xHw1 = isUpstream ? xCenter : xCenter + hwThick;
            double hwTopY = yCenter + bH + tSlab + 0.45;
            double postW = 0.35;

            // Trụ bên trái portal
            AddSolidBoxMesh(group, xHw0, yCenter, -wOut / 2.0 - postW, xHw1, hwTopY, -wOut / 2.0 + tWall, MatHeadwall);
            // Trụ bên phải portal
            AddSolidBoxMesh(group, xHw0, yCenter, wOut / 2.0 - tWall, xHw1, hwTopY, wOut / 2.0 + postW, MatHeadwall);
            // Dầm đỉnh portal (Lintel)
            AddSolidBoxMesh(group, xHw0, yCenter + bH, -wOut / 2.0 - postW, xHw1, hwTopY, wOut / 2.0 + postW, MatHeadwall);

            // 3. Hai tường cánh tam giác vát dốc 45° (Triangular Flared Wingwalls - Cấu kiện 3) màu xanh (#0284C7)
            double wingLen = 2.0;
            double wingFlare = 1.25;
            double wingThick = 0.25;

            // Tường cánh bên trái (tam giác vát dốc từ đỉnh tường đầu xuống mép sân)
            Point3D wTL_top = new Point3D(xCenter, hwTopY, -wOut / 2.0 - postW);
            Point3D wTL_bot_inner = new Point3D(xCenter, yCenter, -wOut / 2.0 - postW);
            Point3D wTL_bot_outer = new Point3D(xCenter + (dir * wingLen), yCenter, -wOut / 2.0 - postW - wingFlare);
            AddTriangularWingWall(group, wTL_bot_inner, wTL_bot_outer, wTL_top, new Vector3D(0, 0, wingThick), MatHeadwall);

            // Tường cánh bên phải (tam giác vát dốc từ đỉnh tường đầu xuống mép sân)
            Point3D wTR_top = new Point3D(xCenter, hwTopY, wOut / 2.0 + postW);
            Point3D wTR_bot_inner = new Point3D(xCenter, yCenter, wOut / 2.0 + postW);
            Point3D wTR_bot_outer = new Point3D(xCenter + (dir * wingLen), yCenter, wOut / 2.0 + postW + wingFlare);
            AddTriangularWingWall(group, wTR_bot_inner, wTR_bot_outer, wTR_top, new Vector3D(0, 0, -wingThick), MatHeadwall);

            // 4. Bê tông lót sân cống (Apron BTL - Cấu kiện 4) dày 100mm nằm dưới bản đáy sân cống
            double btlThick = 0.10;
            double btlW = apronW + 0.20;
            double xBtl0 = isUpstream ? xApron0 - 0.10 : xApron0;
            double xBtl1 = isUpstream ? xApron1 : xApron1 + 0.10;
            AddSolidBoxMesh(group, xBtl0, yCenter - tSlab - btlThick, -btlW / 2.0, xBtl1, yCenter - tSlab, btlW / 2.0, MatBTL);

            // 5. Đá dăm đệm sân cống (Apron DDD - Cấu kiện 5) dày 150mm nằm dưới lớp BTL
            double stoneThick = 0.15;
            double stoneW = apronW + 0.40;
            double xStone0 = isUpstream ? xApron0 - 0.20 : xApron0;
            double xStone1 = isUpstream ? xApron1 : xApron1 + 0.20;
            AddSolidBoxMesh(group, xStone0, yCenter - tSlab - btlThick - stoneThick, -stoneW / 2.0, xStone1, yCenter - tSlab - btlThick, stoneW / 2.0, MatCrushedStone);
        }

        private static void BuildBeddingLayers(Model3DGroup group, CulvertPreviewGeometry geom, double totalL, double wOut, double tSlab, Func<double, double> dropFunc)
        {
            // Xác định các đoạn rải BTL & Đá dăm (loại trừ các vị trí hộp nối)
            var spans = new List<(double Start, double End)>();
            if (geom.Manholes.Count == 0)
            {
                spans.Add((0.0, totalL));
            }
            else
            {
                double curX = 0.0;
                foreach (var mh in geom.Manholes)
                {
                    double mhW = Math.Max(mh.WidthM, 1.2);
                    double mhStart = Math.Max(0.0, mh.DistanceFromP1M - (mhW / 2.0) - 0.05);
                    double mhEnd = Math.Min(totalL, mh.DistanceFromP1M + (mhW / 2.0) + 0.05);

                    if (mhStart > curX + 0.1)
                    {
                        spans.Add((curX, mhStart));
                    }
                    curX = Math.Max(curX, mhEnd);
                }
                if (curX < totalL - 0.1)
                {
                    spans.Add((curX, totalL));
                }
            }

            double btlThick = 0.10;
            double stoneThick = 0.15;
            double btlW = wOut + 0.20;
            double stoneW = wOut + 0.40;

            foreach (var span in spans)
            {
                double x0 = span.Start;
                double x1 = span.End;
                double y0 = -dropFunc(x0) - tSlab;
                double y1 = -dropFunc(x1) - tSlab;

                // 1. Lớp Bê tông lót (Lean concrete layer) dày 100mm
                AddSolidBoxMesh(group, x0, y0 - btlThick, -btlW / 2.0, x1, y1, btlW / 2.0, MatBTL);

                // 2. Lớp Đá dăm đệm (Crushed stone layer) dày 150mm nằm dưới BTL
                AddSolidBoxMesh(group, x0, y0 - btlThick - stoneThick, -stoneW / 2.0, x1, y1 - btlThick, stoneW / 2.0, MatCrushedStone);
            }
        }

        private static void BuildFlowArrow(Model3DGroup group, double totalL, double bH, Func<double, double> dropFunc)
        {
            if (totalL <= 0.5) return;
            double midX = totalL / 2.0;
            double midY = -dropFunc(midX) + (bH * 0.4);
            double arrowLen = Math.Min(2.5, totalL * 0.3);

            // Thân mũi tên
            AddSolidBoxMesh(group, midX - (arrowLen / 2.0), midY - 0.06, -0.06, midX + (arrowLen * 0.15), midY + 0.06, 0.06, MatFlowArrow);

            // Đầu mũi tên (hình chóp tam giác)
            Point3D tip = new Point3D(midX + (arrowLen / 2.0), midY, 0.0);
            Point3D baseT = new Point3D(midX + (arrowLen * 0.15), midY + 0.22, 0.0);
            Point3D baseB = new Point3D(midX + (arrowLen * 0.15), midY - 0.22, 0.0);
            Point3D baseL = new Point3D(midX + (arrowLen * 0.15), midY, -0.22);
            Point3D baseR = new Point3D(midX + (arrowLen * 0.15), midY, 0.22);

            var mesh = new MeshGeometry3D();
            AddTriangle(mesh, tip, baseT, baseL);
            AddTriangle(mesh, tip, baseL, baseB);
            AddTriangle(mesh, tip, baseB, baseR);
            AddTriangle(mesh, tip, baseR, baseT);
            var model = new GeometryModel3D(mesh, MatFlowArrow) { BackMaterial = MatFlowArrow };
            group.Children.Add(model);
        }

        private static void BuildGroundGrid(Model3DGroup group, double totalL, double wOut, double maxDrop)
        {
            double groundY = -Math.Max(maxDrop, 0) - 1.2;
            double startX = -6.0;
            double endX = totalL + 6.0;
            double startZ = -wOut - 4.0;
            double endZ = wOut + 4.0;

            // Tấm mặt đất phẳng mờ
            var groundMesh = new MeshGeometry3D();
            AddQuad(groundMesh,
                new Point3D(startX, groundY, endZ),
                new Point3D(endX, groundY, endZ),
                new Point3D(endX, groundY, startZ),
                new Point3D(startX, groundY, startZ));
            group.Children.Add(new GeometryModel3D(groundMesh, MatGrid) { BackMaterial = MatGrid });

            // Các đường kẻ lưới kỹ thuật (Grid Lines)
            const double step = 2.0;
            for (double x = startX; x <= endX + 0.1; x += step)
            {
                AddSolidBoxMesh(group, x - 0.015, groundY, startZ, x + 0.015, groundY + 0.01, endZ, MatInnerVoid);
            }
            for (double z = startZ; z <= endZ + 0.1; z += step)
            {
                AddSolidBoxMesh(group, startX, groundY, z - 0.015, endX, groundY + 0.01, z + 0.015, MatInnerVoid);
            }
        }
        #endregion

        #region Mesh Generation Helpers (Boxes, Walls, Quads)
        private static void BuildCenterline3D(Model3DGroup group, double totalL, double bH, Func<double, double> dropFunc)
        {
            if (totalL <= 0.1) return;
            double r = 0.025; // Đường tim cống bán kính 25mm
            double startX = -3.0;
            double endX = totalL + 3.0;
            double step = 1.5;

            for (double x = startX; x < endX; x += step)
            {
                double xNext = Math.Min(x + step, endX);
                double y0 = -dropFunc(x) + (bH / 2.0);
                double y1 = -dropFunc(xNext) + (bH / 2.0);
                AddSlopedBoxMesh(group, x, y0 - r, y0 + r, xNext, y1 - r, y1 + r, -r, r, MatCenterline);
            }
        }

        private static void AddSlopedBoxMesh(
            Model3DGroup group,
            double x0, double yBot0, double yTop0,
            double x1, double yBot1, double yTop1,
            double z0, double z1,
            Material mat)
        {
            var mesh = new MeshGeometry3D();

            Point3D p0 = new Point3D(x0, yBot0, z0);
            Point3D p1 = new Point3D(x0, yBot0, z1);
            Point3D p2 = new Point3D(x0, yTop0, z1);
            Point3D p3 = new Point3D(x0, yTop0, z0);

            Point3D p4 = new Point3D(x1, yBot1, z0);
            Point3D p5 = new Point3D(x1, yBot1, z1);
            Point3D p6 = new Point3D(x1, yTop1, z1);
            Point3D p7 = new Point3D(x1, yTop1, z0);

            // Đáy (-Y)
            AddQuad(mesh, p0, p1, p5, p4);
            // Nắp (+Y)
            AddQuad(mesh, p3, p7, p6, p2);
            // Trái (-Z)
            AddQuad(mesh, p0, p4, p7, p3);
            // Phải (+Z)
            AddQuad(mesh, p1, p2, p6, p5);
            // Đầu X0 (-X)
            AddQuad(mesh, p0, p3, p2, p1);
            // Cuối X1 (+X)
            AddQuad(mesh, p4, p5, p6, p7);

            var model = new GeometryModel3D(mesh, mat) { BackMaterial = mat };
            group.Children.Add(model);
        }

        private static void AddTriangularWingWall(Model3DGroup group, Point3D pBotInner, Point3D pBotOuter, Point3D pTopInner, Vector3D thickOffset, Material mat)
        {
            Point3D qBotInner = pBotInner + thickOffset;
            Point3D qBotOuter = pBotOuter + thickOffset;
            Point3D qTopInner = pTopInner + thickOffset;

            var mesh = new MeshGeometry3D();
            // Mặt tam giác trước
            AddTriangle(mesh, pBotInner, pBotOuter, pTopInner);
            // Mặt tam giác sau
            AddTriangle(mesh, qBotInner, qTopInner, qBotOuter);
            // Đáy
            AddQuad(mesh, pBotInner, qBotInner, qBotOuter, pBotOuter);
            // Cạnh vát xiên dốc (hypotenuse)
            AddQuad(mesh, pBotOuter, qBotOuter, qTopInner, pTopInner);
            // Cạnh đứng giáp tường đầu
            AddQuad(mesh, pTopInner, qTopInner, qBotInner, pBotInner);

            var model = new GeometryModel3D(mesh, mat) { BackMaterial = mat };
            group.Children.Add(model);
        }

        private static void AddSolidBoxMesh(Model3DGroup group, double x0, double y0, double z0, double x1, double y1, double z1, Material mat)
        {
            var mesh = new MeshGeometry3D();

            Point3D p0 = new Point3D(x0, y0, z0);
            Point3D p1 = new Point3D(x1, y0, z0);
            Point3D p2 = new Point3D(x1, y0, z1);
            Point3D p3 = new Point3D(x0, y0, z1);

            Point3D p4 = new Point3D(x0, y1, z0);
            Point3D p5 = new Point3D(x1, y1, z0);
            Point3D p6 = new Point3D(x1, y1, z1);
            Point3D p7 = new Point3D(x0, y1, z1);

            // Bottom (-Y)
            AddQuad(mesh, p3, p2, p1, p0);
            // Top (+Y)
            AddQuad(mesh, p4, p5, p6, p7);
            // Front (+Z)
            AddQuad(mesh, p7, p6, p2, p3);
            // Back (-Z)
            AddQuad(mesh, p5, p4, p0, p1);
            // Left (-X)
            AddQuad(mesh, p4, p7, p3, p0);
            // Right (+X)
            AddQuad(mesh, p6, p5, p1, p2);

            var model = new GeometryModel3D(mesh, mat) { BackMaterial = mat };
            group.Children.Add(model);
        }

        private static void AddThickWallMesh(Model3DGroup group, Point3D p0, Point3D p1, Point3D p2, Point3D p3, Vector3D thickOffset, Material mat)
        {
            Point3D q0 = p0 + thickOffset;
            Point3D q1 = p1 + thickOffset;
            Point3D q2 = p2 + thickOffset;
            Point3D q3 = p3 + thickOffset;

            var mesh = new MeshGeometry3D();
            // Mặt trước
            AddQuad(mesh, p0, p1, p2, p3);
            // Mặt sau
            AddQuad(mesh, q3, q2, q1, q0);
            // Các mặt bên bao quanh
            AddQuad(mesh, p3, p2, q2, q3); // Đỉnh
            AddQuad(mesh, p1, p0, q0, q1); // Đáy
            AddQuad(mesh, p0, p3, q3, q0); // Đầu
            AddQuad(mesh, p2, p1, q1, q2); // Đuôi

            var model = new GeometryModel3D(mesh, mat) { BackMaterial = mat };
            group.Children.Add(model);
        }

        private static void AddQuad(MeshGeometry3D mesh, Point3D p0, Point3D p1, Point3D p2, Point3D p3)
        {
            int baseIdx = mesh.Positions.Count;
            mesh.Positions.Add(p0);
            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);
            mesh.Positions.Add(p3);

            Vector3D n = Vector3D.CrossProduct(p1 - p0, p2 - p0);
            if (n.LengthSquared > 1e-6) n.Normalize();
            else n = new Vector3D(0, 1, 0);

            mesh.Normals.Add(n);
            mesh.Normals.Add(n);
            mesh.Normals.Add(n);
            mesh.Normals.Add(n);

            mesh.TriangleIndices.Add(baseIdx);
            mesh.TriangleIndices.Add(baseIdx + 1);
            mesh.TriangleIndices.Add(baseIdx + 2);

            mesh.TriangleIndices.Add(baseIdx);
            mesh.TriangleIndices.Add(baseIdx + 2);
            mesh.TriangleIndices.Add(baseIdx + 3);
        }

        private static void AddTriangle(MeshGeometry3D mesh, Point3D p0, Point3D p1, Point3D p2)
        {
            int baseIdx = mesh.Positions.Count;
            mesh.Positions.Add(p0);
            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);

            Vector3D n = Vector3D.CrossProduct(p1 - p0, p2 - p0);
            if (n.LengthSquared > 1e-6) n.Normalize();
            else n = new Vector3D(0, 1, 0);

            mesh.Normals.Add(n);
            mesh.Normals.Add(n);
            mesh.Normals.Add(n);

            mesh.TriangleIndices.Add(baseIdx);
            mesh.TriangleIndices.Add(baseIdx + 1);
            mesh.TriangleIndices.Add(baseIdx + 2);
        }

        private static Material CreateConcreteMaterial(Color color, double specularPower = 20)
        {
            var grp = new MaterialGroup();
            grp.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            if (specularPower > 0)
            {
                grp.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), specularPower));
            }
            return grp;
        }
        #endregion
    }
}
