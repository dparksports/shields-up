import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, Polygon
from matplotlib.font_manager import FontProperties
import textwrap

EMOJI = FontProperties(family="Segoe UI Emoji")

# ---- Shields Up theme (EmojiWifi reference palette) ----
PAGE   = "#ECF2F9"
PANEL  = "#FDFEFF"
CARD   = "#FFFFFF"
BORDER = "#E1E7F0"
TEXT   = "#1D1D1F"
SUBTLE = "#86868B"
ACCENT = "#007AFF"
ACCENT_D = "#0062CC"
PILL   = "#E5F1FF"
GREEN  = "#1E8E3E"
RED    = "#D70015"
AMBER  = "#B26A00"

plt.rcParams["font.family"] = ["Segoe UI", "DejaVu Sans"]

def rbox(ax, x, y, w, h, fc, ec, r=10, lw=1.2, z=2):
    ax.add_patch(FancyBboxPatch((x, y), w, h,
                 boxstyle=f"round,pad=0,rounding_size={r}",
                 fc=fc, ec=ec, lw=lw, zorder=z))

def shield(ax, cx, cy, s, fc=ACCENT, check="white", z=5):
    pts = [(cx - .62*s, cy + .55*s), (cx - .57*s, cy - .05*s), (cx - .38*s, cy - .32*s),
           (cx, cy - .62*s), (cx + .38*s, cy - .32*s), (cx + .57*s, cy - .05*s),
           (cx + .62*s, cy + .55*s), (cx, cy + .78*s)]
    ax.add_patch(Polygon(pts, fc=fc, ec="none", zorder=z))
    ax.plot([cx - .26*s, cx - .08*s, cx + .30*s],
            [cy - .02*s, cy - .20*s, cy + .30*s],
            color=check, lw=s*.14, solid_capstyle="round", solid_joinstyle="round", zorder=z+1)

# ============================================================ 1. HERO
fig, ax = plt.subplots(figsize=(12.8, 5.2), dpi=125)
fig.patch.set_facecolor(PAGE); ax.set_facecolor(PAGE)
ax.set_xlim(0, 1280); ax.set_ylim(0, 520); ax.axis("off")

shield(ax, 175, 260, 240)
ax.text(385, 330, "Shields Up", fontsize=58, fontweight="bold", color=TEXT, va="center")
ax.text(389, 262, "One administrator window for Windows hardening,", fontsize=19, color=SUBTLE, va="center")
ax.text(389, 230, "firewall, debloating and privacy — nothing hidden, everything reversible.", fontsize=19, color=SUBTLE, va="center")
ax.add_patch(FancyBboxPatch((387, 205), 240, 7, boxstyle="round,pad=0,rounding_size=3", fc=ACCENT, ec="none"))

chips = [("WinUI 3 · .NET 10", True), ("Windows 10 19041+", False), ("Apache-2.0", False)]
x = 387
for label, is_accent in chips:
    w = 44 + len(label) * 9.6
    rbox(ax, x, 138, w, 44, PILL if is_accent else CARD, BORDER, r=12)
    ax.text(x + w/2, 160, label, fontsize=13.5,
            color=ACCENT_D if is_accent else TEXT,
            fontweight="semibold" if is_accent else "normal", ha="center", va="center")
    x += w + 14
ax.text(389, 84, "Date-versioned releases · in-app update check · zero PowerShell core",
        fontsize=13, color=SUBTLE, va="center")
plt.savefig("docs/img/hero.png", bbox_inches="tight", pad_inches=0.25, facecolor=PAGE)
plt.close()

# ============================================================ 2. FEATURES
feats = [
    ("⚡", "Quick Scan",      "Fresh-install hardening in one pass; checklist doubles as a live dashboard", ACCENT),
    ("📡", "Attack Surface",  "WAN miniport cleanup, SSTP backdoors, ghost hotspots, KDNET", RED),
    ("🛡", "Firewall",        "Profiles incl. Shield Up, rule browser, group toggles, Restore Internet", ACCENT),
    ("📦", "Bloatware",       "Uninstall Store bloat with a user-editable list; OneDrive included", AMBER),
    ("📊", "Telemetry",       "Official opt-outs per app, snapshotted and one-click reversible", GREEN),
    ("👁", "Privacy",         "VPN services, WiFi Direct, kernel debug, telemetry task, tamper status", GREEN),
    ("❄", "Update Control",   "Freeze self-updaters with NTFS deny ACEs; icacls snapshot + restore", ACCENT),
    ("📋", "Tasks",           "Full Task Scheduler browser incl. hidden tasks — run, stop, disable", TEXT),
    ("🚀", "Startup",         "Run keys + Startup folders scanner with delete", TEXT),
    ("🌙", "Hibernation",     "Hiberfile off/on, power-state report, true non-hybrid shutdown", ACCENT),
    ("🔒", "OS Hardening",    "LSA RunAsPPL, max UAC, IPv6 kill switch, hosts-file integrity", RED),
    ("✅", "DBX Safety",      "EFI baseline drift, bootloader Authenticode, DBX update + boot repair", GREEN),
    ("🔓", "Unblock",         "Mark-of-the-Web removal for extracted zips", AMBER),
]
cols = 4
CW, CH, GAP = 292, 168, 22
rows = 4  # 13 features + Settings card
W = cols*CW + (cols+1)*GAP
H = 120 + rows*CH + (rows+1)*GAP
fig, ax = plt.subplots(figsize=(W/100, H/100), dpi=125)
fig.patch.set_facecolor(PAGE); ax.set_facecolor(PAGE)
ax.set_xlim(0, W); ax.set_ylim(0, H); ax.axis("off")
ax.text(GAP, H-52, "Fourteen tools, one window", fontsize=26, fontweight="bold", color=TEXT)
ax.text(GAP, H-84, "Every action reuses the exact engine behind its page — the checklist never drifts from the tools.",
        fontsize=13, color=SUBTLE)

cards = feats + [("⚙", "Settings", "Launch-at-logon, in-app update check via GitHub releases, Apache-2.0 footnote", SUBTLE)]
for i, (emoji, name, desc, col) in enumerate(cards):
    c, r = i % cols, i // cols
    x = GAP + c*(CW+GAP)
    y = H - 120 - (r+1)*CH - r*GAP
    rbox(ax, x, y, CW, CH, CARD, BORDER, r=12)
    ax.text(x+18, y+CH-36, emoji, fontsize=16, va="center", fontproperties=EMOJI)
    ax.text(x+54, y+CH-36, name, fontsize=14.5, fontweight="bold", color=TEXT, va="center")
    ax.plot([x+18, x+62], [y+CH-56, y+CH-56], color=col, lw=2.5, solid_capstyle="round")
    ax.text(x+18, y+CH-68, textwrap.fill(desc, width=32), fontsize=10.2,
            color=SUBTLE, va="top", linespacing=1.45)
plt.savefig("docs/img/features.png", bbox_inches="tight", pad_inches=0.25, facecolor=PAGE)
plt.close()

# ============================================================ 3. ARCHITECTURE
fig, ax = plt.subplots(figsize=(12.8, 7.0), dpi=125)
fig.patch.set_facecolor(PAGE); ax.set_facecolor(PAGE)
ax.set_xlim(0, 1280); ax.set_ylim(0, 700); ax.axis("off")

rbox(ax, 90, 516, 800, 140, CARD, BORDER, r=12)
ax.text(110, 624, "ShieldsUp.app", fontsize=17, fontweight="bold", color=TEXT, va="center")
ax.text(110, 600, "WinUI 3 shell — NavigationView, 14 pages, ContentDialogs, elevated content panels",
        fontsize=11.5, color=SUBTLE, va="center")
chips = ["Quick Scan", "Attack Surface", "Firewall", "Bloatware", "Telemetry", "Privacy",
         "Update Control", "Tasks", "Startup", "Hibernation", "Hardening", "DBX", "Unblock", "Settings"]
x = 110; yy = 570
for chip in chips:
    w = 18 + len(chip)*8.0
    if x + w > 860:
        x = 110; yy -= 34
    rbox(ax, x, yy, w, 26, PILL, BORDER, r=8)
    ax.text(x+w/2, yy+13, chip, fontsize=9.3, color=ACCENT_D, ha="center", va="center")
    x += w + 7

ax.annotate("", xy=(490, 456), xytext=(490, 508), arrowprops=dict(arrowstyle="-|>", lw=2, color=SUBTLE))
ax.text(505, 482, "direct engine calls", fontsize=10.5, color=SUBTLE)

rbox(ax, 90, 264, 800, 186, CARD, BORDER, r=12)
ax.text(110, 416, "ShieldsUp.Core", fontsize=17, fontweight="bold", color=TEXT, va="center")
ax.text(110, 392, "Engine class library — zero UI types, zero PowerShell", fontsize=11.5, color=SUBTLE, va="center")
core = ["FirewallService (COM)", "TaskSchedulerService", "AppManagerService", "UpdateFreezeService",
        "TelemetryRulesService", "QuickScanService", "WanMiniportRemover", "RegistryHelper",
        "ServiceHelper", "DbxRemediator", "UpdateCheckService", "AppLogger"]
x = 110; yy = 342
for svc in core:
    w = 20 + len(svc)*7.6
    if x + w > 860:
        x = 110; yy -= 32
    rbox(ax, x, yy, w, 26, "#F2F5FA", BORDER, r=8)
    ax.text(x+w/2, yy+13, svc, fontsize=9.2, color=TEXT, ha="center", va="center")
    x += w + 7

ax.annotate("", xy=(490, 224), xytext=(490, 258), arrowprops=dict(arrowstyle="-|>", lw=2, color=SUBTLE))
ax.text(505, 240, "COM · WMI · P/Invoke · WinRT", fontsize=10.5, color=SUBTLE)

rbox(ax, 90, 70, 800, 148, CARD, BORDER, r=12)
ax.text(110, 186, "Windows", fontsize=17, fontweight="bold", color=TEXT, va="center")
wins = [("INetFwPolicy2", ACCENT), ("Task Scheduler", ACCENT), ("SetupAPI devices", RED),
        ("Win32 services", RED), ("PackageManager", GREEN), ("powercfg / bcdedit", ACCENT),
        ("Registry HKLM/HKCU", AMBER), ("UEFI dbx", GREEN)]
x = 110; yy = 118
for wname, col in wins:
    w = 24 + len(wname)*7.8
    if x + w > 860:
        x = 110; yy -= 38
    rbox(ax, x, yy, w, 28, CARD, BORDER, r=9)
    ax.plot([x+10, x+10], [yy+8, yy+20], color=col, lw=3, solid_capstyle="round")
    ax.text(x+20, yy+14, wname, fontsize=9.4, color=TEXT, va="center")
    x += w + 8

rbox(ax, 930, 330, 300, 326, PANEL, BORDER, r=12)
ax.text(950, 624, "State on disk", fontsize=15, fontweight="bold", color=TEXT, va="center")
states = [
    ("ProgramData\\ShieldsUp", TEXT, True),
    ("  bloatware_config.json", SUBTLE, True),
    ("  firewall_config.json", SUBTLE, True),
    ("  telemetry_state.json", SUBTLE, True),
    ("  update_freeze.json", SUBTLE, True),
    ("  efi_baseline.json", SUBTLE, True),
    ("HKCU\\Software\\ShieldsUp", TEXT, True),
    ("logs\\app.log + WER dumps", TEXT, True),
    ("AC-BLOCK-* firewall rules", TEXT, False),
]
yy = 590
for sline, col, mono in states:
    ax.text(950, yy, sline, fontsize=10, color=col, va="center",
            family="Consolas" if mono else None)
    yy -= 27
ax.text(950, yy-2, "Same formats as AutoCommand,\nnamespaced — never shared.", fontsize=9.5, color=SUBTLE, va="top")

ax.plot([90, 1230], [58, 58], color=BORDER, lw=1.2)
ax.text(90, 36, "Design rules: confirm-before-mutate · system-signed components never touched · downloads gated by SHA256 + Authenticode",
        fontsize=10.5, color=SUBTLE, va="center")
plt.savefig("docs/img/architecture.png", bbox_inches="tight", pad_inches=0.25, facecolor=PAGE)
plt.close()
print("3 infographics written")
