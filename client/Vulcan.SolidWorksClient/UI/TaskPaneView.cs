using System;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.UI
{
    /// <summary>
    /// WinForms host that lets the SolidWorks task pane display the WPF panel.
    /// </summary>
    public class TaskPaneView : UserControl
    {
        private readonly ElementHost _host;
        private readonly TaskPanePanel _panel;
        private readonly Timer _resizeTimer;

        public TaskPaneView(SwModeler modeler)
        {
            _panel = new TaskPanePanel();
            _panel.Initialize(modeler);

            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = _panel
            };

            Controls.Add(_host);
            Dock = DockStyle.Fill;
            BackColor = System.Drawing.Color.FromArgb(243, 246, 251);

            _resizeTimer = new Timer { Interval = 400 };
            _resizeTimer.Tick += (sender, args) =>
            {
                if (Parent != null)
                {
                    Bounds = Parent.ClientRectangle;
                }
            };
            _resizeTimer.Start();
        }

        public System.Windows.Threading.Dispatcher PanelDispatcher
        {
            get { return _panel.Dispatcher; }
        }

        public void SetPrompt(string prompt)
        {
            RunOnPanelThread(() => _panel.SetPrompt(prompt));
        }

        public void SetStatus(string message)
        {
            RunOnPanelThread(() => _panel.SetStatus(message));
        }

        private void RunOnPanelThread(Action action)
        {
            if (_panel.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _panel.Dispatcher.BeginInvoke(action);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _resizeTimer != null)
            {
                _resizeTimer.Stop();
                _resizeTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
