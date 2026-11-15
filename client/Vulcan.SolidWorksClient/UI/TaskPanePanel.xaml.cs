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

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = new SettingsWindow { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true)
            {
                SetStatus("设置已保存。");
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
                    SetStatus("模型返回参数无效：" + string.Join("；", validationErrors.Take(3)));
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
            }
            catch (Exception ex)
            {
                Logger.Error("任务窗格生成失败", ex);
                SetStatus("生成失败：" + ex.Message);
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

        private void SetStatus(string message)
        {
            StatusText.Text = message;
        }
    }
}
