using System.Text;
using ShieldsUp.Core.Helpers;
using ShieldsUp.Core.Models;
using ShieldsUp.Core.Services;

namespace ShieldsUp.Pages;

public sealed partial class FirewallPage : Page
{
    private List<FirewallRuleItem> _currentRules = new();
    private CommunityToolkit.WinUI.UI.Controls.DataGridColumn _sortedColumn;

    public FirewallPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Click-to-sort on any column. The toolkit DataGrid can't sort a plain
    /// List by itself, so the header click maps to a property, the rules are
    /// sorted here (group/name tiebreakers keep rows readable) and rebound,
    /// with the column arrow indicator kept in sync.
    /// </summary>
    private void RulesGrid_Sorting(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridColumnEventArgs e)
    {
        string? prop = e.Column.Header switch
        {
            "Enabled" => nameof(FirewallRuleItem.Enabled),
            "Action" => nameof(FirewallRuleItem.Action),
            "Name" => nameof(FirewallRuleItem.DisplayName),
            "Group" => nameof(FirewallRuleItem.DisplayGroup),
            "Protocol" => nameof(FirewallRuleItem.Protocol),
            "Local Port" => nameof(FirewallRuleItem.LocalPort),
            "Remote Address" => nameof(FirewallRuleItem.RemoteAddress),
            "Profile" => nameof(FirewallRuleItem.Profile),
            _ => null
        };
        if (prop == null) return;

        bool ascending = e.Column.SortDirection
            is null or not CommunityToolkit.WinUI.UI.Controls.DataGridSortDirection.Ascending;

        if (!ReferenceEquals(_sortedColumn, e.Column) && _sortedColumn != null)
            _sortedColumn.SortDirection = null; // clear the previous column's arrow
        _sortedColumn = e.Column;
        e.Column.SortDirection = ascending
            ? CommunityToolkit.WinUI.UI.Controls.DataGridSortDirection.Ascending
            : CommunityToolkit.WinUI.UI.Controls.DataGridSortDirection.Descending;

        SortRules(prop, ascending);
    }

    private void SortRules(string property, bool ascending)
    {
        Func<FirewallRuleItem, object> key = property switch
        {
            nameof(FirewallRuleItem.Enabled) => r => r.Enabled,
            nameof(FirewallRuleItem.Action) => r => r.Action,
            nameof(FirewallRuleItem.DisplayName) => r => r.DisplayName,
            nameof(FirewallRuleItem.DisplayGroup) => r => r.DisplayGroup,
            nameof(FirewallRuleItem.Protocol) => r => r.Protocol,
            nameof(FirewallRuleItem.LocalPort) => r => r.LocalPort,
            nameof(FirewallRuleItem.RemoteAddress) => r => r.RemoteAddress,
            nameof(FirewallRuleItem.Profile) => r => r.Profile,
            _ => r => r.DisplayName
        };

        // Enabled is a bool: order False first on ascending feels wrong for a
        // rules list — enabled rules on top reads better, so invert that one
        if (property == nameof(FirewallRuleItem.Enabled))
            ascending = !ascending;

        var ordered = ascending
            ? _currentRules.OrderBy(key, Comparer<object>.Create((a, b) => string.Compare(a?.ToString(), b?.ToString(), StringComparison.OrdinalIgnoreCase)))
            : _currentRules.OrderByDescending(key, Comparer<object>.Create((a, b) => string.Compare(a?.ToString(), b?.ToString(), StringComparison.OrdinalIgnoreCase)));

        _currentRules = ordered
            .ThenBy(r => r.DisplayGroup, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RulesGrid.ItemsSource = _currentRules;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadRules();
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        await LoadRules();
    }

    private async void FilterChanged(object sender, RoutedEventArgs e) => await LoadRules();

    private async Task LoadRules()
    {
        int direction = InboundRadio.IsChecked == true ? 1 : 2;
        _currentRules = await FirewallService.Instance.LoadRulesAsync(direction);

        // Grouped reading order (WPF grouped the collection; toolkit DataGrid
        // shows a flat list, so group-then-name sorting carries the structure)
        _currentRules = _currentRules
            .OrderBy(r => r.DisplayGroup, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(r => r.Enabled)
            .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RulesGrid.ItemsSource = _currentRules;

        int enabled = _currentRules.Count(r => r.Enabled);
        string dir = direction == 1 ? "Inbound" : "Outbound";
        RuleCountText.Text = $"{_currentRules.Count} {dir} rules ({enabled} enabled)";

        int legacy = await FirewallService.Instance.CountLegacyBlocksAsync();
        RenameLegacyBtn.IsEnabled = legacy > 0;
        ToolTipService.SetToolTip(RenameLegacyBtn, legacy == 0
            ? "No legacy-named AutoCommand block rules — nothing to rename."
            : $"{legacy} legacy-named AutoCommand block rule(s) found. Click to rename them to the current AC-BLOCK scheme.");
    }

    private async void ToggleRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not FirewallRuleItem rule) return;
        bool newState = !rule.Enabled;
        await FirewallService.Instance.ToggleRuleAsync(rule.Name, newState);
        rule.Enabled = newState;
    }

    private async void EnableGroup_Click(object sender, RoutedEventArgs e) => await ToggleGroup(enable: true);

    private async void DisableGroup_Click(object sender, RoutedEventArgs e) => await ToggleGroup(enable: false);

    private async Task ToggleGroup(bool enable)
    {
        if (RulesGrid.SelectedItem is not FirewallRuleItem rule) return;
        ProfileStatusText.Text = $"{(enable ? "Enabling" : "Disabling")} group '{rule.DisplayGroup}'…";
        var r = await FirewallService.Instance.ToggleGroupAsync(rule.DisplayGroup, enable);
        ProfileStatusText.Text = $"Group '{rule.DisplayGroup}': {r.Matched} matched, {r.Changed} changed, {r.Failed} failed"
                                 + (r.FirstError != null ? $" — {r.FirstError}" : "");
        await LoadRules();
    }

    private async void SaveToConfig_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not FirewallRuleItem rule) return;
        FirewallConfigManager.Instance.SetOverride(rule.Name, rule.Enabled);
        await Helpers.Dialogs.Inform(
            $"Saved: {rule.DisplayName} = {(rule.Enabled ? "Enabled" : "Disabled")}", "Config Saved");
    }

    private async void RenameLegacyBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm(
                "Block rules still using the legacy \"AutoCommand IP Block / Process Block\" names will be\n" +
                "recreated under the current AC-BLOCK naming scheme.\n\n" +
                "• Lossless — descriptions (provenance) carry over\n" +
                "• Redundant inbound duplicates are removed (covered by stateful filtering)\n" +
                "• Protection is unchanged",
                "Rename legacy blocks"))
            return;

        RenameLegacyBtn.IsEnabled = false;
        ProfileStatusText.Text = "Renaming legacy block rules…";
        try
        {
            var (targets, removedIn, migrated) = await FirewallService.Instance.ConsolidateLegacyIpBlocksAsync();
            ProfileStatusText.Text = $"Renamed legacy rules across {targets} target(s): "
                                     + $"{migrated} migrated to AC-BLOCK naming, {removedIn} redundant inbound rule(s) removed.";
        }
        catch (Exception ex)
        {
            ProfileStatusText.Text = $"Rename failed: {ex.Message}";
            await Helpers.Dialogs.Error($"Failed to rename legacy block rules: {ex.Message}", "Error");
        }
        finally
        {
            await LoadRules(); // refresh the grid and the button's enabled state
        }
    }

    private async void RestoreInternetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm(
                "Disable ALL Shields Up / AutoCommand block rules (AC-BLOCK fleet)?\n\n" +
                "This is the panic button when something important stopped working. Every block rule\n" +
                "created by this tool family is turned off at once. You can re-create blocks later.",
                "Restore Internet", defaultIsPrimary: false))
            return;

        RestoreInternetBtn.IsEnabled = false;
        ProfileStatusText.Text = "Disabling all block rules…";
        try
        {
            var result = await FirewallService.Instance.SetAllAutoCommandBlocksEnabledAsync(false);
            ProfileStatusText.Text = $"Restore Internet: {result.Matched} block rule(s) matched, {result.Changed} disabled"
                                     + (result.FirstError != null ? $" — {result.FirstError}" : "");
            await Helpers.Dialogs.Inform(
                $"{result.Changed} block rule(s) disabled.\n" +
                (result.SkippedRisky > 0 ? $"{result.SkippedRisky} risky rule(s) skipped.\n" : "") +
                "If connectivity is still broken, check the regular firewall rules below.",
                "Internet Restored");
            await LoadRules();
        }
        finally
        {
            RestoreInternetBtn.IsEnabled = true;
        }
    }

    private async void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileCombo == null || ProfileCombo.SelectedIndex <= 0) return;

        string profileName = ((ComboBoxItem)ProfileCombo.SelectedItem).Content.ToString();

        if (!await Helpers.Dialogs.Confirm(
                $"Apply '{profileName}' profile?\n\nThis will modify your Windows Defender Firewall rules.",
                "Confirm Firewall Profile"))
        {
            ProfileCombo.SelectedIndex = 0; // Reset to Custom
            return;
        }

        FirewallProfileService.ProfileType type;
        switch (profileName)
        {
            case "Shield Up": type = FirewallProfileService.ProfileType.ShieldUp; break;
            case "Gaming": type = FirewallProfileService.ProfileType.GamingMedia; break;
            case "Office": type = FirewallProfileService.ProfileType.Office; break;
            case "Home": type = FirewallProfileService.ProfileType.HomeTrusted; break;
            case "Public Strict": type = FirewallProfileService.ProfileType.StrictPublic; break;
            default:
                ProfileCombo.SelectedIndex = 0;
                return;
        }

        ProfileCombo.IsEnabled = false;
        RefreshBtn.IsEnabled = false;
        ProfileProgress.Visibility = Visibility.Visible;
        ProfileStatusText.Text = $"Applying '{profileName}' profile…";
        var progress = new Progress<string>(msg => DispatcherQueue.TryEnqueue(() => ProfileStatusText.Text = msg));

        try
        {
            ProfileApplyResult result = await FirewallProfileService.Instance.ApplyProfile(type, progress);

            await LoadRules(); // Auto-refresh the grid so the new state is shown immediately

            var sb = new StringBuilder();
            sb.AppendLine($"Profile '{profileName}' applied.");
            if (!string.IsNullOrEmpty(result.Notes)) sb.AppendLine(result.Notes);
            foreach (var g in result.Groups)
            {
                if (g.GroupName == "Shield Up (block all)") continue; // Covered by the Notes line
                sb.AppendLine($"• {g.GroupName}: {g.Matched} matched, {g.Changed} changed"
                              + (g.Failed > 0 ? $", {g.Failed} FAILED" : ""));
            }
            sb.AppendLine($"Total: {result.TotalChanged} rule(s) changed, {result.TotalFailed} failed.");
            if (result.TotalMatched == 0) sb.AppendLine("No matching rules were found — no changes were made.");
            if (result.FirstError != null) sb.AppendLine($"First error: {result.FirstError}");

            ProfileStatusText.Text = $"Profile '{profileName}': {result.TotalChanged} rule(s) changed, "
                                     + $"{result.TotalFailed} failed. Rules list refreshed.";

            if (result.TotalFailed > 0)
                await Helpers.Dialogs.Error(sb.ToString(), "Firewall Profile Complete");
            else
                await Helpers.Dialogs.Inform(sb.ToString(), "Firewall Profile Complete");
        }
        catch (Exception ex)
        {
            ProfileStatusText.Text = $"Failed to apply '{profileName}': {ex.Message}";
            await Helpers.Dialogs.Error($"Failed to apply profile: {ex.Message}", "Error");
        }
        finally
        {
            ProfileProgress.Visibility = Visibility.Collapsed;
            RefreshBtn.IsEnabled = true;
            ProfileCombo.IsEnabled = true;
            ProfileCombo.SelectedIndex = 0;
        }
    }
}
