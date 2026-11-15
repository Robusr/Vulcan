using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using Vulcan.SolidWorksClient.Models;

namespace Vulcan.SolidWorksClient.UI
{
    public class PreviewWindow : Window
    {
        private readonly TextBox _json;

        private static readonly Brush PageBrush = BrushFrom("#F1F5F9");
        private static readonly Brush CardBrush = BrushFrom("#FFFFFF");
        private static readonly Brush LineBrush = BrushFrom("#CBD5E1");
        private static readonly Brush AccentBrush = BrushFrom("#F97316");
        private static readonly Brush LabelBrush = BrushFrom("#334155");

        public ModelData Result { get; private set; }

        public PreviewWindow(ModelData modelData, string prompt)
        {
            Title = "检查并编辑建模参数";
            Width = 760;
            Height = 600;
            Background = PageBrush;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            var header = new TextBlock
            {
                Text = "确认后才会写入 SolidWorks。可直接编辑下面的 JSON。",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFrom("#0F172A"),
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
                Foreground = BrushFrom("#64748B"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(promptLabel, 1);
            root.Children.Add(promptLabel);

            var card = new Border
            {
                Background = CardBrush,
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 12)
            };
            _json = new TextBox
            {
                Text = JsonConvert.SerializeObject(modelData, Formatting.Indented),
                AcceptsReturn = true,
                AcceptsTab = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = BrushFrom("#0F172A"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap
            };
            card.Child = _json;
            Grid.SetRow(card, 2);
            root.Children.Add(card);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var execute = CreateButton("执行建模", true);
            execute.IsDefault = true;
            execute.Click += Execute_Click;
            var cancel = CreateButton("取消", false);
            cancel.IsCancel = true;
            cancel.Click += (s, e) => { DialogResult = false; };
            buttons.Children.Add(execute);
            buttons.Children.Add(cancel);
            Grid.SetRow(buttons, 3);
            root.Children.Add(buttons);

            Content = root;
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
                Width = primary ? 108 : 84,
                Height = 34,
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 13,
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
