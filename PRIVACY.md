# Shields Up privacy policy

**The short version: by default, Shields Up sends nothing. Not at startup,
not on a timer, not "anonymized" — nothing.**

The only exception is a **voluntary, opt-in install count**. If — and only
if — you clicked **"Yes, count me in"** in the dialog the app showed you,
the app sends the following, at most once per day:

| Field | Value | Why |
|---|---|---|
| `a` | `"shields-up"` | Which app |
| `v` | Build date, compact (e.g. `261015`) | Day-level track of which builds people run |
| `o` | Windows build (e.g. `10.0.26300`) | Know which builds to support |
| `i` | A random install id (32 hex chars) | Count *unique installs*, not launches |

**The only identifier in the count is that random id — a number the app
invents on your machine.** It is a fresh `Guid` generated locally and stored
in the count's own registry key. It is *not* derived from your hardware,
your Windows account, your install folder, or your IP address, and the
server never stores anything alongside it that could connect it to a
person. Its one job: make repeated launches and reinstalls count once
instead of once a day.

**What this count is not:** no hostnames, no process names, no connection
data, no behavioral events, and nothing about anything you do inside the app.

**Your IP address is never read, stored, or forwarded by Shields Up's code.**
The receiving worker runs on Cloudflare, which derives your country code at
its network edge (`request.cf.country`) before the request reaches our code —
the same mechanism every website visitor triggers. Its entire database is:
one row per random id (app, country, first-seen day), one row per id per
active day (app, country, version), and the older daily-total table from
before the id existed. A leak of it reveals counts and random numbers —
nothing about any person.

**Consent is:**
- **Off by default** — the count is inert until you click yes.
- **Asked at most three times** — say "no" (or dismiss it three times) and
  the app never asks again. If you said yes under the older no-id payload,
  the app asks you **once more** under the current disclosure; a "no" is
  permanent and never re-asked.
- **Revocable anytime** — delete `HKCU\Software\ShieldsUp\UsagePing` (or set
  its `Consent` value to `no`), and nothing more is ever sent **and your
  random id is erased**. Opting back in later invents a brand-new id with
  no connection to the old one.
- **Per-app** — consent under Shields Up's own registry key; it is never
  shared with or inherited from AutoCommand.

**The legal posture, honestly.** Because the id is pseudonymous, we treat
it as personal data and build to the standards the major privacy laws set
for such data: it is sent only after an informed opt-in (GDPR-style
consent), it is the minimum needed to count (data minimization), the dialog
and this page disclose exactly what is sent (transparency), and withdrawing
erases it for good (right to erasure). There is no sale or sharing as those
words are used by CCPA/CPRA-style laws, and no cross-context profiling.
That is a design claim, not legal advice: we believe the count meets what
GDPR/UK GDPR, CCPA/CPRA, LGPD, PIPEDA and similar regimes require for data
of this kind.

**Everything else you can verify yourself:**
- The spec, receiver code, and database schema are public in this
  repository's [`telemetry/`](telemetry/) folder — one Cloudflare Worker
  and three tables (two counted, one legacy). Read the code; it is short.
- The app is built from exactly this repository (Apache 2.0), and every
  release ships `SHA256SUMS.txt`.

*Effective 2026-10-15 · count updated 2026-11-18 · Dan Park, magicpoint.ai*
