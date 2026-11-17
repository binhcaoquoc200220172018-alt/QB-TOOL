using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace InfraBIM.ManholeTool
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Tab chung của hệ sinh thái ĐV_TOOL_HTKT
                string tabName = "ĐV_TOOL_HTKT";

                try
                {
                    application.CreateRibbonTab(tabName);
                }
                catch
                {
                    // Tab đã tồn tại từ tool khác (Cống Ngang, Cống Kỹ Thuật...)
                }

                // Tạo Panel "TNN - Hố Ga"
                RibbonPanel panel = application.CreateRibbonPanel(tabName, "TNN - Hố Ga");

                // Thêm PushButton "Rải Hố Ga"
                string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
                var buttonData = new PushButtonData(
                    "CmdAutoManhole",
                    "Rải Hố\nGa",
                    thisAssemblyPath,
                    "InfraBIM.ManholeTool.Commands.CmdAutoManhole")
                {
                    ToolTip = "ĐV_TOOL_HTKT_TNN_RẢI HỐ GA\nTự động rải hố ga trái, hố ga phải theo bảng Excel 7 cột 2 Sheet (HO GA TRAI, HO GA PHAI)."
                };

                // Nạp Icon cho Ribbon Button
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
