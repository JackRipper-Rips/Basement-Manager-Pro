using SolusManifestApp.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SolusManifestApp.Views
{
    public partial class LibraryPage : UserControl
    {
        private Brush? _originalBackground;

        public LibraryPage()
        {
            InitializeComponent();
        }

        private void Grid_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Any(f => f.EndsWith(".lua", System.StringComparison.OrdinalIgnoreCase) ||
                                   f.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase)))
                {
                    e.Effects = DragDropEffects.Copy;

                    // Visual feedback - highlight background
                    if (sender is Grid grid)
                    {
                        _originalBackground = grid.Background;
                        grid.Background = new SolidColorBrush(Color.FromArgb(40, 74, 144, 226)); // Semi-transparent accent color
                    }
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            e.Handled = true;
        }

        private void Grid_DragLeave(object sender, DragEventArgs e)
        {
            // Restore original background
            if (sender is Grid grid && _originalBackground != null)
            {
                grid.Background = _originalBackground;
            }
        }

        private void Grid_Drop(object sender, DragEventArgs e)
        {
            // Restore original background
            if (sender is Grid grid && _originalBackground != null)
            {
                grid.Background = _originalBackground;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (DataContext is LibraryViewModel viewModel && viewModel.ProcessDroppedFilesCommand.CanExecute(files))
                {
                    viewModel.ProcessDroppedFilesCommand.Execute(files);
                }
            }
            e.Handled = true;
        }
    }
}
