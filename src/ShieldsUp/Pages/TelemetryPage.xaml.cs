using System.Collections.ObjectModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using ShieldsUp.Core.Services;

namespace ShieldsUp.Pages;

public sealed partial class TelemetryPage : Page
{
    private readonly TelemetryRulesService _service = new();
    private readonly ObservableCollection<TelemetryRow> _rows = new();

    private bool _isInitialized;

    /// <summary>UI row over TelemetryRuleStatus.</summary>
    private sealed class TelemetryRow
    {
        public string AppName { get; set; }
        public string Category { get; set; }
        public string StateText { get; set; }
        public string Evidence { get; set; }
        public Brush StateBrush { get; set; }
        public TelemetryRuleStatus Status { get; set; }
    }

    public TelemetryPage()
    {
        InitializeComponent();
        RulesGrid.ItemsSource = _rows;
        RulesGrid.SelectionChanged += RulesGrid_SelectionChanged;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await ScanAsync();
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e) => await ScanAsync();

    private async Task ScanAsync()
    {
        ScanBtn.IsEnabled = false;
        StatusText.Text = "Scanning installed software against the opt-out knowledge base…";
        try
        {
            var statuses = await _service.EvaluateAllAsync();
            _rows.Clear();
            foreach (var status in statuses)
            {
                // Undetected apps stay visible but sorted last: users should
                // see what the KB covers even when it is not installed here
                _rows.Add(new TelemetryRow
                {
                    AppName = status.Rule.AppName,
                    Category = status.Rule.Category,
                    StateText = status.Detected ? status.StateText : "Not installed",
                    Evidence = status.Evidence,
                    StateBrush = BrushFor(status),
                    Status = status
                });
            }
            var detected = statuses.Where(s => s.Detected).ToList();
            var on = detected.Where(s => s.State == TelemetryState.On).ToList();
            var ineffective = detected.Where(s => s.State == TelemetryState.Ineffective).ToList();
            var blocked = detected.Where(s => s.Enforced).ToList();
            StatusText.Text = detected.Count == 0
                ? "None of the known-telemetry apps are installed on this machine."
                : $"{detected.Count} detected — {on.Count} still sending usage data"
                  + (ineffective.Count > 0 ? $", {ineffective.Count} opt-out(s) present but not honored — re-apply to fix" : "")
                  + (blocked.Count > 0 ? $", {blocked.Count} network-blocked" : "")
                  + ".";
        }
        finally
        {
            ScanBtn.IsEnabled = true;
        }
    }

    private static Brush BrushFor(TelemetryRuleStatus status)
    {
        if (!status.Detected) return Solid(0xFF, 0x6B, 0x6B, 0x6B);       // gray — not installed
        return status.State switch
        {
            TelemetryState.On => Solid(0xFF, 0xB1, 0x1A, 0x1A),           // red — still sending
            TelemetryState.Off => Solid(0xFF, 0x1B, 0x7F, 0x32),          // green — opted out
            TelemetryState.Ineffective => Solid(0xFF, 0xB0, 0x5A, 0x00),  // amber — present, wrong type
            _ => Solid(0xFF, 0x6B, 0x6B, 0x6B),                           // gray — unknown
        };
    }

    private static Brush Solid(byte a, byte r, byte g, byte b) =>
        new SolidColorBrush(ColorHelper.FromArgb(a, r, g, b));

    private void RulesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not TelemetryRow row || row.Status?.Rule == null)
        {
            BlockBtn.Visibility = Visibility.Collapsed;
            UnblockBtn.Visibility = Visibility.Collapsed;
            return;
        }
        var rule = row.Status.Rule;
        DetailText.Text =
            $"{rule.AppName} — {rule.Mechanism}\n" +
            $"Scope: {rule.Scope}. Risk of opting out: {rule.RiskNote}" +
            (rule.WatchDomains.Count > 0
                ? $"\nKnown telemetry domains: {string.Join(", ", rule.WatchDomains)}"
                : "") +
            (rule.Enforce != null
                ? $"\nNetwork block: {(row.Status.Enforced ? "active" : "available")} — pins {string.Join(", ", rule.Enforce.Domains)} to 0.0.0.0 (system-wide, reversible)."
                : "");

        bool canBlock = rule.Enforce != null && row.Status.Detected;
        BlockBtn.Visibility = canBlock && !row.Status.Enforced ? Visibility.Visible : Visibility.Collapsed;
        UnblockBtn.Visibility = canBlock && row.Status.Enforced ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OptOutBtn_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not TelemetryRow row || row.Status?.Rule == null) return;
        var rule = row.Status.Rule;

        if (!await Helpers.Dialogs.Confirm(
                $"Turn off telemetry for {rule.AppName}?\n\n{rule.Mechanism}\n\n" +
                $"Risk: {rule.RiskNote}\nThe current setting is saved first and can be reverted.",
                "Opt out"))
            return;

        OptOutBtn.IsEnabled = false;
        try
        {
            var (success, message) = await _service.ApplyOptOutAsync(rule);
            if (success) await Helpers.Dialogs.Inform(message, "Opted out");
            else await Helpers.Dialogs.Error(message, "Failed");
            await ScanAsync();
        }
        finally
        {
            OptOutBtn.IsEnabled = true;
        }
    }

    private async void RevertBtn_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not TelemetryRow row || row.Status?.Rule == null) return;

        RevertBtn.IsEnabled = false;
        try
        {
            var (success, message) = await _service.RevertAsync(row.Status.Rule);
            if (success) await Helpers.Dialogs.Inform(message, "Reverted");
            else await Helpers.Dialogs.Error(message, "Revert unavailable");
            await ScanAsync();
        }
        finally
        {
            RevertBtn.IsEnabled = true;
        }
    }

    private async void BlockBtn_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not TelemetryRow row || row.Status?.Rule?.Enforce == null) return;
        var rule = row.Status.Rule;

        if (!await Helpers.Dialogs.Confirm(
                $"Block {rule.AppName} telemetry at the network level instead?\n\n" +
                $"This pins {string.Join(", ", rule.Enforce.Domains)} in the hosts file. " +
                "Use it when the app ignores its own opt-out switch, or when you want the guarantee " +
                "regardless of what the app does.\n\n" +
                "Know what this touches:\n" +
                "• System-wide — every program on this PC, including browsers, fails to reach the domain\n" +
                "• No functional impact on this app — its telemetry sender already treats network failure as harmless\n" +
                "• Reversible here (or in AutoCommand); the DNS cache is flushed so it takes effect immediately\n" +
                "• Requires the app to run elevated",
                "Block network"))
            return;

        // AutoCommand is running: offer the sinkhole address so its Black Hole
        // tab catches and logs every denied attempt. AutoCommand engages its
        // listener on its next 5-second state refresh — no restart needed.
        string address = null;
        bool autoCommandRunning = false;
        try { autoCommandRunning = System.Diagnostics.Process.GetProcessesByName("AutoCommand").Length > 0; }
        catch { /* detection is best-effort; silent pin remains the default */ }
        if (autoCommandRunning &&
            await Helpers.Dialogs.Confirm(
                "AutoCommand is running — log the blocked attempts?\n\n" +
                "Yes pins the domain at 127.0.0.1 (sinkhole): AutoCommand's Black Hole tab catches every knock with " +
                "process, PID and command line, and counts them for you.\n" +
                "No pins at 0.0.0.0 (silent): attempts are denied instantly but nothing is logged.",
                "Sinkhole?"))
        {
            address = "127.0.0.1";
        }

        BlockBtn.IsEnabled = false;
        try
        {
            var (success, message) = await _service.ApplyEnforceAsync(rule, address);
            if (success) await Helpers.Dialogs.Inform(message, "Network block");
            else await Helpers.Dialogs.Error(message, "Failed");
            await ScanAsync();
        }
        finally
        {
            BlockBtn.IsEnabled = true;
        }
    }

    private async void UnblockBtn_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not TelemetryRow row || row.Status?.Rule?.Enforce == null) return;

        UnblockBtn.IsEnabled = false;
        try
        {
            var (success, message) = await _service.RevertEnforceAsync(row.Status.Rule);
            if (success) await Helpers.Dialogs.Inform(message, "Network block removed");
            else await Helpers.Dialogs.Error(message, "Failed");
            await ScanAsync();
        }
        finally
        {
            UnblockBtn.IsEnabled = true;
        }
    }

    private async void VerifyBtn_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not TelemetryRow row || row.Status?.Rule == null) return;
        var rule = row.Status.Rule;
        if (rule.WatchDomains.Count == 0)
        {
            await Helpers.Dialogs.Inform("No known telemetry domain recorded for this app — nothing to verify.",
                "Verify");
            return;
        }

        VerifyBtn.IsEnabled = false;
        try
        {
            var seen = await _service.CheckRecentDnsLookupAsync(rule.WatchDomains[0]);
            if (seen)
                await Helpers.Dialogs.Error(
                    $"The DNS resolver cache still holds an entry for {rule.WatchDomains[0]} — something contacted it recently (within its TTL). Re-check after the TTL expires or after closing the app.",
                    "Verification");
            else
                await Helpers.Dialogs.Inform(
                    $"No recent DNS lookup for {rule.WatchDomains[0]} in the resolver cache — consistent with the opt-out working.",
                    "Verification");
        }
        finally
        {
            VerifyBtn.IsEnabled = true;
        }
    }
}
