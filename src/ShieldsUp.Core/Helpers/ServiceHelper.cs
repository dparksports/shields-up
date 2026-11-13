using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace ShieldsUp.Core.Helpers
{
    /// <summary>
    /// Native service management — replaces PowerShell Set-Service -StartupType.
    /// Uses advapi32 P/Invoke for ChangeServiceConfig.
    /// </summary>
    public static class ServiceHelper
    {
        private const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
        private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        private const uint SERVICE_CHANGE_CONFIG = 0x0002;
        private const uint SERVICE_QUERY_CONFIG = 0x0001;

        // Start types
        public const uint SERVICE_AUTO_START = 2;
        public const uint SERVICE_DEMAND_START = 3; // Manual
        public const uint SERVICE_DISABLED = 4;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenSCManager(string machineName, string databaseName, uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string serviceName, uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ChangeServiceConfig(
            IntPtr hService,
            uint dwServiceType,
            uint dwStartType,
            uint dwErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword,
            string lpDisplayName);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        /// <summary>
        /// Set service startup type using native API.
        /// </summary>
        public static bool SetStartupType(string serviceName, uint startType)
        {
            IntPtr scManager = IntPtr.Zero;
            IntPtr service = IntPtr.Zero;
            try
            {
                scManager = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
                if (scManager == IntPtr.Zero) return false;

                service = OpenService(scManager, serviceName, SERVICE_CHANGE_CONFIG);
                if (service == IntPtr.Zero) return false;

                return ChangeServiceConfig(
                    service,
                    SERVICE_NO_CHANGE,
                    startType,
                    SERVICE_NO_CHANGE,
                    null, null, IntPtr.Zero, null, null, null, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ServiceHelper Error: {ex.Message}");
                return false;
            }
            finally
            {
                if (service != IntPtr.Zero) CloseServiceHandle(service);
                if (scManager != IntPtr.Zero) CloseServiceHandle(scManager);
            }
        }

        /// <summary>
        /// Stop and disable a service. Records the pre-disable start type
        /// (first-disable-wins) so EnableAndStart restores Auto vs Manual
        /// instead of hardcoding Manual across reboots.
        /// </summary>
        public static void StopAndDisable(string serviceName)
        {
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                    }
                }
            }
            catch { }
            string before = GetStartMode(serviceName);
            if (string.Equals(before, "Auto", StringComparison.OrdinalIgnoreCase)
                || string.Equals(before, "Manual", StringComparison.OrdinalIgnoreCase))
            {
                RecordStartType(serviceName, before);
            }
            SetStartupType(serviceName, SERVICE_DISABLED);
        }

        public static uint ResolveRestoreStartType(string recorded) =>
            string.Equals(recorded, "Auto", StringComparison.OrdinalIgnoreCase)
                ? SERVICE_AUTO_START
                : SERVICE_DEMAND_START;

        /// <summary>
        /// Re-enable and start a service, restoring its recorded pre-disable
        /// start type (Manual fallback when no record exists).
        /// </summary>
        public static void EnableAndStart(string serviceName)
        {
            string restore = TakeRecordedStartType(serviceName);
            uint startType = ResolveRestoreStartType(restore);
            SetStartupType(serviceName, startType);
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                    }
                }
            }
            catch { }
        }

        // ── pre-disable start type ledger ────────────────────────────────

        private static readonly object BackupLock = new();

        private static string LegacyAutoCommandBackupPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AutoCommand", "service_start_backup.txt");

        private static void RecordStartType(string serviceName, string startMode)
        {
            lock (BackupLock)
            {
                try
                {
                    var map = ReadBackupMapFromPaths(AppPaths.ServiceBackupPath, LegacyAutoCommandBackupPath);
                    if (map.ContainsKey(serviceName)) return; // first disable wins
                    map[serviceName] = startMode;
                    AppPaths.EnsureDataRoot();
                    File.WriteAllLines(AppPaths.ServiceBackupPath, map.Select(kv => kv.Key + "=" + kv.Value));
                }
                catch { /* best-effort: re-enable falls back to Manual */ }
            }
        }

        private static string TakeRecordedStartType(string serviceName)
        {
            lock (BackupLock)
            {
                try
                {
                    var map = ReadBackupMapFromPaths(AppPaths.ServiceBackupPath, LegacyAutoCommandBackupPath);
                    if (!map.TryGetValue(serviceName, out string mode)) return null;
                    map.Remove(serviceName);
                    AppPaths.EnsureDataRoot();
                    File.WriteAllLines(AppPaths.ServiceBackupPath, map.Select(kv => kv.Key + "=" + kv.Value));
                    return mode;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static Dictionary<string, string> ReadBackupMapFromPaths(string primaryPath, string legacyPath)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            LoadLinesInto(legacyPath, map);
            LoadLinesInto(primaryPath, map);
            return map;
        }

        private static void LoadLinesInto(string path, Dictionary<string, string> map)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    foreach (var line in File.ReadAllLines(path))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0 && line.Length > eq + 1)
                            map[line[..eq].Trim()] = line[(eq + 1)..].Trim();
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Get the current start mode of a service via WMI.
        /// </summary>
        public static string GetStartMode(string serviceName)
        {
            try
            {
                using (var service = new System.Management.ManagementObject(
                    new System.Management.ManagementPath($"Win32_Service.Name='{serviceName}'")))
                {
                    return service["StartMode"]?.ToString() ?? "Unknown";
                }
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}

