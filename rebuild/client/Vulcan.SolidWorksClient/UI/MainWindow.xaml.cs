using System;
using System.Windows;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.UI
{
    public partial class MainWindow : Window
    {
        private readonly SwModeler _swModeler;
        private readonly ApiClient _apiClient;
        private readonly string _defaultPromptText = "例如：在前视基准面拉伸一个直径50，高度100圆柱体";

        public MainWindow(SwModeler swModeler)
        {
            InitializeComponent();
            _swModeler = swModeler ?? throw new ArgumentNullException(nameof(swModeler));
            _apiClient = new ApiClient();
        }

        #region 占位符焦点事件
        /// <summary>
        /// 输入框获得焦点，清空默认提示文本
        /// </summary>
        private void PromptTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (PromptTextBox.Text.Trim() == _defaultPromptText)
            {
                PromptTextBox.Text = string.Empty;
            }
        }

        /// <summary>
        /// 输入框失去焦点，无内容时恢复默认提示文本
        /// </summary>
        private void PromptTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PromptTextBox.Text))
            {
                PromptTextBox.Text = _defaultPromptText;
            }
        }
        #endregion

        #region 生成按钮点击事件
        /// <summary>
        /// 生成按钮点击事件
        /// </summary>
        private async void GenerateBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. 获取用户输入，过滤默认提示文本
                string userPrompt = PromptTextBox.Text.Trim();
                if (string.IsNullOrEmpty(userPrompt) || userPrompt == _defaultPromptText)
                {
                    MessageBox.Show("请输入建模需求", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 2. 禁用按钮，防止重复点击
                GenerateBtn.IsEnabled = false;
                GenerateBtn.Content = "正在生成模型...";
                StatusText.Text = "正在请求云端生成参数...";

                // 3. 调用后端接口
                var modelData = await _apiClient.GenerateModelAsync(userPrompt);

                // 4. 执行建模
                StatusText.Text = "正在执行建模...";
                _swModeler.ExecuteModeling(modelData);

                // 5. 完成
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
                // 恢复按钮状态
                GenerateBtn.IsEnabled = true;
                GenerateBtn.Content = "开始生成";
            }
        }
        #endregion
    }
}