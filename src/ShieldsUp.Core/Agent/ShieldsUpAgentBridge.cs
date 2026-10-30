using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoCommand.Engines.Bridge;
using AutoCommand.Engines.Core;

namespace ShieldsUp.Core.Agent
{
    /// <summary>
    /// Shields Up's AI-access session over the shared AutoCommand III bridge
    /// engine (Engines.Bridge): the same loopback-only, bearer-token,
    /// POST-only JSON-RPC endpoint AutoCommand hosts, under Shields Up's own
    /// identity (own data root, own port scan range 47861+, own MCP id).
    ///
    /// This first surface is deliberately READ-ONLY: two firewall-state
    /// tools, no mutating tools at all. The engine's session model already
    /// encodes that stance (Start(allowMutating: false)) — a mutating
    /// surface would need the per-call confirmation dialog seam first, the
    /// same way AutoCommand's Be My Guest tab does it.
    ///
    /// session.json lands in %LOCALAPPDATA%\ShieldsUp\agent\ — clients read
    /// {port, token, pid} from it, never a hardcoded port.
    /// </summary>
    public static class ShieldsUpAgentBridge
    {
        /// <summary>Shields Up's engine identity: same AC-BLOCK rule family
        /// as AutoCommand (this app manages those rules too), own session
        /// data root, own bridge port range so both apps can host a session
        /// side by side.</summary>
        private static AppIdentity Identity() => new(
            "Shields Up",
            AppPaths.DataRoot,
            "AC-BLOCK-IP ",
            "AC-BLOCK-APP ",
            null,
            47861,
            "shieldsup");

        /// <summary>Call once at app startup, before any session can start:
        /// installs the engine + this app's tool surface.</summary>
        public static void Wire() =>
            AutoCommand.Engines.Bridge.AgentBridgeService.Shared =
                new AutoCommand.Engines.Bridge.AgentBridgeService(Identity(), new SuAgentTools());

        public static AutoCommand.Engines.Bridge.AgentBridgeService Instance =>
            AutoCommand.Engines.Bridge.AgentBridgeService.Shared;

        /// <summary>Starts a READ-ONLY session (the only kind this surface
        /// offers). Returns (ok, message) for the UI to show.</summary>
        public static (bool Ok, string Message) Start()
        {
            // F# engine tuples surface as System.Tuple — unpack for callers
            var t = Instance.Start(allowMutating: false);
            return (t.Item1, t.Item2);
        }

        public static void Stop() => Instance.Stop();

        public static List<string> RecentActivity(int count = 30) =>
            Instance.RecentActivity(count).ToList();

        // ── the tool surface ───────────────────────────────────────────────

        private sealed class SuAgentTools : IAgentTools
        {
            public IList<string> Names(bool allowMutating) =>
                new List<string> { "get_firewall_summary", "list_firewall_rules" };

            public string ListTools(bool allowMutating) =>
                new JsonArray(
                    ToolDescriptor("get_firewall_summary",
                        "Windows Firewall rule counts by direction (total/enabled, inbound and outbound) " +
                        "plus how many legacy-named AutoCommand-family block rules exist. The fast " +
                        "orientation call before list_firewall_rules."),
                    ToolDescriptor("list_firewall_rules",
                        "Firewall rules as Shields Up's Firewall page shows them: display name, group, " +
                        "enabled, action, protocol, program, remote address. Args: direction 'in'|'out' " +
                        "(default out), contains (case-insensitive filter over name/group/program), " +
                        "max rows (default 100, cap 500). Read-only."))
                .ToJsonString();

            public IAgentTool Find(string name, bool allowMutating) =>
                name == "get_firewall_summary" ? new SuTool(name, _ => Summary())
                : name == "list_firewall_rules" ? new SuTool(name, Rules)
                : null;

            public string BuildMonitorSnapshot() => null; // no periodic feed on this surface yet

            private static JsonObject ToolDescriptor(string name, string description) => new()
            {
                ["name"] = name,
                ["description"] = description,
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject(),
                },
            };
        }

        private sealed class SuTool : IAgentTool
        {
            private readonly string _name;
            private readonly Func<string, string> _invoke;
            public SuTool(string name, Func<string, string> invoke) { _name = name; _invoke = invoke; }
            public string Name => _name;
            public bool Mutating => false;
            public string ConfirmDetail(string argsJson) => null; // read-only: nothing to confirm
            public string Invoke(string argsJson) => _invoke(argsJson);
        }

        private static string ToJson(JsonObject o) =>
            o.ToJsonString(new JsonSerializerOptions { WriteIndented = false });

        private static string Summary()
        {
            var t = FirewallService.Instance.GetSummaryAsync().GetAwaiter().GetResult();
            int legacy = FirewallService.Instance.CountLegacyBlocksAsync().GetAwaiter().GetResult();
            return ToJson(new JsonObject
            {
                ["type"] = "firewall_summary",
                ["inbound_total"] = t.Item1,
                ["inbound_enabled"] = t.Item2,
                ["outbound_total"] = t.Item3,
                ["outbound_enabled"] = t.Item4,
                ["legacy_block_rules"] = legacy,
                ["note"] = "Read-only view from Shields Up. list_firewall_rules shows individual rules; " +
                           "changes to rules happen in the app, never from the assistant.",
            });
        }

        private static string Rules(string argsJson)
        {
            JsonObject a = null;
            try { a = JsonNode.Parse(argsJson ?? "{}") as JsonObject; } catch { }
            string direction = "out";
            string contains = null;
            int max = 100;
            try
            {
                if (a?["direction"]?.GetValue<string>() is string d &&
                    d.StartsWith("in", StringComparison.OrdinalIgnoreCase))
                    direction = "in";
                contains = a?["contains"]?.GetValue<string>();
                if (a?["max"] is JsonNode m) max = Math.Clamp(m.GetValue<int>(), 1, 500);
            }
            catch { }

            var rules = FirewallService.Instance.LoadRulesAsync(direction == "in" ? 1 : 2)
                .GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(contains))
                rules = rules.Where(r =>
                    (r.DisplayName?.IndexOf(contains, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (r.DisplayGroup?.IndexOf(contains, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (r.Program?.IndexOf(contains, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0).ToList();

            var arr = new JsonArray();
            foreach (var r in rules.Take(max))
                arr.Add(new JsonObject
                {
                    ["name"] = r.Name,
                    ["display"] = r.DisplayName,
                    ["group"] = r.DisplayGroup,
                    ["enabled"] = r.Enabled,
                    ["action"] = r.Action,
                    ["protocol"] = r.Protocol,
                    ["program"] = r.Program,
                    ["remote"] = r.RemoteAddress,
                });

            return ToJson(new JsonObject
            {
                ["type"] = "firewall_rules",
                ["direction"] = direction,
                ["rows"] = arr,
                ["row_count"] = arr.Count,
                ["total_matching"] = rules.Count,
            });
        }
    }
}
