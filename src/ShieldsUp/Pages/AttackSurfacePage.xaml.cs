using System.ServiceProcess;
using ShieldsUp.Core.Helpers;
using ShieldsUp.Core.Models;
using ShieldsUp.Core.Services;

namespace ShieldsUp.Pages;

public sealed partial class AttackSurfacePage : Page
{
    private bool _llmnrOpen;
    private bool _netbiosOpen;
    private bool _mdnsOpen;
    private AttackSurfaceService.HotpatchStatus _hotpatch = new(false, false);

    public AttackSurfacePage()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAll();
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAll();
    }

    private async Task RefreshAll()
    {
        await Task.WhenAll(
            LoadSstpStatus(),
            LoadWifiDirectStatus(),
            LoadKdnetAndAdapters(),
            LoadWanMiniports(),
            LoadWifiDirectAdapters(),
            LoadNameResolutionStatus(),
            LoadHotpatchStatus()
        );

        UpdateSstpButtonLabel();
    }

    private static SolidColorBrush GoodBrush() =>
        new(Windows.UI.Color.FromArgb(255, 0x1E, 0x8E, 0x3E));

    private static SolidColorBrush WarnBrush() =>
        new(Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00));

    private void UpdateSstpButtonLabel()
    {
        var sstp = AttackSurfaceService.GetSstpStatus();
        if (sstp.StatusText == "NotFound")
        {
            SstpToggleBtn.Content = "Toggle SSTP";
            return;
        }
        SstpToggleBtn.Content = sstp.Active
            ? "Disable + Neuter Miniports"
            : "Re-enable Service & Miniports";
    }

    // ── SSTP ──
    private Task LoadSstpStatus()
    {
        return Task.Run(() =>
        {
            var sstp = AttackSurfaceService.GetSstpStatus();
            DispatcherQueue.TryEnqueue(() =>
            {
                SstpStatusText.Text = sstp.StatusText == "NotFound"
                    ? "Service not found"
                    : $"{sstp.StatusText} | Startup: {sstp.StartMode}";
                SstpCountText.Text = sstp.Active ? "Active ⚠" : "Off ✓";
                SstpCountText.Foreground = sstp.Active ? WarnBrush() : GoodBrush();
                SstpSectionCount.Text = SstpCountText.Text;
                SstpSectionCount.Foreground = SstpCountText.Foreground;
            });
        });
    }

    private async void SstpToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        SstpToggleBtn.IsEnabled = false;
        try
        {
            var sstp = await Task.Run(AttackSurfaceService.GetSstpStatus);
            if (sstp.Active)
            {
                if (!await Helpers.Dialogs.Confirm(
                        "This will stop the SSTP service (recording its prior startup mode) and disable all VPN miniport adapters (SSTP, IKEv2, L2TP, PPTP, AgileVPN, NDISWAN).\n\nYou can re-enable them at any time with the same button.",
                        "Disable SSTP & Miniports", yes: "OK", no: "Cancel"))
                    return;

                var failures = await Task.Run(AttackSurfaceService.DisableSstp);
                SstpToggleBtn.Content = "Re-enable Service & Miniports";
                if (failures is { Count: > 0 } && failures.Any(f => f.StartsWith("Failed", StringComparison.OrdinalIgnoreCase)))
                    await Helpers.Dialogs.Inform(
                        "Some miniport adapters reported issues:\n" + string.Join("\n", failures),
                        "Partial disable");
            }
            else
            {
                var failures = await Task.Run(AttackSurfaceService.EnableSstp);
                SstpToggleBtn.Content = "Disable + Neuter Miniports";
                if (failures is { Count: > 0 } && failures.Any(f => f.StartsWith("Failed", StringComparison.OrdinalIgnoreCase)))
                    await Helpers.Dialogs.Inform(
                        "Some miniport adapters reported issues:\n" + string.Join("\n", failures),
                        "Partial enable");
            }
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Error: {ex.Message}", "SSTP Toggle");
        }
        finally
        {
            SstpToggleBtn.IsEnabled = true;
            await Task.WhenAll(LoadSstpStatus(), LoadWanMiniports());
        }
    }

    // ── WiFi Direct ──
    private Task LoadWifiDirectStatus()
    {
        return Task.Run(() =>
        {
            string status = AttackSurfaceService.GetHostedNetworkStatus();
            if (status == "Started") status = "Started (ACTIVE!)";
            DispatcherQueue.TryEnqueue(() => WifiDirectStatusText.Text = status);
        });
    }

    private async void WifiDirectToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        string status = await Task.Run(AttackSurfaceService.GetHostedNetworkStatus);
        if (status == "Started")
            await Task.Run(AttackSurfaceService.DisallowHostedNetwork);
        else
            await Task.Run(AttackSurfaceService.AllowHostedNetwork);
        await LoadWifiDirectStatus();
        await LoadWifiDirectAdapters();
    }

    private async void UninstallWifiBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm("Remove WiFi Direct Virtual Adapter?", "Confirm"))
            return;

        var results = await Task.Run(AttackSurfaceService.RemoveWifiDirectAdapters);
        RemovalResultText.Text = string.Join("\n", results);
        await LoadWifiDirectAdapters();
    }

    private Task LoadWifiDirectAdapters()
    {
        return Task.Run(() =>
        {
            var adapters = AttackSurfaceService.ListWifiDirectAdapters();
            DispatcherQueue.TryEnqueue(() =>
            {
                WifiDirectGrid.ItemsSource = adapters;
                SetCount(WifiDirectCountText, WifiDirectSectionCount, adapters.Count, "found");
            });
        });
    }

    // ── KDNET ──
    private Task LoadKdnetAndAdapters()
    {
        return Task.Run(() =>
        {
            bool debugOn = AttackSurfaceService.IsKernelDebugOn();
            var adapters = AttackSurfaceService.ListKdnetAdapters();
            DispatcherQueue.TryEnqueue(() =>
            {
                KdnetStatusText.Text = debugOn ? "Kernel Debug ENABLED" : "Kernel Debug Disabled";
                KdnetGrid.ItemsSource = adapters;

                bool exposed = debugOn || adapters.Count > 0;
                string badge = debugOn
                    ? "Debug ON ⚠"
                    : adapters.Count > 0 ? $"{adapters.Count} adapter(s) ⚠" : "Off ✓";
                var brush = exposed ? WarnBrush() : GoodBrush();
                KdnetCountText.Text = badge;
                KdnetCountText.Foreground = brush;
                KdnetSectionCount.Text = badge;
                KdnetSectionCount.Foreground = brush;
            });
        });
    }

    private async void KdnetToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        bool debugOn = await Task.Run(AttackSurfaceService.IsKernelDebugOn);
        if (debugOn)
        {
            await Task.Run(() => AttackSurfaceService.SetKernelDebug(false));
        }
        else
        {
            if (!await Helpers.Dialogs.Confirm("Enable kernel debugging? This is a security risk.", "Warning"))
                return;
            await Task.Run(() => AttackSurfaceService.SetKernelDebug(true));
        }
        await LoadKdnetAndAdapters();
    }

    // ── WAN Miniports ──
    private Task LoadWanMiniports()
    {
        return Task.Run(() =>
        {
            var adapters = AttackSurfaceService.ListWanMiniports();
            DispatcherQueue.TryEnqueue(() =>
            {
                WanMiniportGrid.ItemsSource = adapters;
                SetCount(MiniportCountText, MiniportSectionCount, adapters.Count, "present");
            });
        });
    }

    private static void SetCount(TextBlock chip, TextBlock badge, int count, string verb)
    {
        string text = $"{count} {verb}" + (count == 0 ? " ✓" : "");
        chip.Text = text;
        chip.Foreground = count == 0 ? GoodBrush() : WarnBrush();
        badge.Text = text;
        badge.Foreground = chip.Foreground;
    }

    private async void RemoveAllMiniportsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm("Remove ALL WAN Miniport adapters? This will disable VPN capabilities.", "Confirm"))
            return;

        var results = await Task.Run(AttackSurfaceService.RemoveAllWanMiniports);
        RemovalResultText.Text = results.Count > 0 ? string.Join("\n", results) : "No WAN Miniports found.";
        await LoadWanMiniports();
    }

    // ── Legacy Name-Resolution Doors (LLMNR / NetBIOS / mDNS) ──
    private Task LoadNameResolutionStatus()
    {
        return Task.Run(() =>
        {
            var llmnr = NameResolutionDoors.GetLlmnrStatus();
            var nb = NameResolutionDoors.GetNetbiosStatus();
            var mdns = NameResolutionDoors.GetMdnsStatus();
            bool nbOpen = nb.State != NameResolutionDoors.NbState.Closed && nb.Total > 0;
            int openDoors = (llmnr.Open ? 1 : 0) + (nbOpen ? 1 : 0) + (mdns.Open ? 1 : 0);

            DispatcherQueue.TryEnqueue(() =>
            {
                _llmnrOpen = llmnr.Open;
                _netbiosOpen = nbOpen;
                _mdnsOpen = mdns.Open;

                LlmnrStatusText.Text = llmnr.Evidence;
                LlmnrToggleBtn.Content = llmnr.Open ? "Disable LLMNR" : "Re-enable LLMNR";

                NetbiosStatusText.Text = nb.Evidence;
                NetbiosToggleBtn.Content = nbOpen ? "Disable NetBIOS" : "Restore NetBIOS";

                MdnsStatusText.Text = mdns.Evidence;
                MdnsToggleBtn.Content = mdns.Open ? "Block Outbound mDNS" : "Unblock Outbound mDNS";

                string summary = openDoors == 0 ? "0 open ✓" : $"{openDoors}/3 open ⚠";
                var brush = openDoors == 0 ? GoodBrush() : WarnBrush();
                NameResCountText.Text = summary;
                NameResCountText.Foreground = brush;
                NameResSectionCount.Text = summary;
                NameResSectionCount.Foreground = brush;
            });
        });
    }

    private async void LlmnrToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        LlmnrToggleBtn.IsEnabled = false;
        try
        {
            if (_llmnrOpen)
            {
                if (!await Helpers.Dialogs.Confirm(
                        "Disable Link-Local Multicast Name Resolution (LLMNR) via policy (EnableMulticast=0)?\n\n" +
                        "Prevents LAN multicast name-poisoning attacks. Prior value is snapshotted so you can revert here.",
                        "Disable LLMNR", yes: "Disable", no: "Cancel"))
                    return;
                var failures = await Task.Run(NameResolutionDoors.DisableLlmnr);
                if (failures.Count > 0)
                    await Helpers.Dialogs.Error(string.Join("\n", failures), "LLMNR");
            }
            else
            {
                var failures = await Task.Run(NameResolutionDoors.EnableLlmnr);
                if (failures.Count > 0)
                    await Helpers.Dialogs.Error(string.Join("\n", failures), "LLMNR");
            }
        }
        finally
        {
            LlmnrToggleBtn.IsEnabled = true;
            await LoadNameResolutionStatus();
        }
    }

    private async void NetbiosToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        NetbiosToggleBtn.IsEnabled = false;
        try
        {
            if (_netbiosOpen)
            {
                if (!await Helpers.Dialogs.Confirm(
                        "Set NetbiosOptions=2 (Disable NetBIOS over TCP/IP) across all interfaces?\n\n" +
                        "Per-interface values are snapshotted first for clean restore. Takes full effect after adapter restart or reboot.",
                        "Disable NetBIOS over TCP/IP", yes: "Disable", no: "Cancel"))
                    return;
                var failures = await Task.Run(NameResolutionDoors.DisableNetbios);
                if (failures.Count > 0)
                    await Helpers.Dialogs.Error(string.Join("\n", failures), "NetBIOS");
            }
            else
            {
                var failures = await Task.Run(NameResolutionDoors.EnableNetbios);
                if (failures.Count > 0)
                    await Helpers.Dialogs.Error(string.Join("\n", failures), "NetBIOS");
            }
        }
        finally
        {
            NetbiosToggleBtn.IsEnabled = true;
            await LoadNameResolutionStatus();
        }
    }

    private async void MdnsToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        MdnsToggleBtn.IsEnabled = false;
        try
        {
            if (_mdnsOpen)
            {
                if (!await Helpers.Dialogs.Confirm(
                        "Block outbound mDNS multicast (224.0.0.251 / ff02::fb, UDP 5353) with a scoped firewall rule (AC-BLOCK-MDNS)?\n\n" +
                        "Note: LAN printer, Chromecast, and AirPlay discovery rely on mDNS and will pause while this rule is active. Reversible in one click.",
                        "Block Outbound mDNS", yes: "Block", no: "Cancel"))
                    return;
                var failures = await Task.Run(NameResolutionDoors.DisableMdns);
                if (failures.Count > 0)
                    await Helpers.Dialogs.Error(string.Join("\n", failures), "mDNS");
            }
            else
            {
                var failures = await Task.Run(NameResolutionDoors.EnableMdns);
                if (failures.Count > 0)
                    await Helpers.Dialogs.Error(string.Join("\n", failures), "mDNS");
            }
        }
        finally
        {
            MdnsToggleBtn.IsEnabled = true;
            await LoadNameResolutionStatus();
        }
    }

    // ── Windows Hotpatch Monitoring Task ──
    private Task LoadHotpatchStatus()
    {
        return Task.Run(() =>
        {
            var hp = AttackSurfaceService.GetHotpatchStatus();
            DispatcherQueue.TryEnqueue(() =>
            {
                _hotpatch = hp;
                if (!hp.Present)
                {
                    HotpatchStatusText.Text = "Not registered on this Windows build — closed by absence ✓";
                    HotpatchToggleBtn.IsEnabled = false;
                    HotpatchToggleBtn.Content = "Not Present";
                    HotpatchCountText.Text = "Absent ✓";
                    HotpatchCountText.Foreground = GoodBrush();
                }
                else if (!hp.Enabled)
                {
                    HotpatchStatusText.Text = "Registered, currently Disabled ✓ (boot-time SYSTEM readiness probe is off)";
                    HotpatchToggleBtn.IsEnabled = true;
                    HotpatchToggleBtn.Content = "Re-enable Task";
                    HotpatchCountText.Text = "Disabled ✓";
                    HotpatchCountText.Foreground = GoodBrush();
                }
                else
                {
                    HotpatchStatusText.Text = "Active ⚠ — runs hpatchmonTask.cmd as SYSTEM at boot";
                    HotpatchToggleBtn.IsEnabled = true;
                    HotpatchToggleBtn.Content = "Disable Task";
                    HotpatchCountText.Text = "Active ⚠";
                    HotpatchCountText.Foreground = WarnBrush();
                }
                HotpatchSectionCount.Text = HotpatchCountText.Text;
                HotpatchSectionCount.Foreground = HotpatchCountText.Foreground;
            });
        });
    }

    private async void HotpatchToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_hotpatch.Present) return;
        HotpatchToggleBtn.IsEnabled = false;
        try
        {
            if (_hotpatch.Enabled)
            {
                if (!await Helpers.Dialogs.Confirm(
                        "Disable scheduled task '\\Microsoft\\Windows\\Hotpatch\\Monitoring'?",
                        "Disable Hotpatch Monitoring", yes: "Disable", no: "Cancel"))
                    return;
                var failures = await Task.Run(AttackSurfaceService.DisableHotpatchMonitoring);
                if (failures.Count > 0)
                    await Helpers.Dialogs.Error(string.Join("\n", failures), "Hotpatch Monitoring");
            }
            else
            {
                await AttackSurfaceService.EnableHotpatchMonitoringAsync();
            }
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Failed: {ex.Message}", "Hotpatch Monitoring");
        }
        finally
        {
            await LoadHotpatchStatus();
        }
    }
}
