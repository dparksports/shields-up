using System.Diagnostics;
using ShieldsUp.Core.Services;

namespace ShieldsUp.Pages;

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
}
