using System.Windows;
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
                    if (action == "Fit") Canvas3D.FitView();
                    else if (action == "ZoomIn") Canvas3D.ZoomIn();
                    else if (action == "ZoomOut") Canvas3D.ZoomOut();
                };
            }
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            Canvas3D.ZoomIn();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            Canvas3D.ZoomOut();
        }

        private void FitView_Click(object sender, RoutedEventArgs e)
        {
            Canvas3D.FitView();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
