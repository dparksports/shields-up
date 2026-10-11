namespace ShieldsUp.Helpers;

/// <summary>Append-only crash log next to the executable, same convention as AutoCommand.</summary>
internal static class CrashLog
{
    private static readonly object Gate = new();

    internal static void Write(string context, Exception ex)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "crash.log");
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex}\r\n\r\n";
            lock (Gate) File.AppendAllText(path, line);
        }
        catch
        {
            // Never let the crash logger crash.
        }
    }
}
