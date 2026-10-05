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
                // Tab Preview 2D/3D đã được loại bỏ theo yêu cầu
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
