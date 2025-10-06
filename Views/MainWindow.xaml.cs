using SolusManifestApp.ViewModels;
using SolusManifestApp.Services;
using System.Windows;
using System.Windows.Input;

namespace SolusManifestApp.Views
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;

        public MainWindow(MainViewModel viewModel, SettingsService settingsService)
        {
            InitializeComponent();
            DataContext = viewModel;
            _settingsService = settingsService;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;

            // Restore window size
            var settings = _settingsService.LoadSettings();
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.CheckForUpdates();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save window size
            var settings = _settingsService.LoadSettings();
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            _settingsService.SaveSettings(settings);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
