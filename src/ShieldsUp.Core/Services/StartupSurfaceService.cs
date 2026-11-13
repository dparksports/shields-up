using System.Text.Json;
using System.Text.Json.Nodes;
using ShieldsUp.Core.Models;

namespace ShieldsUp.Core.Services;

/// <summary>
/// The startup/persistence surface behind the Startup and Scheduled Tasks pages:
/// logon items (Run keys, Startup folders) and scheduled tasks, with reversible
/// snapshot-based disable/restore for registry Run entries and location-based
/// Inbox vs Third-party task classification.
/// </summary>
public static class StartupSurfaceService
{
    public enum TaskGroup { Inbox, ThirdParty }

    /// <summary>Pure: full task path → review group (self-tested).</summary>
    public static TaskGroup ClassifyTask(string taskPath) =>
        (taskPath ?? "").Replace('/', '\\').StartsWith(@"\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase)
            ? TaskGroup.Inbox
            : TaskGroup.ThirdParty;

    public enum LogonKind { Registry, Folder }

    /// <summary>Pure: location label → how its items toggle (self-tested).</summary>
    public static LogonKind KindOfLocation(string location) =>
        (location ?? "").Contains("Startup", StringComparison.OrdinalIgnoreCase)
            ? LogonKind.Folder
            : LogonKind.Registry;

    // ── registry logon toggles (snapshot → remove / restore) ────────────

    private static string StatePath => AppPaths.StartupStatePath;

    private static JsonObject LoadState()
    {
        try
        {
            if (File.Exists(StatePath))
                return JsonNode.Parse(File.ReadAllText(StatePath)) as JsonObject ?? new JsonObject();
        }
        catch { }
        return new JsonObject();
    }

    private static void SaveState(JsonObject state)
    {
        AppPaths.EnsureDataRoot();
        string tmp = StatePath + ".tmp";
        File.WriteAllText(tmp, state.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(StatePath))
            File.Replace(tmp, StatePath, null);
        else
            File.Move(tmp, StatePath);
    }

    private static (Microsoft.Win32.RegistryKey Hive, string Path) HiveOf(string location)
    {
        if ((location ?? "").StartsWith("HKCU", StringComparison.OrdinalIgnoreCase))
            return (Microsoft.Win32.Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run");
        return (Microsoft.Win32.Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
    }

    public static bool HasLogonSnapshot(string location, string name)
    {
        var state = LoadState();
        return state["logon"]?[location]?[name] is JsonObject;
    }

    /// <summary>Returns snapshotted (disabled) registry Run items so the UI can display and restore them.</summary>
    public static List<StartupItem> GetDisabledSnapshots()
    {
        var result = new List<StartupItem>();
        try
        {
            var state = LoadState();
            if (state["logon"] is not JsonObject logonObj) return result;
            foreach (var (location, entriesNode) in logonObj)
            {
                if (entriesNode is not JsonObject entries) continue;
                foreach (var (name, itemNode) in entries)
                {
                    if (itemNode is not JsonObject itemObj) continue;
                    result.Add(new StartupItem
                    {
                        Name = name,
                        Location = location,
                        Value = itemObj["value"]?.GetValue<string>() ?? "",
                        TargetPath = itemObj["path"]?.GetValue<string>() ?? @"Software\Microsoft\Windows\CurrentVersion\Run",
                        State = "Disabled (snapshotted)",
                        IsDisabledSnapshot = true,
                    });
                }
            }
        }
        catch { }
        return result;
    }

    /// <summary>Snapshots the value, then removes it from the Run key. Empty list = clean.</summary>
    public static List<string> DisableRegistryLogon(string location, string name)
    {
        var failures = new List<string>();
        try
        {
            var (hive, path) = HiveOf(location);
            using (var key = hive.OpenSubKey(path, writable: true))
            {
                if (key?.GetValue(name) is not string value)
                {
                    failures.Add($"no '{name}' in {location}");
                    return failures;
                }
                var state = LoadState();
                var logon = state["logon"] as JsonObject ?? new JsonObject();
                state.Remove("logon");
                logon[location] ??= new JsonObject();
                logon[location][name] = new JsonObject
                {
                    ["value"] = value,
                    ["path"] = path,
                    ["disabledUtc"] = DateTime.UtcNow.ToString("o")
                };
                state["logon"] = logon;
                SaveState(state);
                key.DeleteValue(name);
            }
        }
        catch (Exception ex) { failures.Add(ex.Message); }
        return failures;
    }

    /// <summary>Restores the snapshotted value exactly; refuses honestly when no snapshot exists.</summary>
    public static List<string> EnableRegistryLogon(string location, string name)
    {
        var failures = new List<string>();
        try
        {
            var state = LoadState();
            var snap = state["logon"]?[location]?[name] as JsonObject;
            if (snap == null)
            {
                failures.Add("no snapshot — this item was not disabled by Shields Up");
                return failures;
            }
            var (hive, _) = HiveOf(location);
            using (var key = hive.CreateSubKey(snap["path"]?.GetValue<string>() ?? @"Software\Microsoft\Windows\CurrentVersion\Run"))
                key.SetValue(name, snap["value"]?.GetValue<string>() ?? "", Microsoft.Win32.RegistryValueKind.String);
            (state["logon"]?[location] as JsonObject)?.Remove(name);
            SaveState(state);
        }
        catch (Exception ex) { failures.Add(ex.Message); }
        return failures;
    }
}
