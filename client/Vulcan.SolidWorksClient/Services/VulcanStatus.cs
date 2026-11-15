using System;

namespace Vulcan.SolidWorksClient.Services
{
    /// <summary>
    /// Shared run-state snapshot used by the ribbon commands and the status window.
    /// </summary>
    public static class VulcanStatus
    {
        private static readonly object Sync = new object();
        private static string _lastAction = "就绪";
        private static int _lastFeatureCount;
        private static string _lastError = string.Empty;
        private static string _lastHealth = "未检测";
        private static bool? _lastHealthOk;
        private static DateTime _lastUpdated = DateTime.Now;

        public static string LastAction
        {
            get { lock (Sync) { return _lastAction; } }
        }

        public static int LastFeatureCount
        {
            get { lock (Sync) { return _lastFeatureCount; } }
        }

        public static string LastError
        {
            get { lock (Sync) { return _lastError; } }
        }

        public static string LastHealth
        {
            get { lock (Sync) { return _lastHealth; } }
        }

        public static bool? LastHealthOk
        {
            get { lock (Sync) { return _lastHealthOk; } }
        }

        public static DateTime LastUpdated
        {
            get { lock (Sync) { return _lastUpdated; } }
        }

        public static void ReportAction(string action, int featureCount = 0, string error = null)
        {
            lock (Sync)
            {
                _lastAction = action ?? string.Empty;
                _lastFeatureCount = featureCount;
                _lastError = error ?? string.Empty;
                _lastUpdated = DateTime.Now;
            }
        }

        public static void ReportHealth(bool ok, string message)
        {
            lock (Sync)
            {
                _lastHealthOk = ok;
                _lastHealth = message ?? string.Empty;
                _lastUpdated = DateTime.Now;
            }
        }
    }
}
