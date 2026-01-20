using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SolusManifestApp.Interfaces;
using SolusManifestApp.Models;
using SolusManifestApp.Services;
using SolusManifestApp.Views.Dialogs;

namespace SolusManifestApp.ViewModels
{
    public partial class FixInstallerViewModel : ObservableObject
    {
        private readonly FixApplierService _fixApplier;
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private string _appId = string.Empty;

        [ObservableProperty]
        private string _installPath = string.Empty;

        [ObservableProperty]
        private string _gameName = "Enter App ID to check for fix";

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _isApplyFromUrlEnabled = false;

        [ObservableProperty]
        private bool _isUnfixEnabled = false;

        [ObservableProperty]
        private bool _isApplyFromFileEnabled = true;

        [ObservableProperty]
        private ObservableCollection<string> _fixHistory = new ObservableCollection<string>();

        [ObservableProperty]
        private string? _selectedHistoryItem;

        private List<FixHistoryEntry> _fixHistoryEntries = new List<FixHistoryEntry>();

        public FixInstallerViewModel(FixApplierService fixApplier, ISettingsService settingsService)
        {
            _fixApplier = fixApplier;
            _settingsService = settingsService;
            LoadFixHistory();
        }

        private void LoadFixHistory()
        {
            _fixHistoryEntries = FixHistoryManager.LoadHistory();
            FixHistory.Clear();
            foreach (var entry in _fixHistoryEntries)
            {
                FixHistory.Add($"{entry.GameName} ({entry.AppId})");
            }
        }

        partial void OnSelectedHistoryItemChanged(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                var index = FixHistory.IndexOf(value);
                if (index >= 0 && index < _fixHistoryEntries.Count)
                {
                    var entry = _fixHistoryEntries[index];
                    AppId = entry.AppId.ToString();
                    InstallPath = entry.InstallPath;
                    CheckForFixCommand.Execute(null);
                }
            }
        }

        [RelayCommand]
        private void BrowseInstallPath()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(InstallPath) && Directory.Exists(InstallPath))
            {
                dialog.SelectedPath = InstallPath;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                InstallPath = dialog.SelectedPath;
            }
        }

        [RelayCommand]
        private async Task CheckForFix()
        {
            if (!int.TryParse(AppId, out var appId))
            {
                StatusMessage = "Invalid App ID";
                return;
            }

            StatusMessage = $"Checking for fix for App ID: {appId}...";
            IsApplyFromUrlEnabled = false;
            IsUnfixEnabled = false;
            IsApplyFromFileEnabled = true;

            var gameName = await _fixApplier.GetAppName(appId);
            GameName = $"Game: {gameName}";

            var installPath = _fixApplier.GetGameInstallPath(appId);
            if (!string.IsNullOrEmpty(installPath))
            {
                InstallPath = installPath;
            }

            bool fixApplied = !string.IsNullOrEmpty(InstallPath) && File.Exists(Path.Combine(InstallPath, $"basement-fix-log-{appId}.log"));

            IsUnfixEnabled = fixApplied;

            var fixAvailable = await _fixApplier.CheckForFix(appId);

            if (fixApplied)
            {
                StatusMessage = "A fix is already applied. You can unfix it.";
                IsApplyFromUrlEnabled = false;
                IsApplyFromFileEnabled = false;
            }
            else if (fixAvailable)
            {
                StatusMessage = $"Fix found for {gameName}. Ready to apply.";
                IsApplyFromUrlEnabled = true;
            }
            else
            {
                StatusMessage = $"No fix found for {gameName}.";
            }
        }

        [RelayCommand]
        private async Task GetAppInfo()
        {
            if (!int.TryParse(AppId, out var appId))
            {
                StatusMessage = "Invalid App ID";
                return;
            }

            StatusMessage = $"Getting info for App ID: {appId}...";

            var gameName = await _fixApplier.GetAppName(appId);
            GameName = $"Game: {gameName}";

            var installPath = _fixApplier.GetGameInstallPath(appId);
            if (!string.IsNullOrEmpty(installPath))
            {
                InstallPath = installPath;
            }
            else
            {
                StatusMessage = "Game install path not found. Please browse manually.";
            }

            bool fixApplied = !string.IsNullOrEmpty(InstallPath) && File.Exists(Path.Combine(InstallPath, $"basement-fix-log-{appId}.log"));
            IsUnfixEnabled = fixApplied;

            if (fixApplied)
            {
                StatusMessage = "Game info loaded. A fix is already applied.";
            }
            else
            {
                StatusMessage = "Game info loaded. Ready to apply local fix.";
            }
        }

        [RelayCommand]
        private async Task ApplyFromUrl()
        {
            if (!int.TryParse(AppId, out var appId) || string.IsNullOrEmpty(InstallPath) || !Directory.Exists(InstallPath))
            {
                MessageBox.Show("Invalid App ID or Install Path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                Func<string?> getPassword = () =>
                {
                    string? password = null;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dialog = new PasswordDialog();
                        if (dialog.ShowDialog() == true)
                        {
                            password = dialog.Password;
                        }
                    });
                    return password;
                };

                await _fixApplier.ApplyFixFromUrl(appId, InstallPath, LogStatus, getPassword);
                IsUnfixEnabled = true;
                IsApplyFromUrlEnabled = false;
                IsApplyFromFileEnabled = false;
                StatusMessage = "Fix applied successfully! You can unfix it if needed.";
                LoadFixHistory();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error applying fix: {ex.Message}";
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ApplyFromFile()
        {
            if (!int.TryParse(AppId, out var appId))
            {
                MessageBox.Show("Please enter a valid App ID first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrEmpty(InstallPath) || !Directory.Exists(InstallPath))
            {
                MessageBox.Show("Please specify a valid install path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var openFileDialog = new OpenFileDialog
            {
                Filter = "Archive Files (*.zip, *.rar)|*.zip;*.rar|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    Func<string?> getPassword = () =>
                    {
                        string? password = null;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var dialog = new PasswordDialog();
                            if (dialog.ShowDialog() == true)
                            {
                                password = dialog.Password;
                            }
                        });
                        return password;
                    };

                    await _fixApplier.ApplyFixFromFile(openFileDialog.FileName, appId, InstallPath, LogStatus, "local file", getPassword);
                    IsUnfixEnabled = true;
                    IsApplyFromUrlEnabled = false;
                    IsApplyFromFileEnabled = false;
                    StatusMessage = "Fix applied successfully! You can unfix it if needed.";
                    LoadFixHistory();
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error applying fix: {ex.Message}";
                    MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task Unfix()
        {
            if (!int.TryParse(AppId, out var appId) || string.IsNullOrEmpty(InstallPath) || !Directory.Exists(InstallPath))
            {
                MessageBox.Show("Invalid App ID or Install Path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                await _fixApplier.Unfix(appId, InstallPath, LogStatus);
                LoadFixHistory();
                var logPath = Path.Combine(InstallPath, $"basement-fix-log-{appId}.log");
                if (!File.Exists(logPath) || new FileInfo(logPath).Length == 0)
                {
                    if(File.Exists(logPath)) File.Delete(logPath);
                    await CheckForFix();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error unfixing: {ex.Message}";
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogStatus(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = message;
            });
        }
    }
}
