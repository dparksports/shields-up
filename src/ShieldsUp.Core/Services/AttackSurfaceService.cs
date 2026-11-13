using System.Management;
using System.ServiceProcess;
using ShieldsUp.Core.Helpers;
using ShieldsUp.Core.Models;

namespace ShieldsUp.Core.Services;

/// <summary>
/// The "doors Windows leaves open" checks and closing actions, shared by
/// AttackSurfacePage, PrivacyPage, QuickScanService, the read-only AI bridge,
/// and ShieldsUpSelfTest.
/// </summary>
public static class AttackSurfaceService
{
    public const string SstpServiceName = "SstpSvc";

    public sealed record SstpStatus(bool Active, string StatusText, string StartMode);

    // ── SSTP / VPN miniports ──

    public static SstpStatus GetSstpStatus()
    {
        try
        {
            using var sc = new ServiceController(SstpServiceName);
            string status = sc.Status.ToString();
            string startMode = ServiceHelper.GetStartMode(SstpServiceName);
            bool active = sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending;
            return new SstpStatus(active, status, startMode);
        }
        catch
        {
            return new SstpStatus(false, "NotFound", "");
        }
    }

    public static List<string> DisableSstp()
    {
        ServiceHelper.StopAndDisable(SstpServiceName);
        return WanMiniportRemover.SetSstpMiniportsEnabled(false);
    }

    public static List<string> EnableSstp()
    {
        var failures = WanMiniportRemover.SetSstpMiniportsEnabled(true);
        ServiceHelper.EnableAndStart(SstpServiceName);
        return failures;
    }

    // ── Wi-Fi Direct ghost hotspot ──

    public static string GetHostedNetworkStatus()
    {
        string output = ProcessRunner.Run("netsh", "wlan show hostednetwork");
        if (output.Contains("Not available")) return "Not Available";
        if (output.Contains("Not started")) return "Not Started";
        if (output.Contains("Started")) return "Started";
        return "Unknown";
    }

    public static void DisallowHostedNetwork()
    {
        ProcessRunner.Run("netsh", "wlan stop hostednetwork");
        ProcessRunner.Run("netsh", "wlan set hostednetwork mode=disallow");
    }

    public static void AllowHostedNetwork()
    {
        ProcessRunner.Run("netsh", "wlan set hostednetwork mode=allow");
    }

    // ── Kernel debug (KDNET) ──

    /// <summary>
    /// Pure line-anchored parser for <c>bcdedit /enum {current}</c>.
    /// Avoids the classic false positive where <c>recoveryenabled Yes</c> +
    /// <c>debug No</c> / <c>hypervisordebug Off</c> matched substring checks.
    /// </summary>
    public static bool ParseKernelDebugOn(string output)
    {
        if (string.IsNullOrEmpty(output)) return false;
        foreach (var raw in output.Split('\n'))
        {
            string line = raw.Trim();
            if (!line.StartsWith("debug", StringComparison.OrdinalIgnoreCase))
                continue;
            string rest = line["debug".Length..];
            if (rest.Length == 0 || !char.IsWhiteSpace(rest[0]))
                continue;
            string value = rest.Trim();
            if (value.StartsWith("yes", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("true", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool IsKernelDebugOn() =>
        ParseKernelDebugOn(ProcessRunner.Run("bcdedit", "/enum {current}"));

    public static void SetKernelDebug(bool on)
    {
        ProcessRunner.Run("bcdedit", on ? "/debug on" : "/debug off");
    }

    // ── Windows Hotpatch monitoring task ──

    public const string HotpatchMonitoringTaskPath = @"\Microsoft\Windows\Hotpatch\Monitoring";

    public sealed record HotpatchStatus(bool Present, bool Enabled);

    public static HotpatchStatus GetHotpatchStatus()
    {
        string state = TaskSchedulerService.Instance.GetTaskStateText(HotpatchMonitoringTaskPath);
        return state == null
            ? new HotpatchStatus(false, false)
            : new HotpatchStatus(true, state != "Disabled");
    }

    public static List<string> DisableHotpatchMonitoring()
    {
        var failures = new List<string>();
        if (!GetHotpatchStatus().Present) return failures;
        try
        {
            TaskSchedulerService.Instance
                .SetTaskEnabledAsync(HotpatchMonitoringTaskPath, false).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            failures.Add("disable failed: " + ex.Message);
        }
        return failures;
    }

    public static Task EnableHotpatchMonitoringAsync() =>
        TaskSchedulerService.Instance.SetTaskEnabledAsync(HotpatchMonitoringTaskPath, true);

    // ── Adapter inventory ──

    public static List<NetworkAdapterItem> ListWanMiniports() =>
        ListAdapters("Name LIKE '%WAN Miniport%'");

    public static List<NetworkAdapterItem> ListWifiDirectAdapters() =>
        ListAdapters("Name LIKE '%Wi-Fi Direct%' OR Name LIKE '%Microsoft Wi-Fi Direct Virtual Adapter%'");

    public static List<NetworkAdapterItem> ListKdnetAdapters() =>
        ListAdapters("Name LIKE '%Kernel Debug%' OR Name LIKE '%KDNIC%'");

    private static List<NetworkAdapterItem> ListAdapters(string where)
    {
        var adapters = new List<NetworkAdapterItem>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT Name, NetConnectionStatus FROM Win32_NetworkAdapter WHERE {where}");
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
        return adapters;
    }

    public static List<string> RemoveWifiDirectAdapters() => WanMiniportRemover.RemoveWifiDirect();

    public static List<string> RemoveAllWanMiniports() => WanMiniportRemover.Execute();

    public static string DecodeNetStatus(object status)
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
