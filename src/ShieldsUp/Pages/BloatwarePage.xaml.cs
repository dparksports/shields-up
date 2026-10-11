using ShieldsUp.Core.Helpers;
using ShieldsUp.Core.Models;
using ShieldsUp.Core.Services;
using ShieldsUp.Dialogs;

namespace ShieldsUp.Pages;

public sealed partial class BloatwarePage : Page
{
    private readonly AppManagerService _appService = new();

    public BloatwarePage()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAppsList();
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAppsList();
    }

    private async void FilterChanged(object sender, RoutedEventArgs e)
    {
        // Fires during InitializeComponent before the checkbox exists — guard it
        if (SystemComponentsCheck == null) return;
        await RefreshAppsList();
    }

    private async void RemoveBloatwareBtn_Click(object sender, RoutedEventArgs e)
    {
        RemoveBloatwareBtn.IsEnabled = false;
        AppCountText.Text = "Scanning for bloatware packages…";

        try
        {
            var result = await _appService.RemoveBloatwareAsync(
                new Progress<string>(msg => DispatcherQueue.TryEnqueue(() => AppCountText.Text = msg)),
                async matches =>
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"The following {matches.Count} package(s) will be removed:\n");
                    foreach (var app in matches)
                        sb.AppendLine($"• {app.Name}  ({app.FullName})");
                    sb.AppendLine("\nContinue?");

                    return await Helpers.Dialogs.Confirm(sb.ToString(), "Confirm Bloatware Removal");
                });

            if (result.Removed < 0)
            {
                // User cancelled the confirmation
                return;
            }

            if (result.Matched.Count == 0)
            {
                await Helpers.Dialogs.Inform("No bloatware apps were found on this system.", "Nothing to Remove");
                return;
            }

            if (result.Failed.Count == 0)
                await Helpers.Dialogs.Inform($"Removed {result.Removed} of {result.Matched.Count} package(s) successfully.",
                    "Bloatware Removal Complete");
            else
                await Helpers.Dialogs.Error(
                    $"Removed {result.Removed} of {result.Matched.Count} package(s).\n\nFailed (may be system-protected or managed):\n- "
                    + string.Join("\n- ", result.Failed),
                    "Bloatware Removal Finished");
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Bloatware removal failed: {ex.Message}", "Error");
        }
        finally
        {
            RemoveBloatwareBtn.IsEnabled = true;
            await RefreshAppsList();
        }
    }

    /// <summary>
    /// Toggles the row's app in/out of the bloatware list. Unmatched rows are
    /// added as exact package names; matched rows drop whichever entry covers
    /// them (custom addition, custom pattern, or a disabled default).
    /// </summary>
    private async void BloatwareToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is AppPackageItem app)
        {
            var source = AppManagerService.GetMatchSource(app.PackageName, app.Name, out var pattern);
            switch (source)
            {
                case AppManagerService.BloatwareMatchSource.None:
                    BloatwareConfig.Instance.AddCustomPackage(app.PackageName);
                    break;
                case AppManagerService.BloatwareMatchSource.CustomPackage:
                    BloatwareConfig.Instance.RemoveCustomPackage(app.PackageName);
                    break;
                case AppManagerService.BloatwareMatchSource.CustomPattern:
                    BloatwareConfig.Instance.RemoveCustomPattern(pattern);
                    break;
                case AppManagerService.BloatwareMatchSource.DefaultPattern:
                    BloatwareConfig.Instance.DisableDefault(pattern);
                    break;
            }
            // Rebuild so every row's toggle re-evaluates against the new config
            await RefreshAppsList();
        }
    }

    private async void ManageBloatwareBtn_Click(object sender, RoutedEventArgs e)
    {
        var editor = new BloatwareListDialog();
        var dialog = new ContentDialog
        {
            Title = "Manage Bloatware List",
            Content = editor,
            CloseButtonText = "",
            XamlRoot = App.MainWnd?.Content?.XamlRoot
        };
        // Hide the ContentDialog's own empty close button: the editor has its own Close
        dialog.CloseButtonClick += (_, _) => { };
        editor.CloseRequested += (_, _) => dialog.Hide();
        await dialog.ShowAsync();
        await RefreshAppsList();
    }

    private async Task RefreshAppsList()
    {
        AppsDataGrid.ItemsSource = null;
        var apps = await _appService.GetInstalledAppsAsync(SystemComponentsCheck.IsChecked == true);
        DispatcherQueue.TryEnqueue(() =>
        {
            AppsDataGrid.ItemsSource = apps;
            AppCountText.Text = $"{apps.Count} apps listed"
                                + (SystemComponentsCheck.IsChecked == true ? " (incl. system components)" : " — matches Windows Settings → Installed apps");
        });
    }

    private async void UninstallBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is AppPackageItem app)
        {
            if (!await Helpers.Dialogs.Confirm(
                    $"Are you sure you want to uninstall {app.Name}?\n\nFull Name: {app.FullName}",
                    "Confirm Uninstall"))
                return;

            btn.IsEnabled = false;
            bool success = await _appService.UninstallAppAsync(app.FullName);
            if (success)
            {
                await Helpers.Dialogs.Inform($"{app.Name} uninstalled successfully.", "Success");
                await RefreshAppsList();
            }
            else
            {
                await Helpers.Dialogs.Error($"Failed to uninstall {app.Name}. It might be a system-protected package.", "Error");
                btn.IsEnabled = true;
            }
        }
    }
}
