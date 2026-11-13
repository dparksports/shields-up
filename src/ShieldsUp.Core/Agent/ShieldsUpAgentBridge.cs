using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoCommand.Engines.Bridge;
using AutoCommand.Engines.Core;
using ShieldsUp.Core.Helpers;
using ShieldsUp.Core.Services;

namespace ShieldsUp.Core.Agent
{
    /// <summary>
    /// Shields Up's AI-access session over the shared AutoCommand III bridge
    /// engine (Engines.Bridge): loopback-only, bearer-token, POST-only JSON-RPC
    /// endpoint under Shields Up's own identity (own data root, own port scan
    /// range 47861+, own MCP id).
    ///
    /// Strictly READ-ONLY: firewall summary/listings, full attack-surface &amp;
    /// name-resolution posture, and startup/task inventory. Zero mutating tools.
    /// </summary>
    public static class ShieldsUpAgentBridge
    {
        private static readonly string[] VpnServices = { "RasMan", "IKEEXT", "PolicyAgent", "RemoteAccess" };
        private const string UsageDataTaskPath = @"\Microsoft\Windows\Flighting\FeatureConfig\UsageDataReceiver";

        private static AppIdentity Identity() => new(
            "Shields Up",
            AppPaths.DataRoot,
            "AC-BLOCK-IP ",
            "AC-BLOCK-APP ",
            null,
            47861,
            "shieldsup");

        public static void Wire() =>
            AutoCommand.Engines.Bridge.AgentBridgeService.Shared =
                new AutoCommand.Engines.Bridge.AgentBridgeService(Identity(), new SuAgentTools());

        public static AutoCommand.Engines.Bridge.AgentBridgeService Instance =>
            AutoCommand.Engines.Bridge.AgentBridgeService.Shared;

        public static (bool Ok, string Message) Start()
        {
            var t = Instance.Start(allowMutating: false);
            return (t.Item1, t.Item2);
        }

        public static void Stop() => Instance.Stop();

        public static List<string> RecentActivity(int count = 30) =>
            Instance.RecentActivity(count).ToList();

        // ── the tool surface (read-only) ───────────────────────────────────

        private sealed class SuAgentTools : IAgentTools
        {
            public IList<string> Names(bool allowMutating) =>
                new List<string>
                {
                    "get_firewall_summary",
                    "list_firewall_rules",
                    "get_shieldsup_status",
                    "get_startup_status"
                };

            public string ListTools(bool allowMutating) =>
                new JsonArray(
                    ToolDescriptor("get_firewall_summary",
                        "Windows Firewall rule counts by direction (total/enabled, inbound and outbound) " +
                        "plus how many legacy-named AutoCommand-family block rules exist. Read-only."),
                    ToolDescriptor("list_firewall_rules",
                        "Firewall rules as Shields Up's Firewall page shows them: display name, group, " +
                        "enabled, action, protocol, program, remote address. Args: direction 'in'|'out' " +
                        "(default out), contains (case-insensitive filter over name/group/program), " +
                        "max rows (default 100, cap 500). Read-only."),
                    ToolDescriptor("get_shieldsup_status",
                        "Live attack-surface, legacy name-resolution, and OS hardening posture: SSTP & VPN " +
                        "miniports, Wi-Fi Direct hosted network, KDNET/kernel debug, VPN services, " +
                        "UsageDataReceiver task, LLMNR, NetBIOS over TCP/IP, mDNS, Hotpatch Monitoring, " +
                        "IPv6, UAC strictness, and LSA PPL. Read-only."),
                    ToolDescriptor("get_startup_status",
                        "Persistent logon items (Run keys, Startup folders, plus snapshotted-disabled " +
                        "entries) and scheduled tasks classified into Third-party vs Windows inbox. Read-only."))
                .ToJsonString();

            public IAgentTool Find(string name, bool allowMutating) =>
                name == "get_firewall_summary" ? new SuTool(name, _ => Summary())
                : name == "list_firewall_rules" ? new SuTool(name, Rules)
                : name == "get_shieldsup_status" ? new SuTool(name, _ => ShieldsUpStatus())
                : name == "get_startup_status" ? new SuTool(name, _ => StartupStatus())
                : null;

            public string BuildMonitorSnapshot() => null;

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
            public string ConfirmDetail(string argsJson) => null;
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
                ["note"] = "Read-only view from Shields Up. Changes happen in the app, never from the assistant.",
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

        private static string ShieldsUpStatus()
        {
            var sstp = AttackSurfaceService.GetSstpStatus();
            string hosted = AttackSurfaceService.GetHostedNetworkStatus();
            bool debugOn = AttackSurfaceService.IsKernelDebugOn();
            var hotpatch = AttackSurfaceService.GetHotpatchStatus();
            var llmnr = NameResolutionDoors.GetLlmnrStatus();
            var nb = NameResolutionDoors.GetNetbiosStatus();
            var mdns = NameResolutionDoors.GetMdnsStatus();

            int vpnRunning = 0;
            foreach (var svc in VpnServices)
            {
                try
                {
                    using var sc = new ServiceController(svc);
                    if (sc.Status == ServiceControllerStatus.Running) vpnRunning++;
                }
                catch { }
            }

            bool usageTaskOn = TaskSchedulerService.Instance.IsTaskEnabled(UsageDataTaskPath);

            var doors = new JsonObject
            {
                ["sstp"] = new JsonObject { ["open"] = sstp.Active, ["status"] = sstp.StatusText, ["start_mode"] = sstp.StartMode },
                ["hosted_network"] = new JsonObject { ["open"] = hosted == "Started", ["status"] = hosted },
                ["kernel_debug"] = new JsonObject { ["open"] = debugOn },
                ["vpn_services"] = new JsonObject { ["open"] = vpnRunning > 0, ["running_count"] = vpnRunning },
                ["usage_data_task"] = new JsonObject { ["open"] = usageTaskOn },
                ["llmnr"] = new JsonObject { ["open"] = llmnr.Open, ["evidence"] = llmnr.Evidence },
                ["netbios"] = new JsonObject { ["open"] = nb.State != NameResolutionDoors.NbState.Closed && nb.Total > 0, ["state"] = nb.State.ToString(), ["disabled"] = nb.Disabled, ["total"] = nb.Total, ["evidence"] = nb.Evidence },
                ["mdns"] = new JsonObject { ["open"] = mdns.Open, ["evidence"] = mdns.Evidence },
                ["hotpatch_monitoring"] = new JsonObject { ["open"] = hotpatch.Present && hotpatch.Enabled, ["present"] = hotpatch.Present, ["enabled"] = hotpatch.Enabled },
            };

            return ToJson(new JsonObject
            {
                ["type"] = "shieldsup_status",
                ["doors"] = doors,
                ["hardening"] = new JsonObject
                {
                    ["ipv6_disabled"] = RegistryHelper.GetIpv6DisabledStatus(),
                    ["uac_max_strictness"] = RegistryHelper.GetUacStrictness(),
                    ["lsa_ppl_enabled"] = RegistryHelper.GetLsaProtectionStatus(),
                },
                ["miniport_count"] = AttackSurfaceService.ListWanMiniports().Count,
                ["wifi_direct_count"] = AttackSurfaceService.ListWifiDirectAdapters().Count,
                ["kdnet_count"] = AttackSurfaceService.ListKdnetAdapters().Count,
            });
        }

        private static string StartupStatus()
        {
            var logonActive = RegistryHelper.GetStartupItems();
            var logonDisabled = StartupSurfaceService.GetDisabledSnapshots();
            var allTasks = TaskSchedulerService.Instance.LoadTasksAsync().GetAwaiter().GetResult();

            var logonArr = new JsonArray();
            foreach (var item in logonActive.Concat(logonDisabled))
            {
                logonArr.Add(new JsonObject
                {
                    ["name"] = item.Name,
                    ["location"] = item.Location,
                    ["command"] = item.Value,
                    ["state"] = item.State,
                });
            }

            var thirdParty = allTasks.Where(t =>
                StartupSurfaceService.ClassifyTask(t.FullPath) == StartupSurfaceService.TaskGroup.ThirdParty).ToList();
            int inboxCount = allTasks.Count - thirdParty.Count;

            var tpArr = new JsonArray();
            foreach (var t in thirdParty.Take(200))
            {
                tpArr.Add(new JsonObject
                {
                    ["name"] = t.TaskName,
                    ["path"] = t.FullPath,
                    ["state"] = t.State,
                    ["action"] = t.Action,
                    ["user"] = t.User,
                });
            }

            return ToJson(new JsonObject
            {
                ["type"] = "startup_status",
                ["logon_active_count"] = logonActive.Count,
                ["logon_disabled_snapshot_count"] = logonDisabled.Count,
                ["logon_items"] = logonArr,
                ["third_party_task_count"] = thirdParty.Count,
                ["windows_inbox_task_count"] = inboxCount,
                ["third_party_tasks"] = tpArr,
            });
        }
    }
}
