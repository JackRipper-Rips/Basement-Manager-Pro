using System.Windows;
using System.Windows.Input;

namespace SolusManifestApp.Views.Dialogs
{
    public partial class PasswordDialog : Window
    {
        public string Password { get; private set; } = string.Empty;

        public PasswordDialog()
        {
            InitializeComponent();

            // Set owner to main window if available
            if (Application.Current.MainWindow != null && Application.Current.MainWindow != this)
            {
                Owner = Application.Current.MainWindow;
            }

            PasswordBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Password = PasswordBox.Password;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Password = PasswordBox.Password;
                DialogResult = true;
                Close();
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow the window to be dragged by clicking anywhere on it
            try
            {
                DragMove();
            }
            catch
            {
                // Ignore any exceptions (can occur if window is maximized or during certain operations)
            }
        }
    }
}
