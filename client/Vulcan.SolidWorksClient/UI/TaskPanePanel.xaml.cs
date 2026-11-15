using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Vulcan.SolidWorksClient.Models;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.UI
{
    /// <summary>
    /// Compact prompt panel hosted in the SolidWorks task pane. The modal
    /// window remains available as a fallback when the task pane is unavailable.
    /// </summary>
    public partial class TaskPanePanel : UserControl
    {
        private SwModeler _swModeler;
        private bool _isBusy;

        public TaskPanePanel()
        {
            InitializeComponent();
        }

        public void Initialize(SwModeler swModeler)
        {
            _swModeler = swModeler;
            SetStatus("就绪");
        }

        public void SetPrompt(string prompt)
        {
            PromptTextBox.Text = prompt ?? string.Empty;
            PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
            PromptTextBox.Focus();
        }

        private void CylinderExample_Click(object sender, RoutedEventArgs e)
        {
            SetPrompt("在前视基准面拉伸一个直径 50、高度 100 的圆柱体");
            SetStatus("已填入圆柱示例，点击开始生成。");
        }

        private void PlateExample_Click(object sender, RoutedEventArgs e)
        {
            SetPrompt("在前视基准面拉伸 200×100×20 的安装板，四角各打一个直径 10mm 的通孔");
            SetStatus("已填入安装板示例，点击开始生成。");
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            SetStatus("正在检测服务端连接...");
            try
            {
                string body = await new ApiClient().CheckHealthAsync();
                VulcanStatus.ReportHealth(true, "连接正常：" + body);
                SetStatus("服务端连接正常。");
            }
            catch (Exception ex)
            {
                VulcanStatus.ReportHealth(false, "连接失败：" + ex.Message);
                SetStatus("连接失败：" + ex.Message);
            }
        }

        private void Status_Click(object sender, RoutedEventArgs e)
        {
            var window = new StatusWindow { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        }

        private void Logs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
                    "Vulcan_Logs");
                System.IO.Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start("explorer.exe", folder);
            }
            catch (Exception ex)
            {
                SetStatus("无法打开日志目录：" + ex.Message);
            }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = new SettingsWindow { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true)
            {
                SetStatus("设置已保存。");
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
                SetStatus("请先输入建模需求。");
                return;
            }

            SetBusy(true);
            SetStatus("正在请求模型生成参数...");
            try
            {
                var apiClient = new ApiClient();
                var progress = new Progress<string>(SetStatus);
                ModelData modelData = await apiClient.GenerateModelStreamAsync(
                    userPrompt, progress, System.Threading.CancellationToken.None);

                var validationErrors = FeatureTreeValidator.Validate(modelData);
                if (validationErrors.Count > 0)
                {
                    string message = "模型返回参数无效：" + string.Join("；", validationErrors.Take(3));
                    SetStatus(message);
                    VulcanStatus.ReportAction("校验失败", 0, message);
                    return;
                }

                var preview = new PreviewWindow(modelData, userPrompt)
                {
                    Owner = Window.GetWindow(this)
                };
                if (preview.ShowDialog() != true || preview.Result == null)
                {
                    SetStatus("已取消执行。");
                    return;
                }

                SetStatus("正在写入 SolidWorks 特征...");
                _swModeler.ExecuteModeling(preview.Result, userPrompt);
                SetStatus("模型生成完成。");
                VulcanStatus.ReportAction("模型生成完成", CountFeatures(preview.Result));
            }
            catch (Exception ex)
            {
                Logger.Error("任务窗格生成失败", ex);
                SetStatus("生成失败：" + ex.Message);
                VulcanStatus.ReportAction("生成失败", 0, ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            GenerateBtn.IsEnabled = !busy;
            GenerateBtn.Content = busy ? "正在生成..." : "开始生成";
            PromptTextBox.IsEnabled = !busy;
            SettingsBtn.IsEnabled = !busy;
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

        public void SetStatus(string message)
        {
            StatusText.Text = message;
        }
    }
}
