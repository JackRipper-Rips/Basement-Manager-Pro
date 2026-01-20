using SolusManifestApp.Models;
using SolusManifestApp.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SolusManifestApp.Views
{
    public partial class DataBackupPage : UserControl
    {
        public DataBackupPage()
        {
            InitializeComponent();
        }

        private void OnGameSelected(object sender, RoutedEventArgs e)
        {
            if (sender is ListBoxItem item && item.Content is Game game && DataContext is DataBackupViewModel viewModel)
            {
                if (!viewModel.SelectedGames.Contains(game))
                {
                    viewModel.SelectedGames.Add(game);
                }
            }
        }

        private void OnGameUnselected(object sender, RoutedEventArgs e)
        {
            if (sender is ListBoxItem item && item.Content is Game game && DataContext is DataBackupViewModel viewModel)
            {
                viewModel.SelectedGames.Remove(game);
            }
        }
    }
}
