// Shields Up agent-bridge self-test: wires the shared engine under the
// Shields Up identity, starts a read-only session, then verifies over the
// wire (raw HTTP/1.1, one request per connection - what an MCP client or
// curl does):
//   1. session.json appears in %LOCALAPPDATA%\ShieldsUp\agent\ with this
//      process's pid and NO mutating flag
//   2. handshake answers as the shieldsup MCP server
//   3. tools/list exposes exactly the two read-only tools
//   4. tools/call get_firewall_summary returns real counts (COM read)
//   5. a mutating-looking tool name is unknown on this surface
//   6. Stop removes the session file
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using ShieldsUp.Core.Agent;

int failures = 0;
void Check(string name, bool pass, string evidence = "")
{
    Console.WriteLine($"{(pass ? "PASS" : "FAIL")}  {name}{(pass || evidence.Length == 0 ? "" : "  — " + evidence)}");
    if (!pass) failures++;
}

ShieldsUpAgentBridge.Wire();
var (ok, message) = ShieldsUpAgentBridge.Start();
Check("1a session starts", ok, message);

string sessionPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "ShieldsUp", "agent", "session.json");
var session = JsonNode.Parse(File.Exists(sessionPath) ? File.ReadAllText(sessionPath) : "") as JsonObject;
Check("1b session.json carries this pid, port 4786x, mutating off",
    session?["pid"]?.GetValue<int>() == Environment.ProcessId &&
    session["port"].GetValue<int>() >= 47861 && session["port"].GetValue<int>() < 47881 &&
    session["mutatingAllowed"].GetValue<bool>() == false,
    File.Exists(sessionPath) ? "port=" + session["port"] : "file missing");
int port = session["port"].GetValue<int>();
string token = session["token"].GetValue<string>();

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
Check("3a tools/list = the two read-only tools",
    names != null && names.Count == 2 && names[0] == "get_firewall_summary" && names[1] == "list_firewall_rules",
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

var noMut = JsonNode.Parse(Post("{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/call\"," +
    "\"params\":{\"name\":\"apply_profile\",\"arguments\":{}}}")) as JsonObject;
string noMutText = ((noMut?["result"]?["content"] as JsonArray)?[0]?["text"]?.GetValue<string>() ?? "");
Check("5a mutating-looking tool is unknown on this surface",
    noMut?["result"]?["isError"]?.GetValue<bool>() == true && noMutText.Contains("Unknown tool"),
    noMutText.Length > 60 ? noMutText.Substring(0, 60) : noMutText);

ShieldsUpAgentBridge.Stop();
Check("6a Stop removes the session file", !File.Exists(sessionPath));

Console.WriteLine(failures == 0 ? "SHIELDS UP BRIDGE SELF-TEST PASSED" : $"SELF-TEST FAILED ({failures} check(s))");
return failures == 0 ? 0 : 1;
