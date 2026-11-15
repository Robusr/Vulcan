using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vulcan.SolidWorksClient.Models;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.UI
{
    public partial class MainWindow : Window
    {
        private readonly SwModeler _swModeler;
        private bool _isBusy;

        public MainWindow(SwModeler swModeler)
        {
            InitializeComponent();
            _swModeler = swModeler ?? throw new ArgumentNullException(nameof(swModeler));
            LoadWindowIcon();
            UpdatePlaceholder();
            UpdateCharCount();
            SetStatus("就绪", StatusKind.Ready);
        }

        private void LoadWindowIcon()
        {
            try
            {
                string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(assemblyDirectory))
                {
                    return;
                }
                string iconPath = Path.Combine(assemblyDirectory, "icons", "Vulcan.ico");
                if (File.Exists(iconPath))
                {
                    Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
                }
            }
            catch
            {
            }
        }

        public void SetPrompt(string prompt)
        {
            PromptTextBox.Text = prompt ?? string.Empty;
            PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
            PromptTextBox.Focus();
        }

        private void PromptTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdatePlaceholder();
            UpdateCharCount();
        }

        private void UpdatePlaceholder()
        {
            PlaceholderText.Visibility = string.IsNullOrEmpty(PromptTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateCharCount()
        {
            int length = PromptTextBox.Text == null ? 0 : PromptTextBox.Text.Length;
            CharCountText.Text = length + " / 4000";
            if (length >= 3900)
            {
                CharCountText.Foreground = (Brush)FindResource("ErrorBrush");
            }
            else if (length >= 3500)
            {
                CharCountText.Foreground = (Brush)FindResource("BusyBrush");
            }
            else
            {
                CharCountText.Foreground = (Brush)FindResource("TextMutedBrush");
            }
        }

        private void ExampleChip_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            string text = button == null ? null : button.Tag as string;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            PromptTextBox.Text = text;
            PromptTextBox.CaretIndex = text.Length;
            PromptTextBox.Focus();
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = new SettingsWindow { Owner = this };
            if (window.ShowDialog() == true)
            {
                SetStatus("设置已保存。", StatusKind.Success);
                VulcanStatus.ReportAction("设置已保存");
            }
        }

        private async void GenerateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy)
            {
                return;
            }

            string userPrompt = (PromptTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(userPrompt))
            {
                SetStatus("请先输入建模需求。", StatusKind.Error);
                PromptTextBox.Focus();
                return;
            }

            SetBusy(true);
            SetStatus("正在请求模型生成参数...", StatusKind.Busy);

            try
            {
                var apiClient = new ApiClient();
                var progress = new Progress<string>(message => StatusText.Text = message);
                ModelData modelData = await apiClient.GenerateModelStreamAsync(
                    userPrompt, progress, System.Threading.CancellationToken.None);
                var validationErrors = FeatureTreeValidator.Validate(modelData);
                if (validationErrors.Count > 0)
                {
                    Logger.Warning(
                        "模型返回参数未通过客户端校验: " + string.Join("; ", validationErrors.Take(3)),
                        null);
                    SetStatus(
                        "模型返回参数无效：" + string.Join("；", validationErrors.Take(3)),
                        StatusKind.Error);
                    return;
                }

                var preview = new PreviewWindow(modelData, userPrompt) { Owner = this };
                if (preview.ShowDialog() != true || preview.Result == null)
                {
                    SetStatus("已取消执行。", StatusKind.Ready);
                    return;
                }

                SetStatus("正在写入 SolidWorks 特征...", StatusKind.Busy);
                _swModeler.ExecuteModeling(preview.Result, userPrompt);
                SetStatus("模型生成完成。", StatusKind.Success);
                VulcanStatus.ReportAction("模型生成完成", CountFeatures(preview.Result));
            }
            catch (Exception ex)
            {
                Logger.Error("生成失败", ex);
                SetStatus("生成失败：" + ex.Message, StatusKind.Error);
                VulcanStatus.ReportAction("生成失败", 0, ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private static int CountFeatures(ModelData data)
        {
            if (data == null)
            {
                return 0;
            }
            if (data.features != null && data.features.Count > 0)
            {
                return data.features.Count;
            }
            return string.IsNullOrEmpty(data.feature_type) ? 0 : 1;
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            GenerateBtn.IsEnabled = !busy;
            GenerateBtn.Content = busy ? "正在生成..." : "开始生成";
            BusyBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            PromptTextBox.IsEnabled = !busy;
            ExamplePanel.IsEnabled = !busy;
            SettingsBtn.IsEnabled = !busy;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                if (!_isBusy)
                {
                    GenerateBtn_Click(GenerateBtn, new RoutedEventArgs());
                }
            }
        }

        private void SetStatus(string message, StatusKind kind)
        {
            string icon;
            string backgroundKey;
            string borderKey;
            string foregroundKey;

            switch (kind)
            {
                case StatusKind.Busy:
                    icon = "";
                    backgroundKey = "BusyBgBrush";
                    borderKey = "BusyBorderBrush";
                    foregroundKey = "BusyBrush";
                    break;
                case StatusKind.Error:
                    icon = "";
                    backgroundKey = "ErrorBgBrush";
                    borderKey = "ErrorBorderBrush";
                    foregroundKey = "ErrorBrush";
                    break;
                case StatusKind.Success:
                case StatusKind.Ready:
                default:
                    icon = "";
                    backgroundKey = "SuccessBgBrush";
                    borderKey = "SuccessBorderBrush";
                    foregroundKey = "SuccessBrush";
                    break;
            }

            StatusIcon.Text = icon;
            StatusText.Text = message;
            StatusBanner.Background = (Brush)FindResource(backgroundKey);
            StatusBanner.BorderBrush = (Brush)FindResource(borderKey);
            StatusIcon.Foreground = (Brush)FindResource(foregroundKey);
            StatusText.Foreground = (Brush)FindResource(foregroundKey);
        }

        private enum StatusKind
        {
            Ready,
            Busy,
            Success,
            Error
        }
    }
}
