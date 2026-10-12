# Shields Up

![Shields Up](docs/img/hero.png)

A free, open-source control center for Windows that strips out the things you never
asked for — preinstalled apps, background telemetry, hidden remote-access features —
and shows you exactly what it's about to change before it changes anything.

No installer. No account. No background processes. It runs when you open it and is
gone when you close it.

---

## Why you'd want this

**Windows arrives with company on board.** Games you didn't install, apps phoning
home with usage statistics, VPN and remote-access plumbing most people will never
use — all of it sitting there, consuming resources and widening the door.

Shields Up gives you one window with plain-English controls for all of it:

- **🧹 Remove the junk** — uninstall the preinstalled apps you don't want
  (Outlook, Xbox, Teams, Copilot, OneDrive, …), with a list you can edit.
- **📵 Stop the phone-home** — apply the *official* opt-out switch for software
  known to collect usage data, and check it actually stopped.
- **🚪 Close the side doors** — turn off Windows' built-in VPN backdoors, hidden
  hotspot adapters, and kernel-debugging access that remote tools could abuse.
- **🧊 Freeze annoying updaters** — stop an app from replacing itself every week,
  without breaking the app. Unfreeze anytime.
- **🛡 Set the firewall in one click** — sensible presets from strict to
  easy-going, a browser for every rule, and a panic button that undoes all of it.
- **✅ One fresh-install pass** — Quick Scan checks what's already done and
  finishes the rest in order.

Every action asks first. Most actions have an undo. Nothing is hidden.

## Who makes this

**Dan Park — MagicPoint.ai, California.** Shields Up grew out of
[AutoCommand](https://github.com/), a Windows security toolkit, by pulling out the
parts a typical PC owner actually wants and leaving the analyst tooling behind.
The app carries the same credit in Settings → the footnote at the bottom of the page.

## "Why should I run an app from a publisher I don't know?"

You shouldn't — not on faith. Don't take anyone's word for it, including ours.
Here's what you can **check** instead:

1. **Read every line.** The complete source is in this repository under the
   Apache 2.0 license. There is no compiled mystery: what you download is built
   from exactly this code.
2. **Watch what it does.** No installer — you unzip a folder and run an exe.
   No background service, no task in Task Scheduler (unless *you* turn on
   "launch at logon"), nothing in your tray. Close it, and it's not running.
3. **Watch the network.** The app makes exactly two kinds of connection, both
   only when *you* click a button: the update check talks to GitHub, and the
   DBX Safety page downloads a Microsoft update when asked. That's the whole list.
   No telemetry, no analytics, no "anonymous statistics", no accounts.
4. **Verify the download.** Every release ships a `SHA256SUMS.txt`; the app's
   own update checker verifies against it before trusting a download — you can
   do the same with `certutil -hashfile <zip> SHA256` before you ever run it.
5. **See changes before they happen.** Nothing on any page executes without a
   confirmation that says what will change, and the risky corners (Windows'
   own signed components, for example) are locked out entirely.
6. **Remove everything.** Delete the folder and the app is gone. Its settings
   live in one place — `C:\ProgramData\ShieldsUp` — delete that too and there
   is no trace left.

**About that blue "unknown publisher" warning:** it appears because the app is
not yet code-signed — a certificate that costs real money and ties the binary to
a verified identity. Until releases are signed (planned), Windows will warn on
first run. That warning is Windows doing its job; the sections above are ours.

**Why it asks for administrator:** the whole point is managing firewall rules,
system services, and device drivers — Windows requires admin for those. A
version that didn't ask couldn't do what it promises.

## What's inside

![The fourteen tools](docs/img/features.png)

| Tool | For you, that means |
|---|---|
| ⚡ **Quick Scan** | One button for a fresh PC: runs the checklist below, tells you what's already done. |
| 📡 **Attack Surface** | A green/amber count of every hidden network entry point, then one-click shutoff for each kind. |
| 🛡 **Firewall** | Presets, every rule in a sortable list, and **Restore Internet** — one button that undoes all blocking if something broke. |
| 📦 **Bloatware** | Uninstall the preinstalled apps; edit the list; nothing system-critical is ever offered. |
| 📊 **Telemetry** | The official opt-out for each known data-collecting app, with an undo back to the previous setting. |
| 👁 **Privacy** | VPN services, hidden hotspot adapters, kernel debug, the Windows feedback task — each switchable. |
| ❄ **Update Control** | Freeze an app at its current version; its updater stops working, the app keeps working. |
| 📋 **Tasks** | Everything Windows has scheduled, including hidden items — run, stop, disable, delete. |
| 🚀 **Startup** | What launches at boot, from where, with delete. |
| 🌙 **Hibernation** | Remove the hiberfile (or bring it back), and a true full shutdown. |
| 🔒 **OS Hardening** | The four big Windows security switches in plain language, plus a hosts-file check. |
| ✅ **DBX Safety** | Verifies your boot files haven't been tampered with and applies Microsoft's revocation updates. |
| 🔓 **Unblock** | Removes the "downloaded from the internet" mark that makes Windows nag on extracted files. |
| ⚙ **Settings** | Start-at-logon (optional), check for updates, version info. |

## Try it

1. Download `ShieldsUp-v<date>-win-x64.zip` from [releases](https://github.com/dparksports/shields-up/releases/latest).
2. (Optional, encouraged) check the hash against `SHA256SUMS.txt`.
3. Extract anywhere and run `ShieldsUp.exe`. Windows will ask for permission — that's the admin rights from above.
4. Start with **Quick Scan** and read before you click.

**Uninstalling** = deleting the folder (and `C:\ProgramData\ShieldsUp` if you want the settings gone too).

---

## For developers

![Architecture](docs/img/architecture.png)

Native WinUI 3 / Windows App SDK on .NET 10, two projects: `ShieldsUp.Core`
(the entire engine — COM/WMI/P-Invoke/WinRT, zero UI types, zero PowerShell;
native CLI tools only through one audited `ProcessRunner`) and `ShieldsUp`
(the shell + 14 pages). State is plain JSON under
`C:\ProgramData\ShieldsUp` (same file formats as AutoCommand, namespaced);
sessions, navigation, and exceptions log to `logs\app.log` with WER local
dumps registered for native crashes. The update checker compares against
`releases/latest` and SHA256-verifies downloads against `SHA256SUMS.txt`.
Versions are build dates, stamped automatically by `publish.ps1`.

```powershell
dotnet build ShieldsUp.slnx          # build
./publish.ps1                        # self-contained win-x64 folder + zip + SHA256SUMS
```

Unsigned builds may be blocked by Smart App Control; for distribution guidance
(Azure Artifact Signing, ~$9.99/mo) see the wiki or contact the author.

## License

Apache License 2.0 — see [LICENSE](LICENSE). Derived from AutoCommand; the
derivation is recorded in [NOTICE](NOTICE).

---

Made with ❤️ in California · © 2026 Dan Park · MagicPoint.ai
