using System.Diagnostics;
using System.Management;
using System.ServiceProcess;
using ShieldsUp.Core.Helpers;
using ShieldsUp.Core.Services;
using Microsoft.UI.Xaml.Media;

namespace ShieldsUp.Pages;

public sealed partial class PrivacyPage : Page
{
    private static readonly string[] VpnServices = { "RasMan", "IKEEXT", "PolicyAgent", "RemoteAccess" };
    private const string UsageDataTaskPath = @"\Microsoft\Windows\Flighting\FeatureConfig\UsageDataReceiver";

    public PrivacyPage()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await CheckAllStatus();
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e) => await CheckAllStatus();

    private async Task CheckAllStatus()
    {
        await Task.WhenAll(
            CheckVpnStatus(),
            CheckWifiDirectStatus(),
            CheckKdnetStatus(),
            CheckUsageDataStatus(),
            CheckTamperProtection()
        );
    }

    // ── VPN Services ──
    private Task CheckVpnStatus()
    {
        return Task.Run(() =>
        {
            int running = 0, stopped = 0;
            foreach (var svcName in VpnServices)
            {
                try
                {
                    using var sc = new ServiceController(svcName);
                    if (sc.Status == ServiceControllerStatus.Running) running++;
                    else stopped++;
                }
                catch { stopped++; }
            }

            string status = running > 0
                ? $"⚠ {running} service(s) running — potential attack surface"
                : $"✓ All {VpnServices.Length} services stopped";

            DispatcherQueue.TryEnqueue(() =>
            {
                VpnStatusText.Text = status;
                VpnStatusText.Foreground = running > 0
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00))
                    : new SolidColorBrush(Microsoft.UI.Colors.ForestGreen);
            });
        });
    }

    private async void VpnDisableBtn_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            foreach (var svc in VpnServices)
                ServiceHelper.StopAndDisable(svc);
        });
        await CheckVpnStatus();
    }

    private async void VpnEnableBtn_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            foreach (var svc in VpnServices)
                ServiceHelper.EnableAndStart(svc);
        });
        await CheckVpnStatus();
    }

    // ── WiFi Direct ──
    private Task CheckWifiDirectStatus()
    {
        return Task.Run(() =>
        {
            bool found = false;
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_NetworkAdapter WHERE Name LIKE '%Wi-Fi Direct%'");
                found = searcher.Get().Count > 0;
            }
            catch { }

            DispatcherQueue.TryEnqueue(() =>
            {
                WifiDirectStatusText.Text = found ? "⚠ WiFi Direct adapter present" : "✓ No WiFi Direct adapters found";
                WifiDirectStatusText.Foreground = found
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00))
                    : new SolidColorBrush(Microsoft.UI.Colors.ForestGreen);
            });
        });
    }

    private async void WifiDirectDisableBtn_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT PNPDeviceID FROM Win32_NetworkAdapter WHERE Name LIKE '%Wi-Fi Direct%'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string deviceId = obj["PNPDeviceID"]?.ToString();
                    if (!string.IsNullOrEmpty(deviceId))
                        ProcessRunner.RunDetached("pnputil.exe", $"/disable-device \"{deviceId}\"");
                }
            }
            catch { }
        });
        await CheckWifiDirectStatus();
    }

    private async void WifiDirectEnableBtn_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT PNPDeviceID FROM Win32_NetworkAdapter WHERE Name LIKE '%Wi-Fi Direct%'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string deviceId = obj["PNPDeviceID"]?.ToString();
                    if (!string.IsNullOrEmpty(deviceId))
                        ProcessRunner.RunDetached("pnputil.exe", $"/enable-device \"{deviceId}\"");
                }
            }
            catch { }
        });
        await CheckWifiDirectStatus();
    }

    // ── KDNET ──
    private Task CheckKdnetStatus()
    {
        return Task.Run(() =>
        {
            string output = ProcessRunner.Run("bcdedit", "/enum {current}");
            bool debugOn = output.Contains("debug") && output.Contains("Yes");

            DispatcherQueue.TryEnqueue(() =>
            {
                KdnetStatusText.Text = debugOn ? "⚠ Kernel Debug is ENABLED" : "✓ Kernel Debug is disabled";
                KdnetStatusText.Foreground = debugOn
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xD7, 0x00, 0x15))
                    : new SolidColorBrush(Microsoft.UI.Colors.ForestGreen);
            });
        });
    }

    private async void KdnetDisableBtn_Click(object sender, RoutedEventArgs e)
    {
        await ProcessRunner.RunAsync("bcdedit", "/debug off");
        await CheckKdnetStatus();
    }

    private async void KdnetEnableBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm("Enable kernel debugging? This is a significant security risk.",
                "Warning"))
            return;
        await ProcessRunner.RunAsync("bcdedit", "/debug on");
        await CheckKdnetStatus();
    }

    // ── Usage Data / Telemetry ──
    private Task CheckUsageDataStatus()
    {
        return Task.Run(() =>
        {
            bool enabled = TaskSchedulerService.Instance.IsTaskEnabled(UsageDataTaskPath);

            DispatcherQueue.TryEnqueue(() =>
            {
                UsageDataStatusText.Text = enabled
                    ? "⚠ Telemetry task is active — data being sent"
                    : "✓ Telemetry task is disabled";
                UsageDataStatusText.Foreground = enabled
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00))
                    : new SolidColorBrush(Microsoft.UI.Colors.ForestGreen);
            });
        });
    }

    private async void UsageDataDisableBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await TaskSchedulerService.Instance.SetTaskEnabledAsync(UsageDataTaskPath, false);
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Failed: {ex.Message}", "Error");
        }
        await CheckUsageDataStatus();
    }

    private async void UsageDataEnableBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await TaskSchedulerService.Instance.SetTaskEnabledAsync(UsageDataTaskPath, true);
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Failed: {ex.Message}", "Error");
        }
        await CheckUsageDataStatus();
    }

    // ── Tamper Protection ──
    private Task CheckTamperProtection()
    {
        return Task.Run(() =>
        {
            bool isProtected = false;
            try
            {
                // Use WMI to get accurate Defender status since registry is protected
                var searcher = new ManagementObjectSearcher(
                    @"root\Microsoft\Windows\Defender",
                    "SELECT IsTamperProtected FROM MSFT_MpComputerStatus");

                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["IsTamperProtected"] != null && (bool)obj["IsTamperProtected"])
                    {
                        isProtected = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TamperCheck Error: {ex.Message}");
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                TamperStatusText.Text = isProtected
                    ? "✓ Tamper Protection is ON"
                    : "⚠ Tamper Protection is OFF — settings may be vulnerable";
                TamperStatusText.Foreground = isProtected
                    ? new SolidColorBrush(Microsoft.UI.Colors.ForestGreen)
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xD7, 0x00, 0x15));
                TamperIcon.Text = isProtected ? "🛡" : "⚠";
            });
        });
    }

    private async void TamperEnableBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "windowsdefender://threatsettings",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            await Helpers.Dialogs.Error($"Could not open Windows Security: {ex.Message}", "Error");
        }
    }
}
