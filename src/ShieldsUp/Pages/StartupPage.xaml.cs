using ShieldsUp.Core.Helpers;
using ShieldsUp.Core.Models;
using ShieldsUp.Core.Services;

namespace ShieldsUp.Pages;

public sealed partial class StartupPage : Page
{
    private List<StartupItem> _items = new();

    public StartupPage()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadItems();
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e) => await LoadItems();

    private async Task LoadItems()
    {
        _items = await Task.Run(() =>
        {
            var active = RegistryHelper.GetStartupItems();
            var disabled = StartupSurfaceService.GetDisabledSnapshots();
            // Exclude any snapshot whose key was subsequently re-created outside Shields Up
            var activeKeys = active.Select(a => $"{a.Location}|{a.Name}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return active
                .Concat(disabled.Where(d => !activeKeys.Contains($"{d.Location}|{d.Name}")))
                .ToList();
        });
        DispatcherQueue.TryEnqueue(() => StartupGrid.ItemsSource = _items);
    }

    private async void DisableEntry_Click(object sender, RoutedEventArgs e)
    {
        if (StartupGrid.SelectedItem is not StartupItem item) return;
        if (item.IsDisabledSnapshot)
        {
            await Helpers.Dialogs.Inform($"'{item.Name}' is already disabled with a saved snapshot.", "Startup");
            return;
        }
        if (StartupSurfaceService.KindOfLocation(item.Location) != StartupSurfaceService.LogonKind.Registry)
        {
            await Helpers.Dialogs.Inform(
                $"'{item.Name}' lives in a Startup folder ({item.Location}). Snapshot-disable applies to Registry Run keys; use Delete Permanently if you want to remove the shortcut.",
                "Startup folder item");
            return;
        }

        if (!await Helpers.Dialogs.Confirm(
                $"Disable startup entry '{item.Name}' in {item.Location}?\n\n" +
                $"Target: {item.Value}\n\n" +
                "The exact command is snapshotted first so you can restore it in one click.",
                "Disable (Snapshot)", yes: "Disable", no: "Cancel"))
            return;

        var failures = await Task.Run(() => StartupSurfaceService.DisableRegistryLogon(item.Location, item.Name));
        if (failures.Count > 0)
            await Helpers.Dialogs.Error(string.Join("\n", failures), "Disable failed");
        await LoadItems();
    }

    private async void RestoreEntry_Click(object sender, RoutedEventArgs e)
    {
        if (StartupGrid.SelectedItem is not StartupItem item) return;
        if (!item.IsDisabledSnapshot && !StartupSurfaceService.HasLogonSnapshot(item.Location, item.Name))
        {
            await Helpers.Dialogs.Inform($"'{item.Name}' is currently active (no disabled snapshot to restore).", "Startup");
            return;
        }

        var failures = await Task.Run(() => StartupSurfaceService.EnableRegistryLogon(item.Location, item.Name));
        if (failures.Count > 0)
            await Helpers.Dialogs.Error(string.Join("\n", failures), "Restore failed");
        await LoadItems();
    }

    private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        if (StartupGrid.SelectedItem is not StartupItem item) return;

        if (!await Helpers.Dialogs.Confirm(
                $"Permanently delete startup entry '{item.Name}' from {item.Location}?\n\nTarget: {item.Value}",
                "Confirm Permanent Delete"))
            return;

        await Task.Run(() => RegistryHelper.DeleteStartupItem(item));
        await LoadItems();
    }
}
