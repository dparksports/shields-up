# Shields Up privacy policy

**The short version: by default, Shields Up sends nothing. Not at startup,
not on a timer, not "anonymized" — nothing.**

The only exception is a **voluntary, opt-in daily count**. If — and only if —
you clicked **"Yes, count me in"** in the dialog the app showed you, the app
sends the following, at most once per day:

| Field | Value | Why |
|---|---|---|
| `a` | `"shields-up"` | Which app |
| `v` | Build date, compact (e.g. `261015`) | Day-level track of which builds people run |
| `o` | Windows build (e.g. `10.0.26300`) | Know which builds to support |

**There is no identifier in the count — not even a random one.** No
install id, no machine id, no account, nothing that could ever be connected
back to you, your PC, or your Windows account. Each consenting install adds
1 to a daily total, and the server stores daily totals and nothing else:
no per-user rows exist anywhere in the system.

**What this count is not:** no hostnames, no process names, no connection
data, no behavioral events, and nothing about anything you do inside the app.

**Your IP address is never read, stored, or forwarded by Shields Up's code.**
The receiving worker runs on Cloudflare, which derives your country code at
its network edge (`request.cf.country`) before the request reaches our code —
the same mechanism every website visitor triggers. The worker keeps only the
daily aggregate; the address itself is gone with the connection.

**Consent is:**
- **Off by default** — the count is inert until you click yes.
- **Asked at most three times** — say "no" (or dismiss it three times) and
  the app never asks again.
- **Revocable anytime** — delete `HKCU\Software\ShieldsUp\UsagePing` (or set
  its `Consent` value to `no`), and nothing more is ever sent.
- **Per-app** — consent under Shields Up's own registry key; it is never
  shared with or inherited from AutoCommand.

**Everything else you can verify yourself:**
- The spec, receiver code, and database schema are public in this
  repository's [`telemetry/`](telemetry/) folder — one Cloudflare Worker and
  one aggregate table. Read the code; it is short.
- The app is built from exactly this repository (Apache 2.0), and every
  release ships `SHA256SUMS.txt`.

*Effective 2026-10-15 · Dan Park, magicpoint.ai*
