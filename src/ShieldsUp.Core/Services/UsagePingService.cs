using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShieldsUp.Core.Services;

/// <summary>
/// Anonymous daily usage count — the only thing Shields Up ever sends.
/// Counts active installs so the project can show real usage numbers;
/// there is no other telemetry of any kind.
///
/// Consent model: OFF by default. A one-time dialog (max 3 offers) asks
/// the user to opt in; "no" is permanent and respected immediately.
/// State lives in HKCU\Software\ShieldsUp\UsagePing (Consent = ask|yes|no,
/// LastSentDay, Asks) — readable registry policy, kept under ShieldsUp's
/// own key so consent is per-app and never shared with AutoCommand.
///
/// Payload — the whole thing, nothing else, ever:
///   { "a": "shields-up",
///     "v": build date, compact (e.g. 261015),  "o": Windows build }
/// NO identifier of any kind is included — not even a random install id.
/// The app sends at most once per UTC day (LastSentDay below), so each
/// consenting install adds 1 to a daily aggregate counter on the server.
/// The server derives country at the network edge and never stores an IP
/// address; it keeps only daily totals (see telemetry/ in the repository
/// for the spec, the worker code, and the one-table schema). Deleting the
/// registry key withdraws consent.
/// </summary>
public static class UsagePingService
{
    public const string Endpoint = "https://autocommand-usage-ping.dpark-sports.workers.dev/v1/ping";
    private const string AppName = "shields-up";

    private const string RegKey = @"Software\ShieldsUp\UsagePing";

    /// <summary>Root of the public community graph page (served by the same
    /// worker — daily actives, countries, download + share links).</summary>
    public static string GraphPageUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Endpoint) || !Endpoint.StartsWith("https://")) return null;
            int cut = Endpoint.IndexOf('/', "https://".Length);
            return cut < 0 ? Endpoint : Endpoint.Substring(0, cut);
        }
    }

    /// <summary>Stable public download link — what the Share button copies.</summary>
    public const string ReleasesUrl = "https://github.com/dparksports/shields-up/releases/latest";

    /// <summary>Current consent state: "ask" (default), "yes" or "no".
    /// The UI layer decides when (and how often) to ask; this service only
    /// records the answer and acts on it.</summary>
    public static string Consent => ReadString("Consent", "ask");

    /// <summary>How many times the user has been asked so far.</summary>
    public static int AskCount => ReadInt("Asks", 0);

    public const int MaxAsks = 3;

    /// <summary>Records the consent answer and the fact that an ask happened.
    /// value: "yes" | "no" | "ask" (dismissed — counts as an ask, stays "ask").</summary>
    public static void SetConsent(string value, bool counted = true)
    {
        if (value != "yes" && value != "no" && value != "ask") return;
        WriteString("Consent", value);
        if (counted) WriteInt("Asks", ReadInt("Asks", 0) + 1);
    }

    /// <summary>Sends today's ping unless one was already sent (UTC day).
    /// The daily check is client-side — the server never sees anything
    /// that could identify this install, so "once per day" is all we
    /// can and need to enforce.</summary>
    public static async Task SendDailyPingAsync()
    {
        try
        {
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (ReadString("LastSentDay", "") == today) return;

            var payload = JsonSerializer.Serialize(new
            {
                a = AppName,
                v = CompactBuildDate(),
                o = Environment.OSVersion.Version.ToString()
            });

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(Endpoint, content);
            if (response.IsSuccessStatusCode)
                WriteString("LastSentDay", today);
            // On failure: no state change — retried on a later launch, never spammed.
        }
        catch { /* offline / DNS blocked / endpoint down — silently fine */ }
    }

    /// <summary>The count tracks builds by day, so the ping sends the
    /// version's date in compact form (2026.10.15 → 261015) rather than
    /// the full app version — publish.ps1 stamps the build date already.
    /// Anything unparseable passes through unchanged.</summary>
    private static string CompactBuildDate()
    {
        string v = UpdateCheckService.CleanVersion(UpdateCheckService.GetCurrentVersion());
        var m = Regex.Match(v, @"^(\d{4})\.(\d{1,2})\.(\d{1,2})");
        if (!m.Success) return v;
        string yy = m.Groups[1].Value.Length == 4 ? m.Groups[1].Value.Substring(2) : m.Groups[1].Value;
        return yy + m.Groups[2].Value.PadLeft(2, '0') + m.Groups[3].Value.PadLeft(2, '0');
    }

    // ── HKCU persistence (ShieldsUp's own key — consent is per-app) ─────

    private static Microsoft.Win32.RegistryKey OpenKey(bool writable = true)
    {
        using var root = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\ShieldsUp", writable);
        return root?.CreateSubKey("UsagePing", writable: writable);
    }

    private static string ReadString(string name, string fallback)
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegKey);
            return k?.GetValue(name) as string ?? fallback;
        }
        catch { return fallback; }
    }

    private static int ReadInt(string name, int fallback)
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegKey);
            return k?.GetValue(name) is int i ? i : fallback;
        }
        catch { return fallback; }
    }

    private static void WriteString(string name, string value)
    {
        try
        {
            using var k = OpenKey();
            k?.SetValue(name, value, Microsoft.Win32.RegistryValueKind.String);
        }
        catch { /* registry unavailable — nothing to persist */ }
    }

    private static void WriteInt(string name, int value)
    {
        try
        {
            using var k = OpenKey();
            k?.SetValue(name, value, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch { /* registry unavailable — nothing to persist */ }
    }
}
