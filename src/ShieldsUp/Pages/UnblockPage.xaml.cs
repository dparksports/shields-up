using ShieldsUp.Core.Helpers;

namespace ShieldsUp.Pages;

public sealed partial class UnblockPage : Page
{
    public UnblockPage()
    {
        InitializeComponent();
    }

    private async void UnblockAppBtn_Click(object sender, RoutedEventArgs e)
    {
        string dir = AppContext.BaseDirectory;
        UnblockAppBtn.IsEnabled = false;
        UnblockStatusText.Text = $"Scanning {dir} …";
        try
        {
            var result = await Task.Run(() => MotwUnblocker.UnblockFolder(dir));
            UnblockStatusText.Text = result.Summary;
            if (result.Unblocked > 0)
                await Helpers.Dialogs.Inform(
                    $"Removed the download mark from {result.Unblocked} file(s) in this app's folder.\n\n" +
                    "SmartScreen / Smart App Control will no longer treat them as downloaded\n" +
                    "files — taskbar pins and direct launches work normally now.",
                    "Unblocked");
        }
        finally
        {
            UnblockAppBtn.IsEnabled = true;
        }
    }

    private async void UnblockFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads,
            FileTypeFilter = { "*" }
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainWnd));
        var folder = await picker.PickSingleFolderAsync();
        if (folder == null) return;

        UnblockFolderBtn.IsEnabled = false;
        UnblockStatusText.Text = $"Scanning {folder.Path} …";
        try
        {
            var result = await Task.Run(() => MotwUnblocker.UnblockFolder(folder.Path));
            UnblockStatusText.Text = result.Summary;
            if (result.Failures.Count > 0)
                await Helpers.Dialogs.Error(
                    $"{result.Summary}\n\nFiles in use or in admin-only locations could not be updated —\n" +
                    "close the apps using them, or re-run Shields Up as Administrator and try again.",
                    "Unblock");
        }
        finally
        {
            UnblockFolderBtn.IsEnabled = true;
        }
    }
}
