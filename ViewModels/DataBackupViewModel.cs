using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolusManifestApp.Models;
using SolusManifestApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace SolusManifestApp.ViewModels
{
    public partial class DataBackupViewModel : ObservableObject
    {
        private readonly DataBackupService _databackupService;
        private readonly SteamApiService _steamApiService;
        private readonly NotificationService _notificationService;
        private readonly LoggerService _logger;

        [ObservableProperty]
        private string _currentUser = "Not logged in";

        [ObservableProperty]
        private string _accountId = "";

        [ObservableProperty]
        private ObservableCollection<Game> _gamesWithAchievements = new();

        [ObservableProperty]
        private ObservableCollection<Game> _selectedGames = new();

        [ObservableProperty]
        private bool _isLoading = false;

        public DataBackupViewModel(
            DataBackupService databackupService,
            SteamApiService steamApiService,
            NotificationService notificationService,
            LoggerService logger)
        {
            _databackupService = databackupService;
            _steamApiService = steamApiService;
            _notificationService = notificationService;
            _logger = logger;

            try
            {
                LoadCurrentUser();
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Failed to load current user in DataBackupViewModel: {ex.Message}");
                CurrentUser = "Error loading Steam user";
                AccountId = "";
            }
        }

        private void LoadCurrentUser()
        {
            var user = _databackupService.GetCurrentSteamUser();
            if (user != null)
            {
                CurrentUser = $"{user.PersonaName} ({user.AccountName})";
                AccountId = user.AccountId;
            }
            else
            {
                CurrentUser = "Steam user not found";
                AccountId = "";
            }
        }

        [RelayCommand]
        private async Task BackupPlaytime()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to save playtime backup",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                IsLoading = true;
                try
                {
                    await Task.Run(() =>
                    {
                        if (_databackupService.BackupPlaytime(dialog.SelectedPath))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _notificationService.ShowSuccess($"Playtime backup created successfully in:\n{dialog.SelectedPath}");
                            });
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _notificationService.ShowError("Failed to create playtime backup. Check logs for details.");
                            });
                        }
                    });
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        [RelayCommand]
        private async Task RestorePlaytime()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Playtime Backup (*.zip)|*_playtime_backup_*.zip|All ZIP files (*.zip)|*.zip",
                Title = "Select playtime backup to restore"
            };

            if (dialog.ShowDialog() == true)
            {
                var result = MessageBox.Show(
                    "This will overwrite your current playtime data. Steam should be closed.\n\nContinue?",
                    "Confirm Restore",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    IsLoading = true;
                    try
                    {
                        await Task.Run(() =>
                        {
                            if (_databackupService.RestorePlaytime(dialog.FileName))
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    _notificationService.ShowSuccess("Playtime restored successfully!\n\nRestart Steam for changes to take effect.");
                                });
                            }
                            else
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    _notificationService.ShowError("Failed to restore playtime. Check logs for details.");
                                });
                            }
                        });
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                }
            }
        }

        [RelayCommand]
        private async Task BackupAchievements()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to save achievements backup",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                IsLoading = true;
                try
                {
                    await Task.Run(() =>
                    {
                        if (_databackupService.BackupAchievements(dialog.SelectedPath))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _notificationService.ShowSuccess($"Achievements backup created successfully in:\n{dialog.SelectedPath}");
                            });
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _notificationService.ShowError("Failed to create achievements backup. Check logs for details.");
                            });
                        }
                    });
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        [RelayCommand]
        private async Task RestoreAchievements()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Achievements Backup (*.zip)|*_achievements_backup_*.zip|All ZIP files (*.zip)|*.zip",
                Title = "Select achievements backup to restore"
            };

            if (dialog.ShowDialog() == true)
            {
                var result = MessageBox.Show(
                    "This will overwrite your current achievement data. Steam should be closed.\n\nContinue?",
                    "Confirm Restore",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    IsLoading = true;
                    try
                    {
                        await Task.Run(() =>
                        {
                            if (_databackupService.RestoreAchievements(dialog.FileName))
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    _notificationService.ShowSuccess("Achievements restored successfully!\n\nRestart Steam for changes to take effect.");
                                });
                            }
                            else
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    _notificationService.ShowError("Failed to restore achievements. Check logs for details.");
                                });
                            }
                        });
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                }
            }
        }

        [RelayCommand]
        private async Task BackupAddedGames()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to save added games backup",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                IsLoading = true;
                try
                {
                    await Task.Run(() =>
                    {
                        if (_databackupService.BackupAddedGames(dialog.SelectedPath))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _notificationService.ShowSuccess($"Added games backup created successfully in:\n{dialog.SelectedPath}");
                            });
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _notificationService.ShowError("Failed to create added games backup. Check logs for details.");
                            });
                        }
                    });
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        [RelayCommand]
        private async Task RestoreAddedGames()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Added Games Backup (*.zip)|*_st_games_backup_*.zip|All ZIP files (*.zip)|*.zip",
                Title = "Select added games backup to restore"
            };

            if (dialog.ShowDialog() == true)
            {
                var result = MessageBox.Show(
                    "This will overwrite your current stplug-in files. Steam should be closed.\n\nContinue?",
                    "Confirm Restore",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    IsLoading = true;
                    try
                    {
                        await Task.Run(() =>
                        {
                            if (_databackupService.RestoreAddedGames(dialog.FileName))
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    _notificationService.ShowSuccess("Added games restored successfully!\n\nRestart Steam for changes to take effect.");
                                });
                            }
                            else
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    _notificationService.ShowError("Failed to restore added games. Check logs for details.");
                                });
                            }
                        });
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                }
            }
        }

        [RelayCommand]
        private async Task LoadGamesForReset()
        {
            IsLoading = true;
            try
            {
                await Task.Run(async () =>
                {
                    var games = _databackupService.GetGamesWithAchievements();

                    // Resolve game names using Steam API
                    var apiResponse = await _steamApiService.GetAppListAsync();
                    var apps = apiResponse?.AppList?.Apps ?? new List<SteamApp>();

                    foreach (var game in games)
                    {
                        var steamApp = apps.FirstOrDefault(a => a.AppId.ToString() == game.AppId);
                        if (steamApp != null)
                        {
                            game.Name = steamApp.Name;
                        }
                        else
                        {
                            game.Name = $"Unknown Game ({game.AppId})";
                        }
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        GamesWithAchievements = new ObservableCollection<Game>(games.OrderBy(g => g.Name));
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Failed to load games for reset: {ex.Message}");
                _notificationService.ShowError("Failed to load games with achievements.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ResetSelectedAchievements()
        {
            if (SelectedGames.Count == 0)
            {
                _notificationService.ShowWarning("Please select at least one game.");
                return;
            }

            var result = MessageBox.Show(
                $"This will reset achievements for {SelectedGames.Count} game(s). Steam should be closed.\n\nThis action cannot be undone. Continue?",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                IsLoading = true;
                try
                {
                    var appIds = SelectedGames.Select(g => g.AppId).ToList();
                    await Task.Run(() =>
                    {
                        if (_databackupService.ResetAchievements(appIds))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _notificationService.ShowSuccess($"Reset achievements for {appIds.Count} game(s).\n\nRestart Steam for changes to take effect.");

                                // Remove from list
                                foreach (var game in SelectedGames.ToList())
                                {
                                    GamesWithAchievements.Remove(game);
                                }
                                SelectedGames.Clear();
                            });
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _notificationService.ShowError("Failed to reset achievements. Check logs for details.");
                            });
                        }
                    });
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        [RelayCommand]
        private async Task ClearAllAchievements()
        {
            var result = MessageBox.Show(
                "This will reset achievements for ALL games. Steam should be closed.\n\nThis action cannot be undone. Continue?",
                "Confirm Clear All",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                IsLoading = true;
                try
                {
                    await Task.Run(() =>
                    {
                        if (_databackupService.ResetAllAchievements())
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _notificationService.ShowSuccess("Reset all achievements.\n\nRestart Steam for changes to take effect.");
                                GamesWithAchievements.Clear();
                                SelectedGames.Clear();
                            });
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _notificationService.ShowError("Failed to reset all achievements. Check logs for details.");
                            });
                        }
                    });
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }
    }
}
