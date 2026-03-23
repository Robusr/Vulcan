using System;
using System.IO;

namespace Vulcan.SolidWorksClient.Services
{
    public static class Logger
    {
        private static readonly string LogFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Vulcan_Logs");

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

        public static void Error(string message, Exception ex = null)
        {
            string fullMessage = ex == null ? message : $"{message}，异常详情：{ex}";
            WriteLog("ERROR", fullMessage);
        }

        private static void WriteLog(string level, string message)
        {
            try
            {
                string logFilePath = Path.Combine(LogFolder, $"Vulcan_Log_{DateTime.Now:yyyyMMdd}.txt");
                string logContent = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}\n";
                File.AppendAllText(logFilePath, logContent);
            }
            catch { }
        }
    }
}