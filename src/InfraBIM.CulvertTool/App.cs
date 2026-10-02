using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using InfraBIM.CulvertTool.Services;

namespace InfraBIM.CulvertTool
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Tên Tab trên Ribbon của Revit theo yêu cầu
                string tabName = "ĐV_TOOL_HTKT";

                try
                {
                    application.CreateRibbonTab(tabName);
                }
                catch
                {
                    // Tab đã tồn tại
                }

                // Tạo Panel "TNN - Cống Ngang"
                RibbonPanel panel = application.CreateRibbonPanel(tabName, "TNN - Cống Ngang");

                // Thêm PushButton "Rải Cống Ngang"
                string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
                var buttonData = new PushButtonData(
                    "CmdAutoCulvert",
                    "Rải Cống\nNgang",
                    thisAssemblyPath,
                    "InfraBIM.CulvertTool.Commands.CmdAutoCulvert")
                {
                    ToolTip = "ĐV_TOOL_HTKT_TNN_RẢI CỐNG NGANG\nTự động rải cống tròn, cống hộp, hố ga và sân cống theo bảng trắc dọc Excel 19 cột."
                };

                // Nạp Icon cho Ribbon Button (LargeImage: 32x32, Image: 16x16)
                string assemblyDir = Path.GetDirectoryName(thisAssemblyPath) ?? "";
                string icon32 = Path.Combine(assemblyDir, "Resources", "culvert_32.png");
                string icon16 = Path.Combine(assemblyDir, "Resources", "culvert_16.png");

                if (File.Exists(icon32))
                {
                    buttonData.LargeImage = new BitmapImage(new Uri(icon32, UriKind.Absolute));
                }
                if (File.Exists(icon16))
                {
                    buttonData.Image = new BitmapImage(new Uri(icon16, UriKind.Absolute));
                }

                panel.AddItem(buttonData);

                // Tự động chẩn đoán khi mở file Revit
                application.ControlledApplication.DocumentOpened += (s, e) =>
                {
                    try
                    {
                        if (e.Document != null && !e.Document.IsFamilyDocument)
                        {
                            ModelDiagnosticService.RunDeepDiagnostic(e.Document.Application, e.Document);
                        }
                    }
                    catch { }
                };

                return Result.Succeeded;
            }
            catch (Exception)
            {
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
