using SolusManifestApp.Views.Dialogs;
using System.Windows;

namespace SolusManifestApp.Helpers
{
    public static class MessageBoxHelper
    {
        public static MessageBoxResult Show(string message, string title = "Message", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            var customButtons = buttons switch
            {
                MessageBoxButton.OK => CustomMessageBoxButton.OK,
                MessageBoxButton.OKCancel => CustomMessageBoxButton.OKCancel,
                MessageBoxButton.YesNo => CustomMessageBoxButton.YesNo,
                MessageBoxButton.YesNoCancel => CustomMessageBoxButton.YesNoCancel,
                _ => CustomMessageBoxButton.OK
            };

            var result = CustomMessageBox.Show(message, title, customButtons);

            return result switch
            {
                CustomMessageBoxResult.OK => MessageBoxResult.OK,
                CustomMessageBoxResult.Cancel => MessageBoxResult.Cancel,
                CustomMessageBoxResult.Yes => MessageBoxResult.Yes,
                CustomMessageBoxResult.No => MessageBoxResult.No,
                _ => MessageBoxResult.None
            };
        }
    }
}
