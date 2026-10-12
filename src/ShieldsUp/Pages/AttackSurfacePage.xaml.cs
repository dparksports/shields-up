using System.Management;
using System.ServiceProcess;
using ShieldsUp.Core.Helpers;
using ShieldsUp.Core.Models;

namespace ShieldsUp.Pages;

public sealed partial class AttackSurfacePage : Page
{
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
            LoadKdnetStatus(),
            LoadWanMiniports(),
            LoadWifiDirectAdapters(),
            LoadKdnetAdapters()
        );

        // Sync button label to actual service state after any refresh
        UpdateSstpButtonLabel();
    }

    private void UpdateSstpButtonLabel()
    {
        try
        {
            using var sc = new ServiceController("SstpSvc");
            bool active = sc.Status == ServiceControllerStatus.Running
                       || sc.Status == ServiceControllerStatus.StartPending;
            SstpToggleBtn.Content = active
                ? "Disable + Neuter Miniports"
                : "Re-enable Service & Miniports";
        }
        catch
        {
            SstpToggleBtn.Content = "Toggle SSTP";
        }
    }

    // ── SSTP ──
    private Task LoadSstpStatus()
    {
        return Task.Run(() =>
        {
            try
            {
                using (var sc = new ServiceController("SstpSvc"))
                {
                    string status = sc.Status.ToString();
                    string startMode = ServiceHelper.GetStartMode("SstpSvc");
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        SstpStatusText.Text = $"{status} | Startup: {startMode}";
                        SetSstpSummary(status);
                    });
                }
            }
            catch
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    SstpStatusText.Text = "Service not found";
                    SetSstpSummary("NotFound");
                });
            }
        });
    }

    private void SetSstpSummary(string status)
    {
        bool active = status is "Running" or "StartPending";
        SstpCountText.Text = active ? "Active ⚠" : "Off ✓";
        SstpCountText.Foreground = active
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x1E, 0x8E, 0x3E));
    }

    private async void SstpToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        SstpToggleBtn.IsEnabled = false;
        try
        {
            bool isRunning;
            using (var sc = new ServiceController("SstpSvc"))
                isRunning = sc.Status == ServiceControllerStatus.Running
                         || sc.Status == ServiceControllerStatus.StartPending;

            if (isRunning)
            {
                // ── DISABLE path ──
                if (!await Helpers.Dialogs.Confirm(
                        "This will stop the SSTP service and disable all VPN miniport adapters (SSTP, IKEv2, L2TP, PPTP, AgileVPN, NDISWAN).\n\nYou can re-enable them at any time with the same button.",
                        "Disable SSTP & Miniports", yes: "OK", no: "Cancel"))
                    return;

                await Task.Run(() =>
                {
                    ServiceHelper.StopAndDisable("SstpSvc");
                    WanMiniportRemover.SetSstpMiniportsEnabled(false);
                });

                SstpToggleBtn.Content = "Re-enable Service & Miniports";
            }
            else
            {
                // ── ENABLE path ──
                await Task.Run(() =>
                {
                    WanMiniportRemover.SetSstpMiniportsEnabled(true);
                    ServiceHelper.EnableAndStart("SstpSvc");
                });

                SstpToggleBtn.Content = "Disable + Neuter Miniports";
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
            string output = ProcessRunner.Run("netsh", "wlan show hostednetwork");
            string status = "Unknown";
            if (output.Contains("Not available")) status = "Not Available";
            else if (output.Contains("Not started")) status = "Not Started";
            else if (output.Contains("Started")) status = "Started (ACTIVE!)";
            DispatcherQueue.TryEnqueue(() => WifiDirectStatusText.Text = status);
        });
    }

    private async void WifiDirectToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        string output = await ProcessRunner.RunAsync("netsh", "wlan show hostednetwork");
        if (output.Contains("Started"))
        {
            await ProcessRunner.RunAsync("netsh", "wlan stop hostednetwork");
            await ProcessRunner.RunAsync("netsh", "wlan set hostednetwork mode=disallow");
        }
        else
        {
            await ProcessRunner.RunAsync("netsh", "wlan set hostednetwork mode=allow");
        }
        await LoadWifiDirectStatus();
        await LoadWifiDirectAdapters();
    }

    private async void UninstallWifiBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm("Remove WiFi Direct Virtual Adapter?", "Confirm"))
            return;

        var results = await Task.Run(() => WanMiniportRemover.RemoveWifiDirect());
        RemovalResultText.Text = string.Join("\n", results);
        await LoadWifiDirectAdapters();
    }

    private Task LoadWifiDirectAdapters()
    {
        return Task.Run(() =>
        {
            var adapters = new List<NetworkAdapterItem>();
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT Name, NetConnectionStatus FROM Win32_NetworkAdapter WHERE Name LIKE '%Wi-Fi Direct%' OR Name LIKE '%Microsoft Wi-Fi Direct Virtual Adapter%'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    adapters.Add(new NetworkAdapterItem
                    {
                        Name = obj["Name"]?.ToString() ?? "",
                        Status = DecodeNetStatus(obj["NetConnectionStatus"])
                    });
                }
            }
            catch { }
            DispatcherQueue.TryEnqueue(() =>
            {
                WifiDirectGrid.ItemsSource = adapters;
                SetCount(WifiDirectCountText, WifiDirectSectionCount, adapters.Count, "found");
            });
        });
    }

    // ── KDNET ──
    private Task LoadKdnetStatus()
    {
        return Task.Run(() =>
        {
            string output = ProcessRunner.Run("bcdedit", "/enum {current}");
            bool debugOn = output.Contains("debug") && output.Contains("Yes");
            DispatcherQueue.TryEnqueue(() =>
            {
                KdnetStatusText.Text = debugOn ? "Kernel Debug ENABLED" : "Kernel Debug Disabled";
                KdnetCountText.Text = debugOn ? "On ⚠" : "Off ✓";
                KdnetCountText.Foreground = new SolidColorBrush(debugOn
                    ? Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00)
                    : Windows.UI.Color.FromArgb(255, 0x1E, 0x8E, 0x3E));
            });
        });
    }

    private async void KdnetToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        string output = ProcessRunner.Run("bcdedit", "/enum {current}");
        bool debugOn = output.Contains("debug") && output.Contains("Yes");

        if (debugOn)
        {
            await ProcessRunner.RunAsync("bcdedit", "/debug off");
        }
        else
        {
            if (!await Helpers.Dialogs.Confirm("Enable kernel debugging? This is a security risk.", "Warning"))
                return;
            await ProcessRunner.RunAsync("bcdedit", "/debug on");
        }
        await LoadKdnetStatus();
    }

    private Task LoadKdnetAdapters()
    {
        return Task.Run(() =>
        {
            var adapters = new List<NetworkAdapterItem>();
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT Name, NetConnectionStatus FROM Win32_NetworkAdapter WHERE Name LIKE '%Kernel Debug%' OR Name LIKE '%KDNIC%'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    adapters.Add(new NetworkAdapterItem
                    {
                        Name = obj["Name"]?.ToString() ?? "",
                        Status = DecodeNetStatus(obj["NetConnectionStatus"])
                    });
                }
            }
            catch { }
            DispatcherQueue.TryEnqueue(() =>
            {
                KdnetGrid.ItemsSource = adapters;
                SetCount(KdnetCountText, KdnetSectionCount, adapters.Count, "found");
            });
        });
    }

    // ── WAN Miniports ──
    private Task LoadWanMiniports()
    {
        return Task.Run(() =>
        {
            var adapters = new List<NetworkAdapterItem>();
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT Name, NetConnectionStatus FROM Win32_NetworkAdapter WHERE Name LIKE '%WAN Miniport%'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    adapters.Add(new NetworkAdapterItem
                    {
                        Name = obj["Name"]?.ToString() ?? "",
                        Status = DecodeNetStatus(obj["NetConnectionStatus"])
                    });
                }
            }
            catch { }
            DispatcherQueue.TryEnqueue(() =>
            {
                WanMiniportGrid.ItemsSource = adapters;
                SetCount(MiniportCountText, MiniportSectionCount, adapters.Count, "present");
            });
        });
    }

    /// <summary>
    /// At-a-glance count rundown: the top strip chip and the owning section's
    /// badge both show the adapter count — green at zero (nothing exposed),
    /// amber otherwise.
    /// </summary>
    private static void SetCount(TextBlock chip, TextBlock badge, int count, string verb)
    {
        string text = $"{count} {verb}" + (count == 0 ? " ✓" : "");
        chip.Text = text;
        chip.Foreground = new SolidColorBrush(count == 0
            ? Windows.UI.Color.FromArgb(255, 0x1E, 0x8E, 0x3E)
            : Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00));
        badge.Text = text;
        badge.Foreground = chip.Foreground;
    }

    private async void RemoveAllMiniportsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm("Remove ALL WAN Miniport adapters? This will disable VPN capabilities.", "Confirm"))
            return;

        var results = await Task.Run(() => WanMiniportRemover.Execute());
        RemovalResultText.Text = results.Count > 0 ? string.Join("\n", results) : "No WAN Miniports found.";
        await LoadWanMiniports();
    }

    private static string DecodeNetStatus(object status)
    {
        if (status == null) return "N/A";
        return ((int)(ushort)status) switch
        {
            0 => "Disconnected",
            1 => "Connecting",
            2 => "Connected",
            3 => "Disconnecting",
            4 => "Hardware not present",
            5 => "Hardware disabled",
            6 => "Hardware malfunction",
            7 => "Media disconnected",
            _ => status.ToString()
        };
    }
}
