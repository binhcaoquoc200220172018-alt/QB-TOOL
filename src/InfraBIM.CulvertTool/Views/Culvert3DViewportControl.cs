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
        // Vật liệu bê tông chuẩn Revit Precast Concrete (Shaded visual style)
        private static readonly Material MatStdSegment = CreateConcreteMaterial(Color.FromRgb(210, 218, 226), 15);      // Bê tông xám sáng
        private static readonly Material MatCompSegment = CreateConcreteMaterial(Color.FromRgb(147, 197, 253), 20);     // Đốt bù (Xanh ngọc / Mint nhận diện)
        private static readonly Material MatWarnSegment = CreateConcreteMaterial(Color.FromRgb(248, 113, 113), 20);     // Đốt bù quá ngắn (Đỏ cảnh báo)
        private static readonly Material MatInnerVoid = CreateConcreteMaterial(Color.FromRgb(51, 65, 85), 5);          // Lòng trong cống rỗng
        private static readonly Material MatManhole = CreateConcreteMaterial(Color.FromRgb(148, 163, 184), 15);         // Hố ga / Hộp nối
        private static readonly Material MatManholeCover = CreateConcreteMaterial(Color.FromRgb(30, 41, 59), 40);       // Nắp gang hố ga
        private static readonly Material MatHeadwall = CreateConcreteMaterial(Color.FromRgb(186, 200, 218), 15);       // Tường đầu & tường cánh
        private static readonly Material MatApronSlab = CreateConcreteMaterial(Color.FromRgb(170, 185, 205), 15);       // Bản đáy sân cống
        private static readonly Material MatReinforcedApron = CreateConcreteMaterial(Color.FromRgb(120, 113, 108), 10); // Sân gia cố đá hộc / BT
        private static readonly Material MatToeBeam = CreateConcreteMaterial(Color.FromRgb(87, 83, 78), 10);            // Dầm chân khay
        private static readonly Material MatBTL = CreateConcreteMaterial(Color.FromRgb(100, 116, 139), 10);             // Bê tông lót đáy
        private static readonly Material MatCrushedStone = CreateConcreteMaterial(Color.FromRgb(194, 120, 50), 10);     // Đá dăm đệm
        private static readonly Material MatJoint = CreateConcreteMaterial(Color.FromRgb(15, 23, 42), 5);               // Khe nối / Gioăng
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

            AddLegendItem("Đốt chuẩn", Color.FromRgb(210, 218, 226));
            AddLegendItem("Đốt bù", Color.FromRgb(147, 197, 253));
            AddLegendItem("Hố ga", Color.FromRgb(148, 163, 184));
            AddLegendItem("Cửa xả", Color.FromRgb(186, 200, 218));
            AddLegendItem("Sân gia cố", Color.FromRgb(120, 113, 108));
            AddLegendItem("BTL & Đá", Color.FromRgb(194, 120, 50));

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

            // 1. VẼ LƯỚI MẶT ĐẤT THAM CHIẾU (GROUND REFERENCE GRID)
            BuildGroundGrid(_rootModelGroup, totalL, wOut, Drop(totalL));

            // 2. VẼ THÂN CỐNG TỪNG ĐỐT RỖNG RUỘT 3D (HOLLOW BOX CULVERT SEGMENTS)
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

                // Bản đáy cống (Bottom Slab)
                AddSolidBoxMesh(_rootModelGroup, x0, y0 - tSlab, -wOut / 2.0, x1, y1, wOut / 2.0, segMat);

                // Bản nắp cống (Top Slab)
                AddSolidBoxMesh(_rootModelGroup, x0, y0 + bH, -wOut / 2.0, x1, y1 + bH + tSlab, wOut / 2.0, segMat);

                // Vách bên trái (Left Sidewall)
                AddSolidBoxMesh(_rootModelGroup, x0, y0, -wOut / 2.0, x1, y1 + bH, -wOut / 2.0 + tWall, segMat);

                // Vách bên phải (Right Sidewall)
                AddSolidBoxMesh(_rootModelGroup, x0, y0, wOut / 2.0 - tWall, x1, y1 + bH, wOut / 2.0, segMat);

                // Nếu là cống đôi (cống hộp 2 cửa), thêm vách ngăn giữa
                if (geom.SoCua >= 2 && !geom.IsRoundCulvert)
                {
                    AddSolidBoxMesh(_rootModelGroup, x0, y0, -tWall / 2.0, x1, y1 + bH, tWall / 2.0, segMat);
                }

                // Vành gioăng / ron mối nối giữa các đốt
                if (seg.EndDistanceM < totalL - 0.05)
                {
                    double jx0 = seg.EndDistanceM - (jointGap / 2.0);
                    double jx1 = seg.EndDistanceM + (jointGap / 2.0);
                    double jy = -Drop(seg.EndDistanceM);
                    AddSolidBoxMesh(_rootModelGroup, jx0, jy - tSlab - 0.02, -wOut / 2.0 - 0.02, jx1, jy + bH + tSlab + 0.02, wOut / 2.0 + 0.02, MatJoint);
                }
            }

            // 3. VẼ HỘP NỐI CỐNG (HỐ GA) 3D VỚI NẮP ĐAN HỐ GA
            foreach (var mh in geom.Manholes)
            {
                double xMh = mh.DistanceFromP1M;
                double wMh = Math.Max(mh.WidthM, 1.2);
                double xMh0 = xMh - (wMh / 2.0);
                double xMh1 = xMh + (wMh / 2.0);
                double yMh = -Drop(xMh);
                double mhW = wOut + 0.50;           // Hộp nối rộng hơn thân cống
                double mhTopY = yMh + bH + tSlab + 0.55; // Nhô cao hơn đỉnh cống 0.55m
                double mhBotY = yMh - tSlab - 0.25;      // Đáy hố ga hạ sâu hơn đáy cống

                // Thân hố ga (4 vách khối hộp ngoài)
                AddSolidBoxMesh(_rootModelGroup, xMh0, mhBotY, -mhW / 2.0, xMh1, mhTopY, mhW / 2.0, MatManhole);

                // Nắp đan hố ga phía trên cùng (vát góc, màu gang đậm)
                AddSolidBoxMesh(_rootModelGroup, xMh0 + 0.08, mhTopY, -mhW / 2.0 + 0.08, xMh1 - 0.08, mhTopY + 0.12, mhW / 2.0 - 0.08, MatManholeCover);

                // Nắp tròn hoặc vuông kiểm tra ở trung tâm nắp hố ga
                double capR = Math.Min(wMh * 0.35, 0.40);
                AddSolidBoxMesh(_rootModelGroup, xMh - capR, mhTopY + 0.12, -capR, xMh + capR, mhTopY + 0.16, capR, MatInnerVoid);
            }

            // 4. VẼ CỬA XẢ THƯỢNG LƯU & HẠ LƯU (TƯỜNG ĐẦU, TƯỜNG CÁNH & SÂN CỐNG)
            // A. Thượng lưu (P1, X = 0)
            BuildInletOutlet3D(_rootModelGroup, 0.0, 0.0, wOut, bH, tSlab, tWall, isUpstream: true);

            // B. Hạ lưu (P2, X = totalL)
            BuildInletOutlet3D(_rootModelGroup, totalL, -Drop(totalL), wOut, bH, tSlab, tWall, isUpstream: false);

            // 5. VẼ SÂN GIA CỐ 3M & DẦM CHÂN KHAY
            // A. Thượng lưu (từ X = -5.0m đến -2.0m)
            AddSolidBoxMesh(_rootModelGroup, -5.0, -tSlab - 0.10, -wOut / 2.0 - 1.2, -2.0, -tSlab + 0.15, wOut / 2.0 + 1.2, MatReinforcedApron);
            // Dầm chân khay thượng lưu (cắm sâu xuống đất 0.6m)
            AddSolidBoxMesh(_rootModelGroup, -5.2, -tSlab - 0.70, -wOut / 2.0 - 1.3, -5.0, -tSlab + 0.15, wOut / 2.0 + 1.3, MatToeBeam);

            // B. Hạ lưu (từ X = totalL + 2.0m đến totalL + 5.0m)
            double yHl = -Drop(totalL);
            AddSolidBoxMesh(_rootModelGroup, totalL + 2.0, yHl - tSlab - 0.10, -wOut / 2.0 - 1.2, totalL + 5.0, yHl - tSlab + 0.15, wOut / 2.0 + 1.2, MatReinforcedApron);
            // Dầm chân khay hạ lưu
            AddSolidBoxMesh(_rootModelGroup, totalL + 5.0, yHl - tSlab - 0.70, -wOut / 2.0 - 1.3, totalL + 5.2, yHl - tSlab + 0.15, wOut / 2.0 + 1.3, MatToeBeam);

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

            // 1. Bản đáy sân cống (Apron Slab)
            AddSolidBoxMesh(group, xApron0, yCenter - tSlab, -wOut / 2.0 - 0.3, xApron1, yCenter, wOut / 2.0 + 0.3, MatApronSlab);

            // 2. Tường đầu (Headwall) vát dốc
            double hwThick = 0.40;
            double xHw0 = isUpstream ? xCenter - hwThick : xCenter;
            double xHw1 = isUpstream ? xCenter : xCenter + hwThick;
            double hwTopY = yCenter + bH + tSlab + 0.50;

            // Khối tường đầu trên đỉnh cống
            AddSolidBoxMesh(group, xHw0, yCenter + bH, -wOut / 2.0 - 0.3, xHw1, hwTopY, wOut / 2.0 + 0.3, MatHeadwall);

            // 3. Hai tường cánh mở góc 35° vát xiên (Flared Wingwalls)
            double wingLen = 2.0;
            double wingFlare = 1.35;
            double wingThick = 0.28;

            // Tường cánh bên trái
            Point3D wTL0 = new Point3D(xCenter, yCenter - tSlab, -wOut / 2.0);
            Point3D wTL1 = new Point3D(xCenter + (dir * wingLen), yCenter - tSlab, -wOut / 2.0 - wingFlare);
            Point3D wTL2 = new Point3D(xCenter + (dir * wingLen), yCenter + 0.35, -wOut / 2.0 - wingFlare);
            Point3D wTL3 = new Point3D(xCenter, hwTopY, -wOut / 2.0);
            AddThickWallMesh(group, wTL0, wTL1, wTL2, wTL3, new Vector3D(0, 0, wingThick), MatHeadwall);

            // Tường cánh bên phải
            Point3D wTR0 = new Point3D(xCenter, yCenter - tSlab, wOut / 2.0);
            Point3D wTR1 = new Point3D(xCenter + (dir * wingLen), yCenter - tSlab, wOut / 2.0 + wingFlare);
            Point3D wTR2 = new Point3D(xCenter + (dir * wingLen), yCenter + 0.35, wOut / 2.0 + wingFlare);
            Point3D wTR3 = new Point3D(xCenter, hwTopY, wOut / 2.0);
            AddThickWallMesh(group, wTR0, wTR1, wTR2, wTR3, new Vector3D(0, 0, -wingThick), MatHeadwall);
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
