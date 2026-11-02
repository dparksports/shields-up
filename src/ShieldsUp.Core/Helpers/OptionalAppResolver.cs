using System;
using System.Collections.Generic;
using System.Linq;
using ShieldsUp.Core.Models;

namespace ShieldsUp.Core.Helpers
{
    /// <summary>
    /// Pure decision logic for the "remove individual Microsoft apps" list.
    /// Kept out of <see cref="Services.AppManagerService"/> so the states can be
    /// reasoned about — and checked — without a package manager or a desktop:
    ///
    ///   package found (exact name, alias, or display name) -> Removable
    ///   no package, OS binary present                      -> Windows component
    ///   no package, no binary                              -> Not installed
    ///
    /// "Windows component" is not a dead end: for entries Windows exposes in
    /// Settings → Installed apps the row offers a button that opens that page.
    /// Remote Desktop Connection lives there as the servicing optional component
    /// Microsoft-Windows-TS-OptionalComponents-Package (the payload is
    /// mstsc.exe), and Microsoft publishes no removal API for it beyond the
    /// Settings surface — so pointing at Settings is the supported answer, not
    /// deleting files out of System32.
    /// </summary>
    public static class OptionalAppResolver
    {
        public sealed record InstalledPackage(string Name, string FullName, string Version, string DisplayName);

        public static OptionalAppItem Resolve(
            OptionalAppCatalog.Definition def,
            IReadOnlyList<InstalledPackage> installed,
            Func<string, bool> systemBinaryExists)
        {
            var item = new OptionalAppItem
            {
                DisplayName = def.DisplayName,
                PackageName = def.PackageName,
                Note = def.Note,
                IsWindowsComponent = def.WindowsComponent,
                ReinstallUrl = def.ReinstallUrl,
            };

            InstalledPackage match = null;
            if (!def.WindowsComponent)
            {
                match = installed.FirstOrDefault(p =>
                    string.Equals(p.Name, def.PackageName, StringComparison.OrdinalIgnoreCase));

                if (match is null && def.Aliases is not null)
                {
                    foreach (var alias in def.Aliases)
                    {
                        match = installed.FirstOrDefault(p =>
                            string.Equals(p.Name, alias, StringComparison.OrdinalIgnoreCase));
                        if (match is not null) break;
                    }
                }

                if (match is null && def.MatchByDisplayName)
                {
                    match = installed.FirstOrDefault(p =>
                        !string.IsNullOrWhiteSpace(p.DisplayName) &&
                        string.Equals(p.DisplayName.Trim(), def.DisplayName, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (match is not null)
            {
                item.IsInstalled = true;
                item.FullName = match.FullName;
                item.Version = match.Version;
                return item;
            }

            if (def.SystemBinary is null) return item;

            if (!systemBinaryExists(def.SystemBinary)) return item;

            item.IsInstalled = true;
            item.IsWindowsComponent = true;
            item.Note = def.SystemBinaryNote;
            item.CanOpenSettings = true;   // Settings can uninstall it too
            item.SystemBinaryPath = System.IO.Path.Combine(Environment.SystemDirectory, def.SystemBinary);
            item.SystemBinaryArgs = def.SystemBinaryUninstallArgs;
            item.RemoveViaSystemCommand = !string.IsNullOrEmpty(def.SystemBinaryUninstallArgs);
            return item;
        }
    }
}
