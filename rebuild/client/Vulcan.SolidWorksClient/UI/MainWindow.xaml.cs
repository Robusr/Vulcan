using System;
using System.Windows;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.UI
{
    public partial class MainWindow : Window
    {
        private readonly SwModeler _modeler;
        private readonly VulcanApiClient _apiClient;

        public MainWindow(SwModeler modeler)
        {
            InitializeComponent();
            _modeler = modeler;
            _apiClient = new VulcanApiClient();
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            string userPrompt = TxtPrompt.Text.Trim();
            if (string.IsNullOrEmpty(userPrompt))
            {
                MessageBox.Show("请输入建模需求", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnGenerate.IsEnabled = false;
            BtnGenerate.Content = "正在连接云端...";

            try
            {
                // 1. 从云端获取建模参数
                var modelParams = await _apiClient.GenerateModelParamsAsync(userPrompt);

                // 2. 执行SolidWorks建模
                BtnGenerate.Content = "正在生成模型...";
                _modeler.ExecuteModeling(modelParams);

                MessageBox.Show("模型生成完成！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成失败：{ex.Message}\n详情请查看桌面日志", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnGenerate.IsEnabled = true;
                BtnGenerate.Content = "一键生成模型";
            }
        }
    }
}