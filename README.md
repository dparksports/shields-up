# Shields Up

A native Windows app (WinUI 3 / Windows App SDK on .NET 10) with the system-control
feature set extracted from [AutoCommand](../autocommand1004): one dark, always-elevated
administrator window for hardening, firewall, debloating and privacy — no Sysmon /
process-monitor suite in tow.

## What's inside

| Page | What it does |
|---|---|
| ⚡ Quick Scan | One pass over a fresh install: miniports, Shield Up firewall, telemetry task, IPv6+UAC, logon task, bloatware, hibernation — checklist doubles as a live status dashboard |
| 📡 Attack Surface | Four lists: SSTP/VPN backdoors + WAN miniports, ghost hotspots (WiFi Direct), KDNET kernel debug, WAN miniport cleanup |
| 🛡 Firewall | Profiles (incl. Shield Up), inbound/outbound rule browser with group toggles, legacy AC-BLOCK rename, 🌐 Restore Internet panic button |
| 📦 Bloatware | Uninstall Store bloatware (Outlook, Xbox, Teams, Copilot, OneDrive…) with a customizable list |
| 📊 Telemetry | Official opt-outs (JSON key / env var / registry) per known phoning-home app, snapshot + revert |
| 👁 Privacy | VPN services, WiFi Direct, kernel debug, `UsageDataReceiver` task, Defender tamper status |
| ❄ Update Control | Freeze self-updating apps with NTFS deny ACEs (icacls snapshot + restore) |
| 📋 Tasks | Full Task Scheduler browser (enable/disable/run/stop/delete, incl. hidden tasks) |
| 🚀 Startup | Registry Run keys + Startup folder scanner with delete |
| 🌙 Hibernation | Hibernate off/on, power state report, true (non-hybrid) shutdown |
| 🔒 OS Hardening | LSA RunAsPPL, UAC max strictness, IPv6 kill switch, hosts file integrity + reset |
| ✅ DBX Safety | EFI mount + SHA256 baseline drift, Authenticode bootloader check, DBX update download/apply, boot repair |
| ⚙ Settings | Launch-at-logon (scheduled task, highest privileges), Mark-of-the-Web unblock |

Visual theme ported from the EmojiWifi Windows reference: Apple-style light UI —
`#007AFF` accent, white sidebar with `#E5F1FF` selected pills, `#F5F5F7` content over
white cards (radius 12) with a hint of drop shadow.

## Architecture

```
ShieldsUp.slnx
└── src/
    ├── ShieldsUp.Core/     net10.0-windows classlib — all engine code (COM/WMI/P-Invoke/WinRT), zero UI
    └── ShieldsUp/          WinUI 3 app — NavigationView shell + 13 pages (unpackaged, requireAdministrator)
```

Design rules carried over from AutoCommand's `docs/TECHNICAL_NOTES.md`: zero PowerShell
core (COM/WMI/P-Invoke; native CLI tools only through `ProcessRunner`), confirm-before-mutate,
system-signed components never offered for removal, downloads gated by SHA256 + Authenticode,
all state as plain JSON under `C:\ProgramData\ShieldsUp\` and `HKCU\Software\ShieldsUp`.

State files (same names/formats as AutoCommand, new root): `bloatware_config.json`,
`firewall_config.json`, `telemetry_state.json`, `update_freeze.json` + `freeze_backups\`,
`efi_baseline.json`. Shields Up is independent of AutoCommand — install either or both;
they never touch each other's state. Firewall rule names (`AC-BLOCK…`) are shared
vocabulary: Shields Up's Restore Internet button manages the same block-rule fleet
AutoCommand created, so it works on machines that ran either (or both).

## Build & run

Requires the .NET 10 SDK and Windows 10 19041+ (admin elevation is requested at launch).

```
dotnet build ShieldsUp.slnx
```

Release build (self-contained folder + zip + SHA256SUMS, no Windows App SDK runtime
installer needed on the target machine):

```
./publish.ps1
```

## Notable ports from the WPF original

- `TabControl` sidebar → `NavigationView`; WPF `App.xaml` design system → WinUI resource
  dictionaries, dark-only with the window content extended into the title bar.
- All `MessageBox` call sites → `ContentDialog` helpers (`Dialogs.Inform/Confirm/Error`).
- DataGrids → `CommunityToolkit.WinUI.UI.Controls.DataGrid`; trigger-based cell colors →
  `IValueConverter` brushes in template columns; WPF `CollectionViewSource` grouping →
  group-then-name sort.
- `Dispatcher.Invoke` → `DispatcherQueue.TryEnqueue`; the Settings coupling to
  `FreshSetupService`/`SecurityEnforcer` replaced by a self-contained `LogonTaskService`
  and a trimmed Settings page.

## License

Apache License 2.0 — see [LICENSE](LICENSE). Shields Up is derived from
AutoCommand; the derivation is recorded in [NOTICE](NOTICE).
