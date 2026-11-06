using System;
using System.Windows;
using System.Windows.Controls;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.UI
{
    public partial class MainWindow : Window
    {
        private readonly SwModeler _swModeler;
        private readonly string _defaultPromptText = "例如：在前视基准面拉伸一个直径50，高度100圆柱体";

        public MainWindow(SwModeler swModeler)
        {
            InitializeComponent();
            _swModeler = swModeler ?? throw new ArgumentNullException(nameof(swModeler));
            AddSettingsButton();
        }

        private void AddSettingsButton()
        {
            var root = Content as Grid;
            if (root == null)
            {
                return;
            }

            var settingsButton = new Button
            {
                Content = "设置",
                MinWidth = 64,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0),
                ToolTip = "配置服务端、Token 和 LLM API Key",
                Style = TryFindResource("GhostButton") as Style
            };
            settingsButton.Click += (s, e) =>
            {
                var window = new SettingsWindow { Owner = this };
                if (window.ShowDialog() == true)
                {
                    StatusText.Text = "设置已保存";
                }
            };
            Grid.SetRow(settingsButton, 0);
            root.Children.Add(settingsButton);
        }

        #region 占位符焦点事件
        private void PromptTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (PromptTextBox.Text.Trim() == _defaultPromptText)
            {
                PromptTextBox.Text = string.Empty;
            }
        }

        private void PromptTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PromptTextBox.Text))
            {
                PromptTextBox.Text = _defaultPromptText;
            }
        }
        #endregion

        #region 生成按钮点击事件
        private async void GenerateBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string userPrompt = PromptTextBox.Text.Trim();
                if (string.IsNullOrEmpty(userPrompt) || userPrompt == _defaultPromptText)
                {
                    MessageBox.Show("请输入建模需求", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                GenerateBtn.IsEnabled = false;
                GenerateBtn.Content = "正在生成模型...";
                StatusText.Text = "正在请求云端生成参数...";

                var apiClient = new ApiClient();
                var modelData = await apiClient.GenerateModelAsync(userPrompt);

                var preview = new PreviewWindow(modelData, userPrompt) { Owner = this };
                if (preview.ShowDialog() != true || preview.Result == null)
                {
                    StatusText.Text = "已取消执行";
                    return;
                }

                StatusText.Text = "正在执行建模...";
                _swModeler.ExecuteModeling(preview.Result, userPrompt);

                StatusText.Text = "模型生成完成！";
                MessageBox.Show("模型生成完成！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText.Text = "生成失败";
                MessageBox.Show($"生成失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                GenerateBtn.IsEnabled = true;
                GenerateBtn.Content = "开始生成";
            }
        }
        #endregion
    }
}
