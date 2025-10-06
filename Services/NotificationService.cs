using SolusManifestApp.Helpers;
using System;
using System.Windows;

namespace SolusManifestApp.Services
{
    public class NotificationService
    {
        private readonly SettingsService _settingsService;

        public NotificationService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
        {
            var settings = _settingsService.LoadSettings();
            if (!settings.ShowNotifications)
                return;

            // For now, use MessageBox. Can be replaced with Windows Toast Notifications
            Application.Current.Dispatcher.Invoke(() =>
            {
                var icon = type switch
                {
                    NotificationType.Success => MessageBoxImage.Information,
                    NotificationType.Warning => MessageBoxImage.Warning,
                    NotificationType.Error => MessageBoxImage.Error,
                    _ => MessageBoxImage.Information
                };

                MessageBoxHelper.Show(message, title, MessageBoxButton.OK, icon);
            });
        }

        public void ShowSuccess(string message, string title = "Success")
        {
            ShowNotification(title, message, NotificationType.Success);
        }

        public void ShowWarning(string message, string title = "Warning")
        {
            ShowNotification(title, message, NotificationType.Warning);
        }

        public void ShowError(string message, string title = "Error")
        {
            ShowNotification(title, message, NotificationType.Error);
        }

        public void ShowDownloadComplete(string gameName)
        {
            ShowSuccess($"{gameName} has been downloaded successfully!", "Download Complete");
        }

        public void ShowInstallComplete(string gameName)
        {
            ShowSuccess($"{gameName} has been installed successfully!\n\nRestart Steam for changes to take effect.", "Installation Complete");
        }

        public void ShowUpdateAvailable(string version)
        {
            ShowNotification("Update Available", $"A new version ({version}) is available!", NotificationType.Info);
        }
    }

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
