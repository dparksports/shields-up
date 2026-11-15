using ShieldsUp.Core.Helpers;
using ShieldsUp.Core.Services;
using Microsoft.UI.Xaml.Media;

namespace ShieldsUp.Pages;

public sealed partial class HardeningPage : Page
{
    private const string HostsFilePath = @"C:\Windows\System32\drivers\etc\hosts";

    public HardeningPage()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAll();
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e) => await RefreshAll();

    private async Task RefreshAll()
    {
        await Task.WhenAll(
            CheckLsaStatus(),
            CheckUacStatus(),
            CheckHostsFile(),
            CheckIpv6Status(),
            CheckAccountsStatus()
        );
    }

    // ── IPv6 Hardening ──
    private Task CheckIpv6Status()
    {
        return Task.Run(() =>
        {
            bool isDisabled = RegistryHelper.GetIpv6DisabledStatus();
            DispatcherQueue.TryEnqueue(() =>
            {
                Ipv6StatusText.Text = isDisabled ? "✓ IPv6 is DISABLED globally" : "⚠ IPv6 is currently ENABLED";
                Ipv6StatusText.Foreground = isDisabled
                    ? new SolidColorBrush(Microsoft.UI.Colors.ForestGreen)
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00));
            });
        });
    }

    private async void Ipv6DisableBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm(
                "Disable IPv6 system-wide via registry (DisabledComponents=0xFF)?\n\n" +
                "Networks and services that require IPv6 will stop working until it is re-enabled. A reboot is required.",
                "Disable IPv6", yes: "Disable", no: "Cancel"))
            return;
        try
        {
            await Task.Run(() => RegistryHelper.SetIpv6DisabledStatus(true));
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Failed to disable IPv6: {ex.Message}", "Disable IPv6");
            return;
        }
        await CheckIpv6Status();
        await Helpers.Dialogs.Inform("IPv6 has been disabled via registry (DisabledComponents=0xFF). A reboot is required.", "Info");
    }

    private async void Ipv6EnableBtn_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => RegistryHelper.SetIpv6DisabledStatus(false));
        await CheckIpv6Status();
        await Helpers.Dialogs.Inform("IPv6 has been enabled. A reboot is required.", "Info");
    }

    // ── LSA Protection ──
    private Task CheckLsaStatus()
    {
        return Task.Run(() =>
        {
            bool isProtected = RegistryHelper.GetLsaProtectionStatus();
            DispatcherQueue.TryEnqueue(() =>
            {
                LsaStatusText.Text = isProtected ? "✓ LSA Protection (RunAsPPL) is ENABLED" : "⚠ LSA Protection is DISABLED";
                LsaStatusText.Foreground = isProtected
                    ? new SolidColorBrush(Microsoft.UI.Colors.ForestGreen)
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00));
            });
        });
    }

    private async void LsaEnableBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm(
                "Enable LSA Protection (RunAsPPL)?\n\n" +
                "Third-party credential providers or VPN plug-ins that hook LSA may stop working at next boot.",
                "Enable LSA Protection", yes: "Enable", no: "Cancel"))
            return;
        try
        {
            await Task.Run(() => RegistryHelper.SetLsaProtectionStatus(true));
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Failed to enable LSA Protection: {ex.Message}", "Enable LSA Protection");
            return;
        }
        await CheckLsaStatus();
        await Helpers.Dialogs.Inform("LSA Protection enabled. A reboot is required for this to take effect.", "Info");
    }

    private async void LsaDisableBtn_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => RegistryHelper.SetLsaProtectionStatus(false));
        await CheckLsaStatus();
    }

    // ── UAC Strictness ──
    private Task CheckUacStatus()
    {
        return Task.Run(() =>
        {
            bool isStrict = RegistryHelper.GetUacStrictness();
            DispatcherQueue.TryEnqueue(() =>
            {
                UacStatusText.Text = isStrict ? "✓ UAC is at Maximum Strictness" : "⚠ UAC is relaxed or disabled";
                UacStatusText.Foreground = isStrict
                    ? new SolidColorBrush(Microsoft.UI.Colors.ForestGreen)
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00));
            });
        });
    }

    private async void UacStrictBtn_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => RegistryHelper.SetUacStrictness(true));
        await CheckUacStatus();
    }

    private async void UacRelaxBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm(
                "Relax UAC from maximum strictness? This lowers the system's elevation security.",
                "Relax UAC", yes: "Relax", no: "Cancel"))
            return;
        try
        {
            await Task.Run(() => RegistryHelper.SetUacStrictness(false));
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Failed to change UAC: {ex.Message}", "Relax UAC");
            return;
        }
        await CheckUacStatus();
    }

    // ── Hosts File ──
    private Task CheckHostsFile()
    {
        return Task.Run(() =>
        {
            try
            {
                if (File.Exists(HostsFilePath))
                {
                    string content = File.ReadAllText(HostsFilePath);

                    var lines = File.ReadAllLines(HostsFilePath);
                    int activeRules = 0;
                    bool suspicious = false;
                    foreach (var line in lines)
                    {
                        var t = line.Trim();
                        if (string.IsNullOrWhiteSpace(t) || t.StartsWith("#")) continue;
                        activeRules++;
                        if (!t.Contains("localhost") && !t.Contains("127.0.0.1") && !t.Contains("::1"))
                        {
                            suspicious = true;
                        }
                    }

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        HostsContentBox.Text = content;
                        if (suspicious)
                        {
                            HostsStatusText.Text = $"⚠ Found {activeRules} active redirect(s). Potentially suspicious.";
                            HostsStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xD7, 0x00, 0x15));
                        }
                        else if (activeRules > 0)
                        {
                            HostsStatusText.Text = $"✓ Found {activeRules} standard redirect(s).";
                            HostsStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.ForestGreen);
                        }
                        else
                        {
                            HostsStatusText.Text = "✓ Hosts file is clean (default).";
                            HostsStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.ForestGreen);
                        }
                    });
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        HostsContentBox.Text = "(File not found)";
                        HostsStatusText.Text = "✓ Hosts file missing (default behavior on some systems).";
                        HostsStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.ForestGreen);
                    });
                }
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() => HostsStatusText.Text = $"Error reading hosts: {ex.Message}");
            }
        });
    }

    private async void HostsResetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm("Reset hosts file to Windows default? All custom entries will be lost.",
                "Confirm Reset"))
            return;

        await Task.Run(() =>
        {
            try
            {
                string defaultContent = @"# Copyright (c) 1993-2009 Microsoft Corp.
#
# This is a sample HOSTS file used by Microsoft TCP/IP for Windows.
#
# 127.0.0.1       localhost
# ::1             localhost
";
                File.WriteAllText(HostsFilePath, defaultContent);
            }
            catch { }
        });
        await CheckHostsFile();
    }

    // ── Local Accounts audit ────────────────────────────────────────────

    private Dictionary<string, Finding> _findingsBySid = new();

    private Task CheckAccountsStatus()
    {
        return Task.Run(() =>
        {
            AccountsStatus status = null;
            Exception error = null;
            try { status = AccountSurfaceService.GetAccountsStatus(); }
            catch (Exception ex) { error = ex; }

            DispatcherQueue.TryEnqueue(() =>
            {
                if (error is not null)
                {
                    AccountsStatusText.Text = $"Error reading account state: {error.Message}";
                    AccountsStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xD7, 0x00, 0x15));
                    AccountsList.ItemsSource = null;
                    return;
                }

                _findingsBySid = status.Findings
                    .Where(f => !string.IsNullOrEmpty(f.Sid))
                    .GroupBy(f => f.Sid, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                AccountsList.ItemsSource = status.Findings;

                if (status.Findings.Count == 0)
                {
                    AccountsStatusText.Text = status.Failures.Count > 0
                        ? "✓ no leftover accounts or profiles (see app.log for read warnings)"
                        : "✓ no leftover accounts or profiles";
                    AccountsStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.ForestGreen);
                }
                else
                {
                    int attention = status.Findings.Count(f => f.Severity == "attention");
                    AccountsStatusText.Text = $"⚠ {status.Findings.Count} leftover finding(s)"
                        + (attention > 0 ? $" ({attention} need attention)" : "");
                    AccountsStatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00));
                }
            });
        });
    }

    // x:Bind computed properties (Glyph, Title, ShowPrune) live on Finding itself

    private async void AccountPruneBtn_Click(object sender, RoutedEventArgs e)
    {
        var sid = (sender as FrameworkElement)?.Tag as string;
        if (string.IsNullOrEmpty(sid)) return;
        _findingsBySid.TryGetValue(sid, out var finding);

        var name = finding?.AccountName ?? sid;
        var detail = finding?.Detail ?? sid;
        if (!await Helpers.Dialogs.Confirm(
                $"Delete {name} ({sid})?\n\n{detail}\n\n" +
                "The account and its profile files are permanently deleted. This cannot be undone.",
                "Prune leftover", yes: "Prune", no: "Cancel", defaultIsPrimary: false))
            return;

        List<string> failures;
        try
        {
            failures = await Task.Run(() => AccountSurfaceService.PruneLeftover(sid));
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Prune crashed: {ex.Message}", "Prune failed");
            await CheckAccountsStatus();
            return;
        }

        if (failures.Count == 0)
            await Helpers.Dialogs.Inform("Pruned and verified: the account and profile are gone.", "Prune complete");
        else
            await Helpers.Dialogs.Error("Prune finished with problems:\n" + string.Join("\n", failures), "Prune failed");

        await CheckAccountsStatus();
    }
}
