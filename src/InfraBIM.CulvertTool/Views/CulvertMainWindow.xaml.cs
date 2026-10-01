using System.Windows;
using System.Windows.Controls;
using InfraBIM.CulvertTool.ViewModels;

namespace InfraBIM.CulvertTool.Views
{
    public partial class CulvertMainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public CulvertMainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;

            ViewModel.RequestClose = () =>
            {
                Close();
            };

            ViewModel.RequestPreviewAction = action =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (action == "Fit") PreviewCanvasControl?.FitView();
                    else if (action == "ZoomIn") PreviewCanvasControl?.ZoomIn();
                    else if (action == "ZoomOut") PreviewCanvasControl?.ZoomOut();
                });
            };
        }

        private void OnFilterClick(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                ViewModel.SelectedTypeFilter = rb.Tag.ToString() ?? "TẤT CẢ";
            }
        }
    }
}
