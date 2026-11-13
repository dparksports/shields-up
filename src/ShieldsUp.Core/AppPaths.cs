using System.Security.AccessControl;
using System.Security.Principal;

namespace ShieldsUp.Core;

/// <summary>
/// Single anchor for ShieldsUp's on-disk state. AutoCommand kept state in the app directory
/// and under ProgramData\AutoCommand; ShieldsUp namespaces everything under ProgramData\ShieldsUp
/// with the same file names and formats so the knowledge carries over.
/// </summary>
public static class AppPaths
{
    public const string AppName = "ShieldsUp";
    private static bool _hardenAttempted;

    public static string DataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppName);

    public static string BloatwareConfigPath => Path.Combine(DataRoot, "bloatware_config.json");
    public static string FirewallConfigPath => Path.Combine(DataRoot, "firewall_config.json");
    public static string TelemetryStatePath => Path.Combine(DataRoot, "telemetry_state.json");
    public static string UpdateFreezeStatePath => Path.Combine(DataRoot, "update_freeze.json");
    public static string FreezeBackupsDir => Path.Combine(DataRoot, "freeze_backups");
    public static string EfiBaselinePath => Path.Combine(DataRoot, "efi_baseline.json");
    public static string ServiceBackupPath => Path.Combine(DataRoot, "service_start_backup.txt");
    public static string NameResolutionDoorsPath => Path.Combine(DataRoot, "name-resolution-doors.json");
    public static string StartupStatePath => Path.Combine(DataRoot, "startup-state.json");

    public static string CrashLogPath => Path.Combine(AppContext.BaseDirectory, "crash.log");

    /// <summary>
    /// Creates %ProgramData%\ShieldsUp and hardens its ACL (Administrators + SYSTEM FullControl,
    /// BuiltinUsers ReadAndExecute, inheritance disabled) so non-elevated processes cannot tamper
    /// with state, snapshots, or ACL backups read back by this elevated app.
    /// </summary>
    public static void EnsureDataRoot()
    {
        Directory.CreateDirectory(DataRoot);
        if (_hardenAttempted) return;
        _hardenAttempted = true;
        try
        {
            var info = new DirectoryInfo(DataRoot);
            var security = info.GetAccessControl();
            security.SetAccessRuleProtection(true, false);

            var sidAdmins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var sidSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var sidUsers = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

            security.AddAccessRule(new FileSystemAccessRule(
                sidAdmins, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                sidSystem, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                sidUsers, FileSystemRights.ReadAndExecute,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));

            info.SetAccessControl(security);
        }
        catch
        {
            // Non-elevated self-test or restricted FS: directory exists and remains usable.
        }
    }
}
