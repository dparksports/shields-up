using System.Diagnostics;
using ShieldsUp.Core.Services;

namespace ShieldsUp.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly Core.Services.UpdateCheckService _updates = new();
    private Core.Services.UpdateCheckService.UpdateInfo? _lastUpdate;

    public SettingsPage()
    {
        InitializeComponent();

        // Clean version for display — the assembly informational version carries
        // a "+<commit>" suffix from the build, which is forensics, not UI
        string current = Core.Services.UpdateCheckService.CleanVersion(
            Core.Services.UpdateCheckService.GetCurrentVersion());
        VersionValueText.Text = current;
        UpdateStatusText.Text = $"You are running {current}. Check against the GitHub release for a newer version.";
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

    // -----------------------------------------------------------------------
    // Updates — check the GitHub release, download + SHA256-verify the zip
    // -----------------------------------------------------------------------

    private async void CheckUpdatesBtn_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesBtn.IsEnabled = false;
        DownloadUpdatesBtn.IsEnabled = false;
        UpdateStatusText.Text = "Checking the GitHub release…";
        try
        {
            var info = await _updates.CheckAsync();
            _lastUpdate = info;
            string current = Core.Services.UpdateCheckService.CleanVersion(
                Core.Services.UpdateCheckService.GetCurrentVersion());

            if (!string.IsNullOrEmpty(info.Error))
            {
                UpdateStatusText.Text = $"Check failed: {info.Error}";
                return;
            }
            if (info.ParseFailed)
            {
                UpdateStatusText.Text = $"Latest release tag \"{info.LatestTag}\" could not be compared — open the release page to see it.";
                return;
            }
            if (info.UpdateAvailable)
            {
                UpdateStatusText.Text = $"New version {info.LatestTag} available (you are running {current}).";
                DownloadUpdatesBtn.IsEnabled = !string.IsNullOrEmpty(info.ZipUrl);
            }
            else
            {
                UpdateStatusText.Text = $"You are up to date — {info.LatestTag} is the latest release.";
            }
        }
        finally
        {
            CheckUpdatesBtn.IsEnabled = true;
        }
    }

    private async void DownloadUpdatesBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_lastUpdate?.ZipUrl == null) return;

        DownloadUpdatesBtn.IsEnabled = false;
        CheckUpdatesBtn.IsEnabled = false;
        UpdateProgress.Value = 0;
        UpdateProgress.Visibility = Visibility.Visible;
        UpdateStatusText.Text = "Downloading…";

        try
        {
            var progress = new Progress<double>(p =>
            {
                UpdateProgress.Value = p;
                UpdateStatusText.Text = $"Downloading… {p:F0}%";
            });

            var result = await _updates.DownloadAndVerifyAsync(_lastUpdate, progress);

            if (result.Success)
            {
                UpdateStatusText.Text = $"{Path.GetFileName(result.ZipPath)} — {result.Message}";
                bool open = await Helpers.Dialogs.Confirm(
                    $"{Path.GetFileName(result.ZipPath)}\n\n{result.Message}\nSHA256: {result.Sha256}\n\nSaved to: {result.ZipPath}\n\nOpen the folder now? Extract and run the new ShieldsUp.exe to install.",
                    "Update downloaded", yes: "Open folder", no: "Close");
                if (open)
                    await Windows.System.Launcher.LaunchFolderPathAsync(
                        Path.GetDirectoryName(result.ZipPath) ?? string.Empty);
            }
            else
            {
                UpdateStatusText.Text = result.Message;
                await Helpers.Dialogs.Error(result.Message, "Update download failed");
            }
        }
        finally
        {
            UpdateProgress.Visibility = Visibility.Collapsed;
            CheckUpdatesBtn.IsEnabled = true;
            DownloadUpdatesBtn.IsEnabled = _lastUpdate?.UpdateAvailable == true;
        }
    }

    private async void OpenReleasePageBtn_Click(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(
            new Uri(Core.Services.UpdateCheckService.ReleasePage));
    }
}
