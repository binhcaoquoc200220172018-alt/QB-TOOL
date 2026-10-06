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

        private bool Is3DMode => ViewModel?.PreviewMode == PreviewViewMode.Isometric3D;

        private void ZoomInInternal()
        {
            if (Is3DMode) Viewport3DControl.ZoomIn();
            else Canvas2D.ZoomIn();
        }

        private void ZoomOutInternal()
        {
            if (Is3DMode) Viewport3DControl.ZoomOut();
            else Canvas2D.ZoomOut();
        }

        private void FitViewInternal()
        {
            if (Is3DMode) Viewport3DControl.FitView();
            else Canvas2D.FitView();
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ZoomInInternal();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ZoomOutInternal();
        }

        private void FitView_Click(object sender, RoutedEventArgs e)
        {
            FitViewInternal();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
