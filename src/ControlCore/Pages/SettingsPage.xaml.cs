using System.Diagnostics;
using ControlCore.Core.Helpers;
using ControlCore.Core.Services;
using ControlCore.Services;

namespace ControlCore.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();

        VersionValueText.Text =
            FileVersionInfo.GetVersionInfo(Environment.ProcessPath).ProductVersion ?? "unknown";
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await CheckStartupTask();

        // Load theme preference (System | Light | Dark)
        ThemeCombo.SelectionChanged -= ThemeCombo_SelectionChanged;
        foreach (var item in ThemeCombo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string tag && tag == ThemeService.Saved)
            {
                item.IsSelected = true;
                break;
            }
        }
        ThemeCombo.SelectionChanged += ThemeCombo_SelectionChanged;
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo.SelectedItem is ComboBoxItem item && item.Tag is string theme)
        {
            ThemeService.Saved = theme;
            if (App.MainWnd?.Content is FrameworkElement root)
                ThemeService.ApplyTo(root, theme);
        }
    }

    private async Task CheckStartupTask()
    {
        bool exists = await LogonTaskService.ExistsAsync();

        // Detach events to avoid infinite loop
        LaunchOnStartupCheck.Checked -= LaunchOnStartupCheck_Checked;
        LaunchOnStartupCheck.Unchecked -= LaunchOnStartupCheck_Unchecked;

        LaunchOnStartupCheck.IsChecked = exists;

        LaunchOnStartupCheck.Checked += LaunchOnStartupCheck_Checked;
        LaunchOnStartupCheck.Unchecked += LaunchOnStartupCheck_Unchecked;
    }

    private async void LaunchOnStartupCheck_Checked(object sender, RoutedEventArgs e)
    {
        bool ok = await LogonTaskService.EnableAsync();
        if (!ok)
            await Helpers.Dialogs.Error("Failed to create startup task.", "Error");
        await CheckStartupTask();
    }

    private async void LaunchOnStartupCheck_Unchecked(object sender, RoutedEventArgs e)
    {
        await LogonTaskService.DisableAsync();
        await CheckStartupTask();
    }

    // -----------------------------------------------------------------------
    // Unblock (Mark-of-the-Web removal) — see Core Helpers/MotwUnblocker.cs
    // -----------------------------------------------------------------------

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
                    "close the apps using them, or re-run ControlCore as Administrator and try again.",
                    "Unblock");
        }
        finally
        {
            UnblockFolderBtn.IsEnabled = true;
        }
    }
}
