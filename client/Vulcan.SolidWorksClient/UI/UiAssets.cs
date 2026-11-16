using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Vulcan.SolidWorksClient.UI
{
    internal static class UiAssets
    {
        public static void ApplyIcon(Window window)
        {
            if (window == null) { return; }

            try
            {
                string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(assemblyDirectory)) { return; }

                string iconPath = Path.Combine(assemblyDirectory, "icons", "Vulcan.ico");
                if (File.Exists(iconPath))
                {
                    window.Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
                }
            }
            catch
            {
                // A missing or unreadable icon must never block the dialog.
            }
        }
    }
}
