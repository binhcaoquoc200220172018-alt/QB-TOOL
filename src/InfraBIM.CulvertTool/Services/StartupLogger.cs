using System;
using System.IO;

namespace InfraBIM.CulvertTool.Services
{
    public static class StartupLogger
    {
        private static readonly string LogPath = @"C:\Users\ADMIN\Desktop\CULVERT_STARTUP_LOG.txt";
        private static readonly object _lock = new object();

        public static void Log(string msg)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\r\n");
                }
            }
            catch { }
        }
    }
}
