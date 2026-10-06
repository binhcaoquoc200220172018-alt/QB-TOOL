using System.Windows;
using InfraBIM.CulvertTool.Models;
using InfraBIM.CulvertTool.ViewModels;

namespace InfraBIM.CulvertTool.Views
{
    /// <summary>
    /// Cửa sổ kiểm tra mô hình 3D phối cảnh, mặt cắt dọc và mặt bằng cống ngang
    /// Giúp kỹ sư kiểm tra cao độ Z1, Z2, dốc i%, hình dạng các đốt và vị trí hố ga trước khi chạy mô hình Revit
    /// </summary>
    public partial class Culvert3DPreviewWindow : Window
    {
        public MainViewModel? ViewModel { get; }

        public Culvert3DPreviewWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;

            if (ViewModel != null)
            {
                ViewModel.RequestPreviewAction = action =>
                {
                    if (action == "Fit") FitViewInternal();
                    else if (action == "ZoomIn") ZoomInInternal();
                    else if (action == "ZoomOut") ZoomOutInternal();
                };
            }
        }

        private void ZoomInInternal() => Viewport3DControl.ZoomIn();
        private void ZoomOutInternal() => Viewport3DControl.ZoomOut();
        private void FitViewInternal() => Viewport3DControl.FitView();

        private void SetIso_Click(object sender, RoutedEventArgs e) => Viewport3DControl.SetViewIsometric();
        private void SetFront_Click(object sender, RoutedEventArgs e) => Viewport3DControl.SetViewFront();
        private void SetTop_Click(object sender, RoutedEventArgs e) => Viewport3DControl.SetViewTop();
        private void SetRight_Click(object sender, RoutedEventArgs e) => Viewport3DControl.SetViewRight();

        private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomInInternal();
        private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomOutInternal();
        private void FitView_Click(object sender, RoutedEventArgs e) => FitViewInternal();

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
