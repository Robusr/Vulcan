using System;
using System.IO;

namespace Vulcan.SolidWorksClient.Services
{
    public static class Logger
    {
        private static readonly string LogFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Vulcan_Logs");
        private static readonly string LogFile = Path.Combine(LogFolder, $"log_{DateTime.Now:yyyyMMdd}.txt");
        private static readonly object _lock = new object();

        static Logger()
        {
            if (!Directory.Exists(LogFolder))
            {
                Directory.CreateDirectory(LogFolder);
            }
        }

        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        // 补全缺失的Warning方法
        public static void Warning(string message, Exception ex = null)
        {
            WriteLog("WARN", message + (ex != null ? $"\n{ex}" : ""));
        }

        public static void Error(string message, Exception ex)
        {
            WriteLog("ERROR", $"{message}\n{ex}");
        }

        private static void WriteLog(string level, string message)
        {
            lock (_lock)
            {
                try
                {
                    string logLine = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}\r\n";
                    File.AppendAllText(LogFile, logLine);
                }
                catch { }
            }
        }
    }
}