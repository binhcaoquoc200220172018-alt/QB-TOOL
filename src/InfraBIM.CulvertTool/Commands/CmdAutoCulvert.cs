using System;
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

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;

                // Tự động chẩn đoán hệ tọa độ và đối tượng cống ra file desktop
                ModelDiagnosticService.RunDiagnostic(uiApp);

                // Nếu cửa sổ đang mở thì focus vào
                if (_currentWindow != null && _currentWindow.IsLoaded)
                {
                    _currentWindow.Activate();
                    return Result.Succeeded;
                }

                var eventHandler = new RevitExternalEventHandler();
                var externalEvent = ExternalEvent.Create(eventHandler);
                var viewModel = new MainViewModel(uiApp, externalEvent, eventHandler);

                _currentWindow = new CulvertMainWindow(viewModel);
                _currentWindow.Closed += (s, e) => _currentWindow = null;
                _currentWindow.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
