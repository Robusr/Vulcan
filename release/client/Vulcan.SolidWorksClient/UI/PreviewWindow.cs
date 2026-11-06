using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using Vulcan.SolidWorksClient.Models;

namespace Vulcan.SolidWorksClient.UI
{
    public class PreviewWindow : Window
    {
        private readonly TextBox _json;

        public ModelData Result { get; private set; }

        public PreviewWindow(ModelData modelData, string prompt)
        {
            Title = "检查并编辑建模参数";
            Width = 720;
            Height = 560;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new Grid { Margin = new Thickness(14) };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            var header = new TextBlock
            {
                Text = "确认后才会写入 SolidWorks。可直接编辑下面的 JSON。",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            string promptText = prompt ?? string.Empty;
            if (promptText.Length > 180)
            {
                promptText = promptText.Substring(0, 180) + "...";
            }
            var promptLabel = new TextBlock
            {
                Text = "需求：" + promptText,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(promptLabel, 1);
            root.Children.Add(promptLabel);

            _json = new TextBox
            {
                Text = JsonConvert.SerializeObject(modelData, Formatting.Indented),
                AcceptsReturn = true,
                AcceptsTab = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(_json, 2);
            root.Children.Add(_json);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var execute = new Button
            {
                Content = "执行建模",
                Width = 100,
                Height = 30,
                IsDefault = true,
                Margin = new Thickness(0, 0, 8, 0)
            };
            execute.Click += Execute_Click;
            var cancel = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30,
                IsCancel = true
            };
            cancel.Click += (s, e) => { DialogResult = false; };
            buttons.Children.Add(execute);
            buttons.Children.Add(cancel);
            Grid.SetRow(buttons, 3);
            root.Children.Add(buttons);

            Content = root;
        }

        private void Execute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<ModelData>(_json.Text);
                bool hasSingle = parsed != null && !string.IsNullOrEmpty(parsed.feature_type);
                bool hasMulti = parsed != null && parsed.features != null && parsed.features.Count > 0;

                if (!hasSingle && !hasMulti)
                {
                    MessageBox.Show("JSON 中没有有效特征。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (hasSingle && parsed.@params == null)
                {
                    MessageBox.Show("单特征缺少 params。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (hasMulti)
                {
                    foreach (var feature in parsed.features)
                    {
                        if (feature.@params == null)
                        {
                            MessageBox.Show("特征缺少 params：" + (feature.feature_type ?? "unknown"), "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                }

                Result = parsed;
                DialogResult = true;
            }
            catch (JsonException ex)
            {
                MessageBox.Show("JSON 解析失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("参数无效：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
