using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShieldsUp.Core.Services
{
    /// <summary>
    /// Aggregated outcome of applying a firewall profile, for UI progress/summary reporting.
    /// </summary>
    public class ProfileApplyResult
    {
        public string ProfileName { get; set; }
        public List<GroupToggleResult> Groups { get; } = new List<GroupToggleResult>();
        public int TotalMatched { get; set; }
        public int TotalChanged { get; set; }
        public int TotalFailed { get; set; }
        public string FirstError { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>
    /// Shields Up's named presets over the shared AutoCommand III firewall
    /// engine (Engines.Firewall through Contain.Core). The user-facing names
    /// (Strict Public / Home / Gaming / Office / Shield Up) are this app's
    /// language; every rule touch underneath goes through the same engine
    /// AutoCommand uses, so the two apps can never disagree about a group
    /// toggle or the AC-BLOCK rule family.
    /// </summary>
    public class FirewallProfileService
    {
        private static FirewallProfileService _instance;
        public static FirewallProfileService Instance => _instance ??= new FirewallProfileService();

        public enum ProfileType
        {
            Custom,
            StrictPublic,
            HomeTrusted,
            GamingMedia,
            ShieldUp,
            Office
        }

        public async Task<ProfileApplyResult> ApplyProfile(ProfileType profile, IProgress<string> progress = null)
        {
            var result = new ProfileApplyResult { ProfileName = profile.ToString() };

            switch (profile)
            {
                case ProfileType.StrictPublic:
                    await Toggle(result, progress, "File and Printer Sharing", false);
                    await Toggle(result, progress, "Network Discovery", false);
                    await Toggle(result, progress, "Remote Desktop", false);
                    break;
                case ProfileType.HomeTrusted:
                    await Toggle(result, progress, "File and Printer Sharing", true);
                    await Toggle(result, progress, "Network Discovery", true);
                    break;
                case ProfileType.GamingMedia:
                    await Toggle(result, progress, "Network Discovery", true);
                    await Toggle(result, progress, "Cast to Device functionality", true);
                    break;
                case ProfileType.ShieldUp:
                {
                    progress?.Report("Shield Up: disabling all grouped rules…");
                    var shield = await ApplyShieldUp();
                    result.Groups.Add(shield);
                    result.TotalMatched += shield.Matched;
                    result.TotalChanged += shield.Changed;
                    result.TotalFailed += shield.Failed;
                    if (shield.FirstError != null && result.FirstError == null) result.FirstError = shield.FirstError;
                    result.Notes = shield.Notes;
                    break;
                }
                case ProfileType.Office:
                    await Toggle(result, progress, "File and Printer Sharing", true);
                    await Toggle(result, progress, "Network Discovery", true);
                    await Toggle(result, progress, "Remote Desktop", true);
                    break;
            }

            return result;
        }

        private async Task Toggle(ProfileApplyResult acc, IProgress<string> progress, string group, bool enable)
        {
            progress?.Report($"{(enable ? "Enabling" : "Disabling")} group '{group}'…");
            GroupToggleResult r = await FirewallService.Instance.ToggleGroupAsync(group, enable);
            acc.Groups.Add(r);
            acc.TotalMatched += r.Matched;
            acc.TotalChanged += r.Changed;
            acc.TotalFailed += r.Failed;
            if (r.FirstError != null && acc.FirstError == null) acc.FirstError = r.FirstError;
        }

        // ── Shield Up (block all) ─────────────────────────────────────────
        // Implemented locally rather than calling the engine's ShieldsUpMdns
        // preset for one deliberate reason: the whitelist here is WIDER. The
        // 2026-10-30 field report caught a Shields-Up-era mass-disable with
        // only mDNS + Core Networking surviving — which silently takes
        // WSL2/Hyper-V VM egress down with it (forwarded VM traffic dies
        // below the per-app rule model, with no attribution anywhere in the
        // UI). VM infrastructure groups stay ON so a lockdown never kills
        // virtual machines. The same widening belongs upstream in
        // Engines.Firewall/Profiles.fs.

        private static readonly string[] ShieldWhitelistDisplay = { "mDNS", "Core Networking" };

        // Raw-group tokens (resource-string groups like icsvc.dll,-700 and
        // the WSL ms-resource package group never resolve to a friendly
        // name) — matched case-insensitively against the raw grouping.
        private static readonly string[] ShieldWhitelistRawTokens =
        {
            "icsvc",                 // Hyper-V Host Compute Service (Virtual Machine rules)
            "SubsystemForLinux",     // WSL store-package group (ms-resource://...SubsystemForLinux...)
            "Hyper-V",
            "Virtual Machine",
            "VirtualMachine",
            "WSL",
            "HNS",                   // Host Network Service (vSwitch/NAT plumbing)
            "wslservice",
            "vmcompute",
        };

        private static bool ShieldWhitelisted(string display, string rawGroup)
        {
            foreach (var wl in ShieldWhitelistDisplay)
                if (display.Equals(wl, StringComparison.OrdinalIgnoreCase) ||
                    rawGroup.Equals(wl, StringComparison.OrdinalIgnoreCase))
                    return true;
            foreach (var token in ShieldWhitelistRawTokens)
                if (rawGroup.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    display.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private Task<GroupToggleResult> ApplyShieldUp()
        {
            // Disable ALL grouped rules, then re-enable the whitelist
            return Task.Run(() =>
            {
                    var result = new GroupToggleResult { GroupName = "Shield Up (block all)" };
                try
                {
                    Type fwPolicyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
                    dynamic fwPolicy = Activator.CreateInstance(fwPolicyType);
                    int disabledCount = 0;
                    int whitelistEnabled = 0;
                    bool vmSpared = false;

                    // Pass 1: Disable everything with a group
                    foreach (dynamic rule in fwPolicy.Rules)
                    {
                        try
                        {
                            string group = rule.Grouping ?? "";
                            if (string.IsNullOrEmpty(group)) continue;

                            bool wasEnabled = (bool)rule.Enabled;
                            rule.Enabled = false;
                            result.Matched++;
                            if (wasEnabled)
                            {
                                result.Changed++;
                                disabledCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Failed++;
                            if (result.FirstError == null) result.FirstError = ex.Message;
                        }
                    }

                    // Pass 2: Re-enable whitelisted groups (networking basics
                    // + VM infrastructure — see the note above)
                    foreach (dynamic rule in fwPolicy.Rules)
                    {
                        try
                        {
                            string group = rule.Grouping ?? "";
                            if (string.IsNullOrEmpty(group)) continue;
                            string display = FirewallService.ResolveGroupName(group);
                            if (!ShieldWhitelisted(display, group)) continue;
                            if (!display.Equals("mDNS", StringComparison.OrdinalIgnoreCase) &&
                                !display.Equals("Core Networking", StringComparison.OrdinalIgnoreCase))
                                vmSpared = true;

                            bool wasEnabled = (bool)rule.Enabled;
                            rule.Enabled = true;
                            result.Matched++;
                            whitelistEnabled++;
                            if (!wasEnabled) result.Changed++;
                        }
                        catch (Exception ex)
                        {
                            result.Failed++;
                            if (result.FirstError == null) result.FirstError = ex.Message;
                        }
                    }

                    result.Notes = $"{disabledCount} grouped rule(s) disabled, {whitelistEnabled} whitelist rule(s) " +
                                   $"re-enabled{(vmSpared ? " (VM networking spared)" : "")}";
                }
                catch (Exception ex)
                {
                    result.FailedCompletely = true;
                    result.FirstError = ex.Message;
                }
                return result;
            });
        }
    }
}
