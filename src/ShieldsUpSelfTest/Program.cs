// Shields Up self-test suite:
//   Part A (live loopback MCP bridge):
//     1. session.json appears in %ProgramData%\ShieldsUp\agent\ with this pid and mutatingAllowed=false
//     2. initialize handshake answers as shieldsup
//     3. tools/list exposes the 4 read-only tools (firewall summary/rules + shieldsup_status + startup_status)
//     4. tools/call exercises all 4 read-only tools over HTTP/1.1 JSON-RPC
//     5. mutating tool names are rejected as unknown
//     6. Stop removes session.json
//   Part B (security, reliability & hardening unit invariants):
//     7. AttackSurfaceService.ParseKernelDebugOn (regression guard against recoveryenabled Yes + debug No)
//     8. ScheduledTaskGuard.Classify (PT72H / battery kill defaults vs PT0S healthy)
//     9. NameResolutionDoors pure state transitions (LLMNR, NetBIOS, mDNS)
//    10. StartupSurfaceService.ClassifyTask & KindOfLocation
//    11. ServiceHelper.ResolveRestoreStartType (Auto vs Manual/null) & ProcessRunner.ResolveTool System32 pinning
//    12. TelemetryRulesService (11 rules incl. 3 Search/Bing feature toggles + registry/JSON write allow-list guards)
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using ShieldsUp.Core.Agent;
using ShieldsUp.Core.Helpers;
using ShieldsUp.Core.Services;

int failures = 0;
void Check(string name, bool pass, string evidence = "")
{
    Console.WriteLine($"{(pass ? "PASS" : "FAIL")}  {name}{(pass || evidence.Length == 0 ? "" : "  — " + evidence)}");
    if (!pass) failures++;
}

// ── Part A: Live loopback read-only MCP bridge ─────────────────────────────

ShieldsUpAgentBridge.Wire();
var (ok, message) = ShieldsUpAgentBridge.Start();
Check("1a session starts", ok, message);

string sessionPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "ShieldsUp", "agent", "session.json");
var session = JsonNode.Parse(File.Exists(sessionPath) ? File.ReadAllText(sessionPath) : "{}") as JsonObject;
Check("1b session.json carries this pid, port 4786x, mutating off",
    session?["pid"]?.GetValue<int>() == Environment.ProcessId &&
    session["port"]?.GetValue<int>() >= 47861 && session["port"]?.GetValue<int>() < 47881 &&
    session["mutatingAllowed"]?.GetValue<bool>() == false,
    File.Exists(sessionPath) ? "port=" + session?["port"] : "file missing");
int port = session?["port"]?.GetValue<int>() ?? 47861;
string token = session?["token"]?.GetValue<string>() ?? "";

string Post(string json, string auth = null)
{
    using var c = new TcpClient("127.0.0.1", port);
    using var s = c.GetStream();
    string req = "POST / HTTP/1.1\r\n" +
                 $"Host: 127.0.0.1:{port}\r\n" +
                 $"Authorization: {auth ?? $"Bearer {token}"}\r\n" +
                 $"Content-Length: {Encoding.UTF8.GetBytes(json).Length}\r\n" +
                 "Connection: close\r\n\r\n" + json;
    byte[] bytes = Encoding.UTF8.GetBytes(req);
    s.Write(bytes, 0, bytes.Length);
    s.Flush();
    var buf = new byte[65536];
    using var ms = new MemoryStream();
    int n;
    try { while ((n = s.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n); } catch { }
    string raw = Encoding.UTF8.GetString(ms.ToArray());
    int split = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
    return split >= 0 ? raw.Substring(split + 4) : "";
}

var init = JsonNode.Parse(Post("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"," +
    "\"params\":{\"protocolVersion\":\"2025-06-18\",\"clientInfo\":{\"name\":\"su-selftest\"}}}")) as JsonObject;
Check("2a handshake answers as shieldsup",
    init?["result"]?["serverInfo"]?["name"]?.GetValue<string>() == "shieldsup");

var list = JsonNode.Parse(Post("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}")) as JsonObject;
var names = (list?["result"]?["tools"] as JsonArray)?.Select(t => t["name"].GetValue<string>()).OrderBy(x => x).ToList();
var expectedTools = new[] { "get_firewall_summary", "get_shieldsup_status", "get_startup_status", "list_firewall_rules" };
Check("3a tools/list exposes the 4 read-only tools",
    names != null && names.SequenceEqual(expectedTools),
    names != null ? string.Join(",", names) : "no tools");

var call = JsonNode.Parse(Post("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\"," +
    "\"params\":{\"name\":\"get_firewall_summary\",\"arguments\":{}}}")) as JsonObject;
var structured = call?["result"]?["structuredContent"];
int total = (structured?["inbound_total"]?.GetValue<int>() ?? -1) + (structured?["outbound_total"]?.GetValue<int>() ?? -1);
Check("4a get_firewall_summary returns real counts via COM",
    call?["result"]?["isError"]?.GetValue<bool>() == false && total > 0,
    "in+out=" + total);

var rules = JsonNode.Parse(Post("{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\"," +
    "\"params\":{\"name\":\"list_firewall_rules\",\"arguments\":{\"direction\":\"out\",\"max\":5}}}")) as JsonObject;
var rows = rules?["result"]?["structuredContent"]?["rows"] as JsonArray;
Check("4b list_firewall_rules returns rows",
    rows != null && rows.Count > 0 && rows.Count <= 5, "rows=" + (rows?.Count ?? 0));

var posture = JsonNode.Parse(Post("{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/call\"," +
    "\"params\":{\"name\":\"get_shieldsup_status\",\"arguments\":{}}}")) as JsonObject;
var doors = posture?["result"]?["structuredContent"]?["doors"] as JsonObject;
Check("4c get_shieldsup_status returns all 9 doors + hardening block",
    posture?["result"]?["isError"]?.GetValue<bool>() == false &&
    doors != null && doors.Count == 9 &&
    doors["llmnr"] != null && doors["netbios"] != null && doors["mdns"] != null && doors["hotpatch_monitoring"] != null,
    doors != null ? $"doors={doors.Count}" : "missing doors");

var startup = JsonNode.Parse(Post("{\"jsonrpc\":\"2.0\",\"id\":6,\"method\":\"tools/call\"," +
    "\"params\":{\"name\":\"get_startup_status\",\"arguments\":{}}}")) as JsonObject;
var startupBody = startup?["result"]?["structuredContent"] as JsonObject;
Check("4d get_startup_status returns logon + classified task counts",
    startup?["result"]?["isError"]?.GetValue<bool>() == false &&
    (startupBody?["windows_inbox_task_count"]?.GetValue<int>() ?? -1) >= 0 &&
    (startupBody?["third_party_task_count"]?.GetValue<int>() ?? -1) >= 0,
    $"inbox={startupBody?["windows_inbox_task_count"]}, thirdParty={startupBody?["third_party_task_count"]}");

var noMut = JsonNode.Parse(Post("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/call\"," +
    "\"params\":{\"name\":\"apply_profile\",\"arguments\":{}}}")) as JsonObject;
string noMutText = ((noMut?["result"]?["content"] as JsonArray)?[0]?["text"]?.GetValue<string>() ?? "");
Check("5a mutating-looking tool is unknown on this surface",
    noMut?["result"]?["isError"]?.GetValue<bool>() == true && noMutText.Contains("Unknown tool"),
    noMutText.Length > 60 ? noMutText.Substring(0, 60) : noMutText);

ShieldsUpAgentBridge.Stop();
Check("6a Stop removes the session file", !File.Exists(sessionPath));

// ── Part B: Security, reliability & hardening unit checks ──────────────────

string normalBcd = """
Windows Boot Loader
-------------------
identifier              {current}
recoveryenabled         Yes
debug                   No
hypervisordebug         Off
""";
string enabledBcd = """
Windows Boot Loader
-------------------
identifier              {current}
recoveryenabled         No
debug                   Yes
""";
Check("7a ParseKernelDebugOn rejects 'recoveryenabled Yes + debug No' false positive",
    !AttackSurfaceService.ParseKernelDebugOn(normalBcd) &&
    AttackSurfaceService.ParseKernelDebugOn(enabledBcd));

var h72 = ScheduledTaskGuard.Classify(true, "PT72H", true, true);
var hBatt = ScheduledTaskGuard.Classify(true, "PT0S", false, true);
var hGood = ScheduledTaskGuard.Classify(true, "PT0S", false, false);
Check("8a ScheduledTaskGuard flags PT72H and battery kill defaults, approves PT0S",
    !h72.Healthy && !hBatt.Healthy && hGood.Healthy);

Check("9a NameResolutionDoors pure state transitions",
    NameResolutionDoors.LlmnrOpenFromValue(null) &&
    NameResolutionDoors.LlmnrOpenFromValue(1) &&
    !NameResolutionDoors.LlmnrOpenFromValue(0) &&
    NameResolutionDoors.ComputeNetbiosState(3, 0) == NameResolutionDoors.NbState.None &&
    NameResolutionDoors.ComputeNetbiosState(3, 2) == NameResolutionDoors.NbState.Partial &&
    NameResolutionDoors.ComputeNetbiosState(3, 3) == NameResolutionDoors.NbState.Closed &&
    !NameResolutionDoors.MdnsClosedFromRuleCount(0) &&
    NameResolutionDoors.MdnsClosedFromRuleCount(1));

Check("10a StartupSurfaceService classifies inbox vs third-party tasks & logon locations",
    StartupSurfaceService.ClassifyTask(@"\Microsoft\Windows\Defrag\ScheduledDefrag") == StartupSurfaceService.TaskGroup.Inbox &&
    StartupSurfaceService.ClassifyTask(@"\Microsoft\EdgeUpdate\MicrosoftEdgeUpdateTaskMachineCore") == StartupSurfaceService.TaskGroup.ThirdParty &&
    StartupSurfaceService.ClassifyTask(@"\ShieldsUpStartupTask") == StartupSurfaceService.TaskGroup.ThirdParty &&
    StartupSurfaceService.KindOfLocation("HKCU Run") == StartupSurfaceService.LogonKind.Registry &&
    StartupSurfaceService.KindOfLocation("User Startup") == StartupSurfaceService.LogonKind.Folder);

Check("11a ServiceHelper restores Auto vs Manual/null accurately",
    ServiceHelper.ResolveRestoreStartType("Auto") == ServiceHelper.SERVICE_AUTO_START &&
    ServiceHelper.ResolveRestoreStartType("Manual") == ServiceHelper.SERVICE_DEMAND_START &&
    ServiceHelper.ResolveRestoreStartType(null) == ServiceHelper.SERVICE_DEMAND_START);

string sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
Check("11b ProcessRunner.ResolveTool pins unqualified system binaries to System32",
    ProcessRunner.ResolveTool("netsh").StartsWith(sys32, StringComparison.OrdinalIgnoreCase) &&
    ProcessRunner.ResolveTool("bcdedit").StartsWith(sys32, StringComparison.OrdinalIgnoreCase) &&
    ProcessRunner.ResolveTool("powershell").StartsWith(sys32, StringComparison.OrdinalIgnoreCase));

Check("11c AppManagerService includes MicrosoftStickyNotes & rejects non-existent OneDrive binary",
    AppManagerService.BloatwarePatterns.Contains("MicrosoftStickyNotes") &&
    !AppManagerService.IsTrustedOneDriveBinary(@"C:\nonexistent\OneDriveSetup.exe", "OneDriveSetup.exe"));

var telemetrySvc = new TelemetryRulesService();
var allRules = telemetrySvc.Rules;
var regRules = allRules.Where(r => r.OptOut?.Type == "registry").ToList();
Check("12a TelemetryRulesService loads all 11 rules including 3 Search/Bing feature toggles",
    allRules.Count == 11 && allRules.Count(r => r.IsFeatureToggle) == 3,
    $"count={allRules.Count}, feature={allRules.Count(r => r.IsFeatureToggle)}");

Check("12b TelemetryRulesService registry & JSON write allow-lists enforce confinement",
    regRules.All(r => TelemetryRulesService.IsRegistryWriteAllowed(r.OptOut.RegKey)) &&
    !TelemetryRulesService.IsRegistryWriteAllowed(@"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters") &&
    !TelemetryRulesService.IsRegistryWriteAllowed(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Run") &&
    TelemetryRulesService.IsJsonPathAllowed(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code", "User", "settings.json")) &&
    !TelemetryRulesService.IsJsonPathAllowed(Path.Combine(sys32, "drivers", "etc", "hosts")));

Console.WriteLine(failures == 0 ? "SHIELDS UP SELF-TEST PASSED" : $"SELF-TEST FAILED ({failures} check(s))");
return failures == 0 ? 0 : 1;
