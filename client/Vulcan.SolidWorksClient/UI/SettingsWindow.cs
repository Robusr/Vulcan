using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.UI
{
    public class SettingsWindow : Window
    {
        private readonly TextBox _serverUrl = new TextBox();
        private readonly PasswordBox _serverToken = new PasswordBox();
        private readonly PasswordBox _llmApiKey = new PasswordBox();
        private readonly TextBox _llmBaseUrl = new TextBox();
        private readonly TextBox _llmModel = new TextBox();
        private readonly TextBlock _status = new TextBlock();

        private static readonly Brush PageBrush = BrushFrom("#F1F5F9");
        private static readonly Brush CardBrush = BrushFrom("#FFFFFF");
        private static readonly Brush LineBrush = BrushFrom("#CBD5E1");
        private static readonly Brush LabelBrush = BrushFrom("#334155");
        private static readonly Brush MutedBrush = BrushFrom("#64748B");
        private static readonly Brush AccentBrush = BrushFrom("#F97316");

        public SettingsWindow()
        {
            Title = "Vulcan 设置";
            Width = 580;
            Height = 500;
            Background = PageBrush;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new Grid { Margin = new Thickness(18) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;
            AddRow(root, row++, "服务端地址", _serverUrl, "例如 http://127.0.0.1:5000");
            AddRow(root, row++, "服务端 Token", _serverToken, "对应 VULCAN_API_TOKEN，可留空");
            AddRow(root, row++, "LLM API Key", _llmApiKey, "留空则使用服务端配置");
            AddRow(root, row++, "LLM Base URL", _llmBaseUrl, "例如 https://api.openai.com/v1");
            AddRow(root, row++, "模型名称", _llmModel, "例如 gpt-4o-mini");

            var note = new TextBlock
            {
                Text = "LLM API Key 会随生成请求发送到你的 Vulcan 服务端，仅在请求期间使用，不写入日志。建议通过 HTTPS 连接。",
                TextWrapping = TextWrapping.Wrap,
                Foreground = MutedBrush,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 10)
            };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            Grid.SetRow(note, row++);
            Grid.SetColumnSpan(note, 2);
            root.Children.Add(note);

            _status.TextWrapping = TextWrapping.Wrap;
            _status.Foreground = MutedBrush;
            _status.FontSize = 12;
            _status.Margin = new Thickness(0, 0, 0, 10);
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            Grid.SetRow(_status, row++);
            Grid.SetColumnSpan(_status, 2);
            root.Children.Add(_status);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var testButton = CreateButton("测试连接", false);
            testButton.Click += TestConnection_Click;
            var logsButton = CreateButton("打开日志目录", false);
            logsButton.Click += OpenLogs_Click;
            var cancelButton = CreateButton("取消", false);
            cancelButton.Click += (s, e) => { DialogResult = false; };
            var saveButton = CreateButton("保存", true);
            saveButton.IsDefault = true;
            saveButton.Click += Save_Click;

            buttons.Children.Add(testButton);
            buttons.Children.Add(logsButton);
            buttons.Children.Add(cancelButton);
            buttons.Children.Add(saveButton);

            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            Grid.SetRow(buttons, row);
            Grid.SetColumnSpan(buttons, 2);
            root.Children.Add(buttons);

            Content = root;
            LoadSettings();
        }

        private static Brush BrushFrom(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex);
        }

        private static Button CreateButton(string text, bool primary)
        {
            var button = new Button
            {
                Content = text,
                Height = 32,
                MinWidth = 96,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(14, 4, 14, 4),
                FontSize = 12,
                Cursor = Cursors.Hand,
                BorderThickness = primary ? new Thickness(0) : new Thickness(1)
            };

            if (primary)
            {
                button.Background = AccentBrush;
                button.Foreground = Brushes.White;
                button.FontWeight = FontWeights.Bold;
            }
            else
            {
                button.Background = CardBrush;
                button.Foreground = LabelBrush;
                button.BorderBrush = LineBrush;
            }

            return button;
        }

        private static void AddRow(Grid root, int row, string label, Control input, string tooltip)
        {
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            var text = new TextBlock
            {
                Text = label,
                Foreground = LabelBrush,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 12)
            };
            Grid.SetRow(text, row);
            Grid.SetColumn(text, 0);
            root.Children.Add(text);

            input.Height = 30;
            input.FontSize = 12;
            input.Padding = new Thickness(6, 4, 6, 4);
            input.Background = CardBrush;
            input.BorderBrush = LineBrush;
            input.Margin = new Thickness(0, 0, 0, 12);
            input.ToolTip = tooltip;
            Grid.SetRow(input, row);
            Grid.SetColumn(input, 1);
            root.Children.Add(input);
        }

        private void LoadSettings()
        {
            var settings = SettingsStore.Load();
            _serverUrl.Text = settings.ServerUrl ?? string.Empty;
            _serverToken.Password = settings.ServerToken ?? string.Empty;
            _llmApiKey.Password = settings.LlmApiKey ?? string.Empty;
            _llmBaseUrl.Text = settings.LlmBaseUrl ?? string.Empty;
            _llmModel.Text = settings.LlmModel ?? string.Empty;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string serverUrl = _serverUrl.Text.Trim();
            if (!string.IsNullOrEmpty(serverUrl) &&
                !Uri.TryCreate(serverUrl, UriKind.Absolute, out _))
            {
                MessageBox.Show("服务端地址不是有效 URL。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SettingsStore.Save(new VulcanSettings
                {
                    ServerUrl = serverUrl,
                    ServerToken = _serverToken.Password,
                    LlmApiKey = _llmApiKey.Password,
                    LlmBaseUrl = _llmBaseUrl.Text.Trim(),
                    LlmModel = _llmModel.Text.Trim()
                });
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存设置失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            _status.Text = "正在测试连接...";
            try
            {
                var client = new ApiClient(_serverUrl.Text.Trim(), _serverToken.Password);
                string body = await client.CheckHealthAsync();
                _status.Text = "连接成功：" + body;
            }
            catch (Exception ex)
            {
                _status.Text = "连接失败：" + ex.Message;
            }
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
                MessageBox.Show("无法打开日志目录：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
