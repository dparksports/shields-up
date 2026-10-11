using System.Collections.ObjectModel;
using ControlCore.Core.Services;

namespace ControlCore.Pages;

public sealed partial class UpdateControlPage : Page
{
    private readonly UpdateFreezeService _freezeService = new();
    private readonly ObservableCollection<FreezeRow> _rows = new();

    private bool _isInitialized;

    /// <summary>UI row: discovery target + live frozen state.</summary>
    private sealed class FreezeRow
    {
        public string Name { get; set; }
        public string ExePath { get; set; }
        public string Evidence { get; set; }
        public string Vendor { get; set; }
        public string State { get; set; }
        public bool IsFreezable { get; set; }
        public string NotFreezableReason { get; set; }
    }

    public UpdateControlPage()
    {
        InitializeComponent();
        TargetsGrid.ItemsSource = _rows;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        // Show already-frozen apps even before a discovery scan
        await RefreshRowsAsync(_freezeService.DiscoverCandidates());
    }

    private async void DiscoverBtn_Click(object sender, RoutedEventArgs e)
    {
        DiscoverBtn.IsEnabled = false;
        StatusText.Text = "Scanning for self-updating apps…";
        try
        {
            var targets = await Task.Run(() => _freezeService.DiscoverCandidates());
            await RefreshRowsAsync(targets);
            StatusText.Text = targets.Count == 0
                ? "No self-updating apps found in the user profile. Frozen apps from earlier sessions are kept in the list."
                : $"{targets.Count} app(s) found. Select one and Freeze to pin its version — updates fail until unfrozen.";
        }
        finally
        {
            DiscoverBtn.IsEnabled = true;
        }
    }

    private async Task RefreshRowsAsync(List<UpdateFreezeTarget> targets)
    {
        _rows.Clear();
        foreach (var target in targets)
        {
            _rows.Add(new FreezeRow
            {
                Name = target.Name,
                ExePath = target.ExePath,
                Evidence = target.IsFreezable
                    ? target.Evidence
                    : $"{target.Evidence} — {target.NotFreezableReason}",
                Vendor = target.Vendor,
                State = _freezeService.IsFrozen(target.ExePath) ? "Frozen" : "Unfrozen",
                IsFreezable = target.IsFreezable,
                NotFreezableReason = target.NotFreezableReason
            });
        }
        await Task.CompletedTask;
    }

    private async void FreezeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (TargetsGrid.SelectedItem is not FreezeRow row) return;

        if (!row.IsFreezable)
        {
            await Helpers.Dialogs.Error(row.NotFreezableReason, "Cannot freeze");
            return;
        }
        if (_freezeService.IsFrozen(row.ExePath)) return;

        if (!await Helpers.Dialogs.Confirm(
                $"Freeze {row.Name} at its current version?\n\n{row.ExePath}\n\n" +
                "The exe gets a deny rule for delete/write: the app keeps working, but its updater will fail " +
                "(it may log update errors) until you unfreeze it here. The original permissions are backed up " +
                "first and restored on unfreeze.",
                "Freeze updates", defaultIsPrimary: false))
            return;

        FreezeBtn.IsEnabled = false;
        try
        {
            var (success, message) = await _freezeService.FreezeAsync(row.ExePath);
            if (success) await Helpers.Dialogs.Inform(message, "Frozen");
            else await Helpers.Dialogs.Error(message, "Freeze failed");
            await RefreshRowsAsync(_freezeService.DiscoverCandidates());
        }
        finally
        {
            FreezeBtn.IsEnabled = true;
        }
    }

    private async void ThawBtn_Click(object sender, RoutedEventArgs e)
    {
        if (TargetsGrid.SelectedItem is not FreezeRow row) return;
        if (!_freezeService.IsFrozen(row.ExePath))
        {
            await Helpers.Dialogs.Inform($"{row.Name} is not frozen.", "Info");
            return;
        }

        if (!await Helpers.Dialogs.Confirm(
                $"Unfreeze {row.Name} and restore its original permissions?\nUpdates will work again.",
                "Unfreeze"))
            return;

        ThawBtn.IsEnabled = false;
        try
        {
            var (success, message) = await _freezeService.ThawAsync(row.ExePath);
            if (success) await Helpers.Dialogs.Inform(message, "Unfrozen");
            else await Helpers.Dialogs.Error(message, "Unfreeze failed");
            await RefreshRowsAsync(_freezeService.DiscoverCandidates());
        }
        finally
        {
            ThawBtn.IsEnabled = true;
        }
    }
}
