using Microsoft.UI.Xaml.Controls;

namespace ShieldsUp.Helpers;

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

    // Parameter order is (message, title) on purpose: every call site in the
    // app passes the long body first and the short heading second, and with
    // the old (title, message) signature the multi-paragraph body rendered in
    // the big bold Title slot while the heading became the body. The swap
    // fixes all ~77 call sites without touching them.
    public static async Task Inform(string message, string title)
    {
        await Build(title, message, "OK", "OK", ContentDialogButton.Primary).ShowAsync();
    }

    public static async Task Error(string message, string title)
    {
        await Build($"⚠ {title}", message, "OK", "OK", ContentDialogButton.Primary).ShowAsync();
    }

    /// <summary>MessageBoxResult.Yes style confirmation.</summary>
    public static async Task<bool> Confirm(string message, string title, string yes = "Yes", string no = "No",
        bool defaultIsPrimary = true)
    {
        var dlg = Build(title, message, yes, no,
            defaultIsPrimary ? ContentDialogButton.Primary : ContentDialogButton.Close);
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }
}
