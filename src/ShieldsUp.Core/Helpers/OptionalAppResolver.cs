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
    ///   package found (exact name, alias, or display name)  -> Removable
    ///   no package, mstsc.exe present, Windows older than 23H2 -> Windows component
    ///   no package, mstsc.exe present, Windows 23H2 or newer   -> Not installed
    ///   no package, no mstsc.exe                                -> Not installed
    ///
    /// The last two are deliberately different: a 23H2+ machine that lacks the
    /// Remote Desktop Connection app has nothing to remove, so no button is
    /// offered; only pre-23H2 builds get the "part of Windows" explanation.
    /// </summary>
    public static class OptionalAppResolver
    {
        /// <summary>Windows 11 23H2, where Remote Desktop Connection became a removable app.</summary>
        public const int RemoteDesktopAppMinBuild = 22631;

        public sealed record InstalledPackage(string Name, string FullName, string Version, string DisplayName);

        public static OptionalAppItem Resolve(
            OptionalAppCatalog.Definition def,
            IReadOnlyList<InstalledPackage> installed,
            int osBuild,
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

            bool binaryExists = systemBinaryExists(def.SystemBinary);
            item.IsWindowsComponent = binaryExists && osBuild < RemoteDesktopAppMinBuild;
            item.IsInstalled = item.IsWindowsComponent;
            item.Note = binaryExists ? def.SystemBinaryNote : def.Note;
            return item;
        }
    }
}
