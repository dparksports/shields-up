using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ShieldsUp.Core.Helpers
{
    /// <summary>
    /// Persists firewall rule override state to JSON.
    /// Ported from DeviceMonitorCS.
    /// </summary>
    public class FirewallConfigManager
    {
        private static FirewallConfigManager _instance;
        public static FirewallConfigManager Instance => _instance ??= new FirewallConfigManager();

        private readonly string _configPath;
        private readonly object _lock = new();
        public Dictionary<string, string> RuleOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public FirewallConfigManager()
        {
            _configPath = AppPaths.FirewallConfigPath;
            Load();
        }

        public void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_configPath))
                    {
                        string json = File.ReadAllText(_configPath);
                        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (data != null)
                            RuleOverrides = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch { }
            }
        }

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    AppPaths.EnsureDataRoot();
                    string tmp = _configPath + ".tmp";
                    string json = JsonSerializer.Serialize(RuleOverrides, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(tmp, json);
                    if (File.Exists(_configPath))
                        File.Replace(tmp, _configPath, null);
                    else
                        File.Move(tmp, _configPath);
                }
                catch { }
            }
        }

        public void SetOverride(string ruleName, bool enabled)
        {
            string state = enabled ? "True" : "False";
            lock (_lock)
            {
                RuleOverrides[ruleName] = state;
            }
            Save();
        }

        public string GetOverride(string ruleName)
        {
            lock (_lock)
            {
                return RuleOverrides.TryGetValue(ruleName, out string val) ? val : null;
            }
        }
    }
}

