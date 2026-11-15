using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.UI
{
    public partial class SettingsWindow : Window
    {
        private bool _syncing;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = SettingsStore.Load();
            ServerUrlTextBox.Text = settings.ServerUrl ?? string.Empty;
            ServerTokenPasswordBox.Password = settings.ServerToken ?? string.Empty;
            ServerTokenTextBox.Text = ServerTokenPasswordBox.Password;
            LlmApiKeyPasswordBox.Password = settings.LlmApiKey ?? string.Empty;
            LlmApiKeyTextBox.Text = LlmApiKeyPasswordBox.Password;
            LlmBaseUrlTextBox.Text = settings.LlmBaseUrl ?? string.Empty;
            LlmModelTextBox.Text = settings.LlmModel ?? string.Empty;
            LoadProviders();
            string provider = settings.LlmProvider ?? "openai";
            if (LlmProviderComboBox.Items.Contains(provider))
            {
                LlmProviderComboBox.SelectedItem = provider;
            }
        }

        private void ServerTokenPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_syncing)
            {
                return;
            }
            _syncing = true;
            ServerTokenTextBox.Text = ServerTokenPasswordBox.Password;
            _syncing = false;
        }

        private void ServerTokenTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing)
            {
                return;
            }
            _syncing = true;
            ServerTokenPasswordBox.Password = ServerTokenTextBox.Text;
            _syncing = false;
        }

        private void LlmApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_syncing)
            {
                return;
            }
            _syncing = true;
            LlmApiKeyTextBox.Text = LlmApiKeyPasswordBox.Password;
            _syncing = false;
        }

        private void LlmApiKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing)
            {
                return;
            }
            _syncing = true;
            LlmApiKeyPasswordBox.Password = LlmApiKeyTextBox.Text;
            _syncing = false;
        }

        private void ShowServerTokenBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleSecret(ServerTokenPasswordBox, ServerTokenTextBox, ShowServerTokenBtn);
        }

        private void ShowLlmKeyBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleSecret(LlmApiKeyPasswordBox, LlmApiKeyTextBox, ShowLlmKeyBtn);
        }

        private static void ToggleSecret(PasswordBox passwordBox, TextBox textBox, Button button)
        {
            bool passwordVisible = passwordBox.Visibility == Visibility.Visible;
            if (passwordVisible)
            {
                passwordBox.Visibility = Visibility.Collapsed;
                textBox.Visibility = Visibility.Visible;
                button.Content = "隐藏";
                textBox.Focus();
            }
            else
            {
                textBox.Visibility = Visibility.Collapsed;
                passwordBox.Visibility = Visibility.Visible;
                button.Content = "显示";
                passwordBox.Focus();
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            string serverUrl = ServerUrlTextBox.Text.Trim();
            string error;
            if (!ValidateServerUrl(serverUrl, out error))
            {
                SetSaveStatus(error, true);
                return;
            }

            string llmBaseUrl = LlmBaseUrlTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(llmBaseUrl) &&
                !Uri.TryCreate(llmBaseUrl, UriKind.Absolute, out _))
            {
                SetSaveStatus("LLM Base URL 不是有效 URL。", true);
                return;
            }

            try
            {
                SettingsStore.Save(new VulcanSettings
                {
                    ServerUrl = serverUrl,
                    ServerToken = ServerTokenPasswordBox.Password,
                    LlmApiKey = LlmApiKeyPasswordBox.Password,
                    LlmBaseUrl = llmBaseUrl,
                    LlmModel = LlmModelTextBox.Text.Trim(),
                    LlmProvider = LlmProviderComboBox.SelectedItem as string
                });
                DialogResult = true;
            }
            catch (Exception ex)
            {
                SetSaveStatus("保存设置失败：" + ex.Message, true);
            }
        }

        private async void TestConnectionBtn_Click(object sender, RoutedEventArgs e)
        {
            string serverUrl = ServerUrlTextBox.Text.Trim();
            string error;
            if (!ValidateServerUrl(serverUrl, out error))
            {
                SetTestStatus(error, (Brush)FindResource("ErrorBrush"));
                return;
            }

            TestConnectionBtn.IsEnabled = false;
            SetTestStatus("正在测试连接...", (Brush)FindResource("BusyBrush"));
            try
            {
                var client = new ApiClient(serverUrl, ServerTokenPasswordBox.Password);
                string body = await client.CheckHealthAsync();
                SetTestStatus("连接成功", (Brush)FindResource("SuccessBrush"));
            }
            catch (Exception ex)
            {
                SetTestStatus("连接失败：" + ex.Message, (Brush)FindResource("ErrorBrush"));
            }
            finally
            {
                TestConnectionBtn.IsEnabled = true;
            }
        }

        private void OpenLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "Vulcan_Logs");
                Directory.CreateDirectory(folder);
                Process.Start("explorer.exe", folder);
            }
            catch (Exception ex)
            {
                SetSaveStatus("无法打开日志目录：" + ex.Message, true);
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void LoadProviders()
        {
            LlmProviderComboBox.Items.Clear();
            LlmProviderComboBox.Items.Add("openai");
            LlmProviderComboBox.Items.Add("anthropic");
        }

        private static bool ValidateServerUrl(string serverUrl, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                return true;
            }

            Uri uri;
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out uri))
            {
                error = "服务端地址不是有效 URL。";
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                error = "服务端地址必须使用 http 或 https。";
                return false;
            }

            return true;
        }

        private void SetSaveStatus(string message, bool error)
        {
            SaveStatusText.Text = message;
            SaveStatusText.Foreground = (Brush)FindResource(error ? "ErrorBrush" : "TextMutedBrush");
        }

        private void SetTestStatus(string message, Brush brush)
        {
            TestStatusText.Text = message;
            TestStatusText.Foreground = brush;
        }
    }
}
