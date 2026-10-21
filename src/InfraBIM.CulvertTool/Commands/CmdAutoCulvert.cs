using System;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using InfraBIM.CulvertTool.Services;
using InfraBIM.CulvertTool.ViewModels;
using InfraBIM.CulvertTool.Views;

namespace InfraBIM.CulvertTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdAutoCulvert : IExternalCommand
    {
        private static CulvertMainWindow? _currentWindow;
        private static readonly string LogPath = @"C:\Users\ADMIN\Desktop\CULVERT_STARTUP_LOG.txt";

        private static void Log(string msg)
        {
            StartupLogger.Log("[CMD] " + msg);
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Log("=== CMD_AUTO_CULVERT START ===");
                var uiApp = commandData.Application;
                if (uiApp?.ActiveUIDocument?.Document == null)
                {
                    Log("Document is null!");
                    TaskDialog.Show("Thông báo", "Vui lòng mở một file dự án Revit trước khi khởi chạy công cụ cống.");
                    return Result.Cancelled;
                }
                Log($"Document title: {uiApp.ActiveUIDocument.Document.Title}");

                // Nếu cửa sổ đang mở thì focus vào
                if (_currentWindow != null && _currentWindow.IsLoaded)
                {
                    Log("Window already open, activating");
                    _currentWindow.Activate();
                    return Result.Succeeded;
                }

                Log("Creating RevitExternalEventHandler...");
                var eventHandler = new RevitExternalEventHandler();
                Log("Creating ExternalEvent...");
                var externalEvent = ExternalEvent.Create(eventHandler);
                Log("Creating MainViewModel...");
                var viewModel = new MainViewModel(uiApp, externalEvent, eventHandler);
                Log("MainViewModel created successfully!");

                Log("Creating CulvertMainWindow...");
                _currentWindow = new CulvertMainWindow(viewModel);
                _currentWindow.Closed += (s, e) => { Log("Window closed."); _currentWindow = null; };
                Log("Showing CulvertMainWindow...");
                _currentWindow.Show();
                Log("CulvertMainWindow shown successfully!");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log($"CRITICAL ERROR in Execute: {ex}");
                TaskDialog.Show("Lỗi khởi chạy", $"Không thể mở công cụ cống:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
