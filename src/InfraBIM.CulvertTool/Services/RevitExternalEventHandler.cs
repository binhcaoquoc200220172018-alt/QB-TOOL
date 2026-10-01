using System;
using Autodesk.Revit.UI;

namespace InfraBIM.CulvertTool.Services
{
    /// <summary>
    /// Cho phép WPF Modeless Window gọi các thao tác ghi vào Revit API một cách an toàn
    /// </summary>
    public class RevitExternalEventHandler : IExternalEventHandler
    {
        private Action<UIApplication>? _action;

        public void SetAction(Action<UIApplication> action)
        {
            _action = action;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                _action?.Invoke(app);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi Revit API", ex.ToString());
            }
            finally
            {
                _action = null;
            }
        }

        public string GetName()
        {
            return "InfraBIM.CulvertTool.RevitExternalEventHandler";
        }
    }
}
