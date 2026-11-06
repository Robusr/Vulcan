using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Vulcan.SolidWorksClient.Services
{
    public class VulcanSettings
    {
        public string ServerUrl { get; set; }
        public string ServerToken { get; set; }
        public string LlmApiKey { get; set; }
        public string LlmBaseUrl { get; set; }
        public string LlmModel { get; set; }
    }

    public static class SettingsStore
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Vulcan");
        private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");
        private static readonly object Sync = new object();

        public static string SettingsPath
        {
            get { return SettingsFile; }
        }

        public static VulcanSettings Load()
        {
            lock (Sync)
            {
                try
                {
                    if (!File.Exists(SettingsFile))
                    {
                        return new VulcanSettings();
                    }

                    var stored = JsonConvert.DeserializeObject<StoredSettings>(
                        File.ReadAllText(SettingsFile, Encoding.UTF8));
                    if (stored == null)
                    {
                        return new VulcanSettings();
                    }

                    return new VulcanSettings
                    {
                        ServerUrl = stored.ServerUrl,
                        ServerToken = Unprotect(stored.ServerToken),
                        LlmApiKey = Unprotect(stored.LlmApiKey),
                        LlmBaseUrl = stored.LlmBaseUrl,
                        LlmModel = stored.LlmModel
                    };
                }
                catch (Exception ex)
                {
                    Logger.Warning("读取设置失败，使用默认设置", ex);
                    return new VulcanSettings();
                }
            }
        }

        public static void Save(VulcanSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            lock (Sync)
            {
                Directory.CreateDirectory(SettingsFolder);
                var stored = new StoredSettings
                {
                    ServerUrl = settings.ServerUrl == null ? null : settings.ServerUrl.Trim(),
                    ServerToken = Protect(settings.ServerToken),
                    LlmApiKey = Protect(settings.LlmApiKey),
                    LlmBaseUrl = settings.LlmBaseUrl == null ? null : settings.LlmBaseUrl.Trim(),
                    LlmModel = settings.LlmModel == null ? null : settings.LlmModel.Trim()
                };

                File.WriteAllText(
                    SettingsFile,
                    JsonConvert.SerializeObject(stored, Formatting.Indented),
                    Encoding.UTF8);
            }
        }

        private static string Protect(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            byte[] encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(value),
                null,
                DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        private static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            try
            {
                byte[] decrypted = ProtectedData.Unprotect(
                    Convert.FromBase64String(value),
                    null,
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return string.Empty;
            }
        }

        private sealed class StoredSettings
        {
            public string ServerUrl { get; set; }
            public string ServerToken { get; set; }
            public string LlmApiKey { get; set; }
            public string LlmBaseUrl { get; set; }
            public string LlmModel { get; set; }
        }
    }
}
