using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using InfraBIM.CulvertTool.ViewModels;

namespace InfraBIM.CulvertTool.Views
{
    public partial class CulvertMainWindow : Window
    {
        public MainViewModel? ViewModel { get; }
        private static readonly string LogPath = @"C:\Users\ADMIN\Desktop\CULVERT_STARTUP_LOG.txt";

        private static void Log(string msg)
        {
            try { File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [WINDOW] {msg}\n"); } catch { }
        }

        public CulvertMainWindow(MainViewModel viewModel)
        {
            Log("CulvertMainWindow constructor start");
            Log("Calling InitializeComponent()...");
            InitializeComponent();
            Log("InitializeComponent() completed successfully!");

            ViewModel = viewModel;
            Log("Setting DataContext = ViewModel...");
            DataContext = ViewModel;
            Log("DataContext set successfully!");

            if (ViewModel != null)
            {
                ViewModel.RequestClose = () =>
                {
                    Close();
                };

                ViewModel.RequestPreviewAction = action =>
                {
                    // Tab Preview 2D/3D đã được loại bỏ theo yêu cầu
                };
            }
            Log("CulvertMainWindow constructor finished!");
        }

        private void OnFilterClick(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null && ViewModel != null)
            {
                ViewModel.SelectedTypeFilter = rb.Tag.ToString() ?? "TẤT CẢ";
            }
        }

        private void MaterialsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dg && ViewModel != null)
            {
                var selectedList = dg.SelectedItems.OfType<InfraBIM.CulvertTool.Models.CulvertMaterialItem>().ToList();
                ViewModel.UpdateSelectedMaterialItems(selectedList);
            }
        }
    }
}
