# ControlCore

A native Windows app (WinUI 3 / Windows App SDK on .NET 10) with the system-control
feature set extracted from [AutoCommand](../autocommand1004): one administrator
window for hardening, debloating and privacy — no firewall/Sysmon/monitor suite in tow.

## What's inside

| Page | What it does |
|---|---|
| 📡 Attack Surface | Stop/neuter SSTP + VPN miniports, ghost hotspots (WiFi Direct), KDNET kernel debug, WAN miniport cleanup |
| 📦 Bloatware | Uninstall Store bloatware (Outlook, Xbox, Teams, Copilot, OneDrive…) with a customizable list |
| 📊 Telemetry | Official opt-outs (JSON key / env var / registry) per known phoning-home app, snapshot + revert |
| 👁 Privacy | VPN services, WiFi Direct, kernel debug, `UsageDataReceiver` task, Defender tamper status |
| ❄ Update Control | Freeze self-updating apps with NTFS deny ACEs (icacls snapshot + restore) |
| 📋 Tasks | Full Task Scheduler browser (enable/disable/run/stop/delete, incl. hidden tasks) |
| 🚀 Startup | Registry Run keys + Startup folder scanner with delete |
| 🌙 Hibernation | Hibernate off/on, power state report, true (non-hybrid) shutdown |
| 🔒 OS Hardening | LSA RunAsPPL, UAC max strictness, IPv6 kill switch, hosts file integrity + reset |
| ✅ DBX Safety | EFI mount + SHA256 baseline drift, Authenticode bootloader check, DBX update download/apply, boot repair |
| ⚙ Settings | Theme, launch-at-logon (scheduled task, highest privileges), Mark-of-the-Web unblock |

## Architecture

```
ControlCore.slnx
└── src/
    ├── ControlCore.Core/     net10.0-windows classlib — all engine code (COM/WMI/P-Invoke/WinRT), zero UI
    └── ControlCore/          WinUI 3 app — NavigationView shell + 11 pages (unpackaged, requireAdministrator)
```

Design rules carried over from AutoCommand's `docs/TECHNICAL_NOTES.md`: zero PowerShell
core (COM/WMI/P-Invoke; native CLI tools only through `ProcessRunner`), confirm-before-mutate,
system-signed components never offered for removal, downloads gated by SHA256 + Authenticode,
all state as plain JSON under `C:\ProgramData\ControlCore\` and `HKCU\Software\ControlCore`.

Shared state files (same names/formats as AutoCommand, new root): `bloatware_config.json`,
`telemetry_state.json`, `update_freeze.json` + `freeze_backups\`, `efi_baseline.json`.
ControlCore is independent of AutoCommand — install either or both; they never touch each
other's state.

## Build & run

Requires the .NET 10 SDK and Windows 10 19041+ (admin elevation is requested at launch).

```
dotnet build ControlCore.slnx
```

Release build (self-contained folder + zip + SHA256SUMS, no Windows App SDK runtime
installer needed on the target machine):

```
./publish.ps1
```

## Notable ports from the WPF original

- `TabControl` sidebar → `NavigationView`; WPF `App.xaml` design system → WinUI resource
  dictionaries with native Light/Dark/System theming (`RequestedTheme`).
- All `MessageBox` call sites → `ContentDialog` helpers (`Dialogs.Inform/Confirm/Error`).
- DataGrids → `CommunityToolkit.WinUI.UI.Controls.DataGrid`; trigger-based cell colors →
  `IValueConverter` brushes in template columns.
- `Dispatcher.Invoke` → `DispatcherQueue.TryEnqueue`; settings coupling to
  `FreshSetupService`/`SecurityEnforcer` replaced by a self-contained `LogonTaskService`
  and a trimmed Settings page.
