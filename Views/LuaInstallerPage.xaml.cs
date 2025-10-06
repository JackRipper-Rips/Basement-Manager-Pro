using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SolusManifestApp.Views
{
    public partial class LuaInstallerPage : UserControl
    {
        private Brush _originalBackground;

        public LuaInstallerPage()
        {
            InitializeComponent();
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Any(f => f.EndsWith(".lua", System.StringComparison.OrdinalIgnoreCase) ||
                                   f.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase) ||
                                   f.EndsWith(".manifest", System.StringComparison.OrdinalIgnoreCase)))
                {
                    e.Effects = DragDropEffects.Copy;

                    // Visual feedback - highlight background
                    if (sender is Border border)
                    {
                        _originalBackground = border.Background;
                        border.Background = new SolidColorBrush(Color.FromArgb(40, 74, 144, 226)); // Semi-transparent accent
                        border.BorderBrush = new SolidColorBrush(Color.FromRgb(74, 144, 226));
                        border.BorderThickness = new Thickness(2);
                    }
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            e.Handled = true;
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            // Restore original background
            if (sender is Border border && _originalBackground != null)
            {
                border.Background = _originalBackground;
                border.BorderBrush = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
            }
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            // Restore original background
            if (sender is Border border && _originalBackground != null)
            {
                border.Background = _originalBackground;
                border.BorderBrush = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (DataContext is ViewModels.LuaInstallerViewModel viewModel && viewModel.ProcessDroppedFilesCommand.CanExecute(files))
                {
                    viewModel.ProcessDroppedFilesCommand.Execute(files);
                }
            }
            e.Handled = true;
        }
    }
}
