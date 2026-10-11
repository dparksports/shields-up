using System.Text;

namespace ShieldsUp.Core.Services;

/// <summary>
/// Append-only application log under %ProgramData%\ShieldsUp\logs. Two files:
///   app.log  — session events, navigation, and any caught exception
///   *.dmp    — native crash dumps captured by Windows Error Reporting
///              (registered for this exe by EnsureCrashDumps; native crashes
///              like XAML/Composition failures never reach managed handlers)
/// </summary>
public static class AppLogger
{
    private static readonly object Gate = new();
    private static readonly string LogsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ShieldsUp", "logs");

    public static string LogFilePath => Path.Combine(LogsDir, "app.log");
    public static string DumpsDir => Path.Combine(LogsDir, "dumps");

    public static void Log(string category, string message)
    {
        Write($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}");
    }

    public static void LogException(string context, Exception ex)
    {
        Write($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [EXCEPTION] {context}\r\n{ex}\r\n");
    }

    public static void SessionHeader()
    {
        var v = typeof(AppLogger).Assembly.GetName().Version;
        Write(
            $"\r\n════════ Shields Up session {DateTime.Now:yyyy-MM-dd HH:mm:ss} ════════\r\n" +
            $"version={v}  os={Environment.OSVersion.VersionString}  64bit={Environment.Is64BitProcess}\r\n" +
            $"user={Environment.UserName}  log={LogFilePath}\r\n");
    }

    private static void Write(string line)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogsDir);
                // Keep one generation: rotate past 2 MB
                if (File.Exists(LogFilePath) && new FileInfo(LogFilePath).Length > 2_000_000)
                {
                    string old = LogFilePath + ".old";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(LogFilePath, old);
                }
                File.AppendAllText(LogFilePath, line + "\r\n", Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }

    /// <summary>
    /// Registers Windows Error Reporting local dumps for this executable so
    /// NATIVE crashes (which bypass every managed handler) still leave a .dmp
    /// in %ProgramData%\ShieldsUp\logs\dumps for post-mortem debugging.
    /// No-op when the registry is unavailable.
    /// </summary>
    public static void EnsureCrashDumps()
    {
        try
        {
            Directory.CreateDirectory(DumpsDir);
            const string baseKey = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps";
            string exe = Path.GetFileName(Environment.ProcessPath);
            using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey($@"{baseKey}\{exe}");
            key.SetValue("DumpFolder", DumpsDir);
            key.SetValue("DumpType", 2, Microsoft.Win32.RegistryValueKind.DWord); // full dump
            key.SetValue("DumpCount", 5, Microsoft.Win32.RegistryValueKind.DWord);
            Log("logger", $"WER local dumps registered: {DumpsDir}");
        }
        catch (Exception ex)
        {
            Log("logger", $"WER local dump registration failed: {ex.Message}");
        }
    }
}
