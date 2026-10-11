using Microsoft.UI.Xaml.Controls;

namespace ControlCore.Helpers;

/// <summary>
/// ContentDialog wrappers that replace AutoCommand's MessageBox call sites
/// (MessageBox.Show → Dialogs.Inform / Dialogs.Confirm / Dialogs.Error).
/// </summary>
public static class Dialogs
{
    private static ContentDialog Build(string title, string message, string primary, string close,
        ContentDialogButton defaultButton)
    {
        return new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primary,
            CloseButtonText = close,
            DefaultButton = defaultButton,
            XamlRoot = App.MainWnd?.Content?.XamlRoot
        };
    }

    public static async Task Inform(string title, string message)
    {
        await Build(title, message, "OK", "OK", ContentDialogButton.Primary).ShowAsync();
    }

    public static async Task Error(string title, string message)
    {
        await Build($"⚠ {title}", message, "OK", "OK", ContentDialogButton.Primary).ShowAsync();
    }

    /// <summary>MessageBoxResult.Yes style confirmation.</summary>
    public static async Task<bool> Confirm(string title, string message, string yes = "Yes", string no = "No",
        bool defaultIsPrimary = true)
    {
        var dlg = Build(title, message, yes, no,
            defaultIsPrimary ? ContentDialogButton.Primary : ContentDialogButton.Close);
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }
}
