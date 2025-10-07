using System;
using System.Windows;
using System.Windows.Forms;
using System.IO;
using System.Drawing;
using System.Reflection;

namespace SolusManifestApp.Services
{
    public class TrayIconService : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private readonly Window _mainWindow;
        private readonly SettingsService _settingsService;

        public TrayIconService(Window mainWindow, SettingsService settingsService)
        {
            _mainWindow = mainWindow;
            _settingsService = settingsService;
        }

        public void Initialize()
        {
            _notifyIcon = new NotifyIcon
            {
                Text = "Solus Manifest App",
                Visible = false
            };

            // Load icon from embedded resources first, then try file path
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "SolusManifestApp.icon.ico";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        _notifyIcon.Icon = new Icon(stream);
                    }
                    else
                    {
                        // Try loading from file path as fallback
                        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                        if (File.Exists(iconPath))
                        {
                            _notifyIcon.Icon = new Icon(iconPath);
                        }
                        else
                        {
                            _notifyIcon.Icon = SystemIcons.Application;
                        }
                    }
                }
            }
            catch
            {
                // Use default icon if loading fails
                _notifyIcon.Icon = SystemIcons.Application;
            }

            // Create context menu
            var contextMenu = new ContextMenuStrip();

            var showItem = new ToolStripMenuItem("Show");
            showItem.Click += (s, e) => ShowWindow();

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitApplication();

            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();
        }

        public void ShowInTray()
        {
            if (_notifyIcon != null)
            {
                // Ensure icon is set before showing
                if (_notifyIcon.Icon == null)
                {
                    _notifyIcon.Icon = SystemIcons.Application;
                }

                _mainWindow.Hide();
                _notifyIcon.Visible = true;
            }
        }

        public void HideFromTray()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
            }
        }

        private void ShowWindow()
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            HideFromTray();
        }

        private void ExitApplication()
        {
            _notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        public void Dispose()
        {
            _notifyIcon?.Dispose();
        }
    }
}
