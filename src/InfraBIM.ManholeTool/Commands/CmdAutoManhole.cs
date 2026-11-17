using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using InfraBIM.ManholeTool.Services;
using InfraBIM.ManholeTool.ViewModels;
using InfraBIM.ManholeTool.Views;

namespace InfraBIM.ManholeTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdAutoManhole : IExternalCommand
    {
        private static ManholeMainWindow? _currentWindow;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                if (uiApp?.ActiveUIDocument?.Document == null)
                {
                    TaskDialog.Show("Thông báo", "Vui lòng mở một dự án Revit trước khi khởi chạy công cụ Rải Hố Ga.");
                    return Result.Cancelled;
                }

                // Nếu cửa sổ đang mở thì đưa lên phía trước
                if (_currentWindow != null && _currentWindow.IsLoaded)
                {
                    _currentWindow.Activate();
                    return Result.Succeeded;
                }

                var eventHandler = new RevitExternalEventHandler();
                var externalEvent = ExternalEvent.Create(eventHandler);
                var viewModel = new ManholeMainViewModel(uiApp, externalEvent, eventHandler);

                _currentWindow = new ManholeMainWindow(viewModel);
                _currentWindow.Closed += (s, e) => { _currentWindow = null; };
                _currentWindow.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi Khởi Chạy", $"Không thể mở công cụ Rải Hố Ga:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
