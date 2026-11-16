using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.UI
{
    public partial class StatusWindow : Window
    {
        public StatusWindow()
        {
            InitializeComponent();
            UiAssets.ApplyIcon(this);
            RefreshSnapshot();
            Loaded += (sender, args) => TestConnection();
        }

        private void RefreshSnapshot()
        {
            var settings = SettingsStore.Load();
            ServerUrlText.Text = string.IsNullOrWhiteSpace(settings.ServerUrl)
                ? "使用默认地址 http://127.0.0.1:5000"
                : settings.ServerUrl;

            string provider = string.IsNullOrWhiteSpace(settings.LlmProvider)
                ? "服务端默认"
                : settings.LlmProvider;
            string model = string.IsNullOrWhiteSpace(settings.LlmModel)
                ? "服务端默认"
                : settings.LlmModel;
            ProviderText.Text = provider + " / " + model;

            ActionText.Text = VulcanStatus.LastAction;
            FeatureCountText.Text = VulcanStatus.LastFeatureCount.ToString();
            UpdatedText.Text = VulcanStatus.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss");

            bool? healthOk = VulcanStatus.LastHealthOk;
            HealthText.Text = VulcanStatus.LastHealth;
            HealthText.Foreground = (Brush)FindResource(
                healthOk == true ? "SuccessBrush" : healthOk == false ? "ErrorBrush" : "TextMutedBrush");

            if (string.IsNullOrWhiteSpace(VulcanStatus.LastError))
            {
                ErrorText.Visibility = Visibility.Collapsed;
            }
            else
            {
                ErrorText.Visibility = Visibility.Visible;
                ErrorText.Text = "最近错误：" + VulcanStatus.LastError;
            }
        }

        private async void TestConnection()
        {
            TestButton.IsEnabled = false;
            HealthText.Text = "正在检测连接...";
            HealthText.Foreground = (Brush)FindResource("BusyBrush");
            try
            {
                var client = new ApiClient();
                string body = await client.CheckHealthAsync();
                VulcanStatus.ReportHealth(true, "连接正常：" + body);
            }
            catch (Exception ex)
            {
                VulcanStatus.ReportHealth(false, "连接失败：" + ex.Message);
            }
            finally
            {
                TestButton.IsEnabled = true;
                RefreshSnapshot();
            }
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            TestConnection();
        }

        private void OpenLogs_Click(object sender, RoutedEventArgs e)
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
                VulcanStatus.ReportAction("打开日志目录失败", 0, ex.Message);
                RefreshSnapshot();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
