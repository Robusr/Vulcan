using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vulcan.SolidWorksClient.Models;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.UI
{
    public partial class PreviewWindow : Window
    {
        private readonly DispatcherTimer _validationTimer;
        private readonly string _originalJson;

        public ModelData Result { get; private set; }

        public PreviewWindow(ModelData modelData, string prompt)
        {
            InitializeComponent();

            _validationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _validationTimer.Tick += ValidationTimer_Tick;

            _originalJson = JsonConvert.SerializeObject(modelData, Formatting.Indented);
            JsonEditor.Text = _originalJson;

            string promptText = prompt ?? string.Empty;
            PromptText.Text = "需求：" + Truncate(promptText, 150);
            PromptText.ToolTip = promptText;

            Loaded += (sender, args) =>
            {
                JsonEditor.Focus();
                ValidateEditor();
            };
        }

        private void JsonEditor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_validationTimer == null)
            {
                return;
            }
            _validationTimer.Stop();
            _validationTimer.Start();
        }

        private void ValidationTimer_Tick(object sender, EventArgs e)
        {
            _validationTimer.Stop();
            ValidateEditor();
        }

        private bool ValidateEditor()
        {
            List<string> errors;
            ParseAndValidate(out errors);

            if (errors.Count == 0)
            {
                ValidationBanner.Visibility = Visibility.Collapsed;
                ValidationHint.Text = "JSON 有效";
                ValidationHint.Foreground = (Brush)FindResource("SuccessBrush");
                ExecuteBtn.IsEnabled = true;
                return true;
            }

            ShowErrors(errors);
            return false;
        }

        private ModelData ParseAndValidate(out List<string> errors)
        {
            errors = new List<string>();

            ModelData parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<ModelData>(JsonEditor.Text);
            }
            catch (Exception ex)
            {
                errors.Add("JSON 解析失败：" + ex.Message);
                return null;
            }

            if (parsed == null)
            {
                errors.Add("JSON 内容为空");
                return null;
            }

            errors.AddRange(FeatureTreeValidator.Validate(parsed));
            return parsed;
        }

        private void ShowErrors(IReadOnlyList<string> errors)
        {
            ValidationBanner.Visibility = Visibility.Visible;
            ValidationBanner.Background = (Brush)FindResource("ErrorBgBrush");
            ValidationBanner.BorderBrush = (Brush)FindResource("ErrorBorderBrush");
            ValidationIcon.Text = "";
            ValidationIcon.Foreground = (Brush)FindResource("ErrorBrush");
            ValidationText.Foreground = (Brush)FindResource("ErrorBrush");

            ValidationText.Text = string.Join("\n", errors.Take(6));
            if (errors.Count > 6)
            {
                ValidationText.Text += "\n...";
            }

            ValidationHint.Text = "有错误";
            ValidationHint.Foreground = (Brush)FindResource("ErrorBrush");
            ExecuteBtn.IsEnabled = false;
        }

        private void FormatBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                JsonEditor.Text = JToken.Parse(JsonEditor.Text).ToString(Formatting.Indented);
                ValidateEditor();
            }
            catch (Exception ex)
            {
                ShowErrors(new List<string> { "JSON 格式化失败：" + ex.Message });
            }
        }

        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            JsonEditor.Text = _originalJson;
            ValidateEditor();
        }

        private void ExecuteBtn_Click(object sender, RoutedEventArgs e)
        {
            List<string> errors;
            ModelData parsed = ParseAndValidate(out errors);
            if (errors.Count > 0)
            {
                ShowErrors(errors);
                return;
            }

            Result = parsed;
            DialogResult = true;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                if (ExecuteBtn.IsEnabled)
                {
                    ExecuteBtn_Click(ExecuteBtn, new RoutedEventArgs());
                }
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
            }
        }

        private static string Truncate(string value, int length)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= length)
            {
                return value ?? string.Empty;
            }
            return value.Substring(0, length) + "...";
        }
    }
}
