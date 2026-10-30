# Shields Up on the AutoCommand III engines — migration notes (2026-10-30)

Shields Up now runs its firewall stack and AI-access bridge on the shared
AutoCommand III core libraries instead of the pre-II AutoCommand services it
had vendored in `ShieldsUp.Core` (the lineage its NOTICE already credited).

## How the engines are consumed

`external/autocommand-iii` is a **git submodule** pinned at `997ebae`
(v2026.10.30.9-3). Build with `git clone --recursive`; update the pin
deliberately — engine fixes ride with it:

    git submodule update --remote external/autocommand-iii
    # test, then commit the new gitlink

Referenced projects (each carries its own dependency closure):

- `core/AutoCommand.Contain.Core` — the app-facing C# firewall facade over
  the F# engine, plus the shared telemetry/sinkhole/freeze services
- `Engines/Engines.Bridge` — the AI-access bridge engine (loopback,
  bearer-token, POST-only JSON-RPC)

Shields Up is AutoCommand-family: it manages the same **AC-BLOCK** rule
family, so the engine keeps the `autocommand` naming (the facade constructs
it with `Naming.autocommand()`). The AI bridge runs under Shields Up's OWN
identity: data root `%ProgramData%\ShieldsUp`, port scan base **47861**
(AutoCommand scans from 47821 — both apps can host a session side by side),
MCP id `shieldsup`.

## What swapped

| Was vendored (deleted) | Now |
|---|---|
| `Services/FirewallService.cs` (934 lines) | `AutoCommand.Contain.Core.FirewallService` facade over `Engines.Firewall` (F#) |
| `Services/IpClassifier.cs` | gone with its only consumer; shared `Engines.Core/IpClassification.fs` covers the class if needed again |
| `Models/DataModels.cs` `FirewallRuleItem` | `AutoCommand.Engines.Firewall.RuleInfo` via a csproj using-alias — same fields, same INotifyPropertyChanged enable-toggle |
| (none) | `Engines.Bridge` session hosting — Settings → AI access |

`Services/FirewallProfileService.cs` was REWRITTEN, not deleted: Shields
Up's user-facing preset names (Strict Public / Home / Gaming / Office /
Shield Up) are this app's language, so the preset mapping stays here while
every group toggle underneath goes through the shared engine.

### The Shield Up preset is deliberately different from the engine's

The engine's `ShieldsUpMdns` preset whitelists only `mDNS` + `Core
Networking` when it disables every grouped rule. The 2026-10-30 field report
on the AutoCommand host caught what that whitelist misses: forwarded
WSL2/Hyper-V VM egress dies with only those two groups surviving, silently,
below the per-app rule model. Shields Up's local `ApplyShieldUp` therefore
ALSO spares the VM infrastructure groups (`icsvc`, the WSL store-package
group, `Hyper-V`, `Virtual Machine`, `HNS`, `vmcompute`, `wslservice` —
matched against both resolved display names and raw resource-string
groupings). **The same widening belongs upstream in
`Engines.Firewall/Profiles.fs`** so both presets share it.

## The AI access surface (new)

Settings → **AI access**: Start/Stop a READ-ONLY session. The assistant
sees two tools — `get_firewall_summary` and `list_firewall_rules` — the
same data the Firewall page shows. There are **no mutating tools** on this
surface, so no per-call confirmation dialog is needed (that's the seam
AutoCommand's Be My Guest tab installs; adding write tools here means
adding a WinUI `IAgentBridgeUI` first — a ContentDialog confirm).

- session file: `%ProgramData%\ShieldsUp\agent\session.json` — clients read
  `{port, token, pid}` from it, never a hardcoded port
- the session dies with the window (`MainWindow.Closed` → Stop)
- the bridge engine carries this week's transport fixes **only after the
  next engine pin bump** — the pinned commit (997ebae) predates the
  session-file pid-guard, one-dialect error bodies, and no-bare-close work
  sitting uncommitted in the autocommand-iii tree. Bump the pin when that
  lands upstream.
- `AutoCommand.AgentProxy` (stdio → HTTP) is NOT shipped for Shields Up
  this round; streamable-HTTP MCP clients work directly against the
  session port.

## Deferred, with reasons

- **`UpdateFreezeService`** stays vendored: the shared copy keeps its state
  (`update_freeze.json`, `freeze_backups/`) at **CWD-relative** paths, while
  Shields Up keeps it under `%ProgramData%\ShieldsUp`. Swapping would
  orphan the freeze ledger (frozen exes could never be thawed from the new
  code). Upstream fix that unblocks this: make the shared service's state
  path injectable. The APIs are otherwise identical — the swap becomes
  trivial then.
- **`TelemetryRulesService`** stays: the shared `HostsTelemetryPins` is a
  parse/transform helper, while pin-block write/remove under rule markers
  lives app-side in both apps. Both sides already honor the same marker
  format (`# ShieldsUp telemetry-block ` / `# AutoCommand telemetry-block `)
  and each removes both apps' pins — the interop contract holds.
- **Attack-surface helpers** (WanMiniportRemover, SSTP/KDNET): still
  app-level in AutoCommand too; nothing shared to adopt yet.

## Verification

- `src/ShieldsUpSelfTest` — wires the real identity and drives the bridge
  over the wire (session file + pid, handshake as `shieldsup`, both tools
  returning real COM data, mutating tools absent, clean session-file
  removal): **8/8 PASS**.
- Full app build: 0 errors (`dotnet build src/ShieldsUp/ShieldsUp.csproj`).
- `publish.ps1` unchanged — ProjectReference wiring pulls the engines into
  the self-contained publish automatically.

## Engine-pin housekeeping

- Package pins in `ShieldsUp.Core.csproj` were aligned to the engine
  closure (`System.Management` / `System.ServiceProcess.ServiceController`
  10.0.13) — a lower pin is a NU1605 downgrade error.
- If nuget.org is unreachable on a machine (which happens on hosts whose
  firewall blocks CDN ranges — see the AutoCommand field report), restore
  with `dotnet restore --ignore-failed-sources`.
