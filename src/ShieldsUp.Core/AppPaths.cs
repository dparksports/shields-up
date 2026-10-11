namespace ShieldsUp.Core;

/// <summary>
/// Single anchor for ShieldsUp's on-disk state. AutoCommand kept state in the app directory
/// and under ProgramData\AutoCommand; ShieldsUp namespaces everything under ProgramData\ShieldsUp
/// with the same file names and formats so the knowledge carries over.
/// </summary>
public static class AppPaths
{
    public const string AppName = "ShieldsUp";

    public static string DataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppName);

    public static string BloatwareConfigPath => Path.Combine(DataRoot, "bloatware_config.json");
    public static string FirewallConfigPath => Path.Combine(DataRoot, "firewall_config.json");
    public static string TelemetryStatePath => Path.Combine(DataRoot, "telemetry_state.json");
    public static string UpdateFreezeStatePath => Path.Combine(DataRoot, "update_freeze.json");
    public static string FreezeBackupsDir => Path.Combine(DataRoot, "freeze_backups");
    public static string EfiBaselinePath => Path.Combine(DataRoot, "efi_baseline.json");

    public static string CrashLogPath => Path.Combine(AppContext.BaseDirectory, "crash.log");

    public static void EnsureDataRoot()
    {
        Directory.CreateDirectory(DataRoot);
    }
}
