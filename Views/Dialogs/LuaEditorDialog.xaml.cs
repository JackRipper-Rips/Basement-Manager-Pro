using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SolusManifestApp.Views.Dialogs
{
    public partial class LuaEditorDialog : Window, INotifyPropertyChanged
    {
        private string _luaContent = string.Empty;
        private string _statusText = string.Empty;
        private string _filePath = string.Empty;
        private string _title = "Lua File Editor";

        public event PropertyChangedEventHandler? PropertyChanged;

        public string LuaContent
        {
            get => _luaContent;
            set
            {
                if (_luaContent != value)
                {
                    _luaContent = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        public LuaEditorDialog(string filePath)
        {
            InitializeComponent();
            DataContext = this;

            FilePath = filePath;

            if (File.Exists(filePath))
            {
                try
                {
                    LuaContent = File.ReadAllText(filePath);
                    var fileName = Path.GetFileName(filePath);
                    Title = $"Edit Lua File - {fileName}";
                    StatusText = $"Loaded {LuaContent.Length} characters";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusText = "Failed to load file";
                }
            }
            else
            {
                StatusText = "File not found";
                MessageBox.Show($"File not found: {filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Focus the text box
            Loaded += (s, e) => LuaContentTextBox.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                File.WriteAllText(FilePath, LuaContent);
                StatusText = $"Saved successfully at {DateTime.Now:HH:mm:ss}";
                MessageBox.Show("Lua file saved successfully!\n\nRestart Steam for changes to take effect.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                StatusText = "Failed to save";
                MessageBox.Show($"Failed to save file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
