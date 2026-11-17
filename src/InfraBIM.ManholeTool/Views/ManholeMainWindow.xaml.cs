using System.Windows;
using InfraBIM.ManholeTool.ViewModels;

namespace InfraBIM.ManholeTool.Views
{
    /// <summary>
    /// Interaction logic for ManholeMainWindow.xaml
    /// </summary>
    public partial class ManholeMainWindow : Window
    {
        public ManholeMainWindow(ManholeMainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
