namespace ShieldsUp.Core.Models
{
    /// <summary>
    /// One row in the Bloatware page's "remove individual Microsoft apps"
    /// list: an app that ships with Windows and is deliberately NOT part of
    /// the Remove Bloatware sweep, but which a user may want gone.
    /// </summary>
    public class OptionalAppItem
    {
        public string DisplayName { get; set; }

        /// <summary>Appx package name, or the executable name for OS components.</summary>
        public string PackageName { get; set; }

        /// <summary>Resolved package full name; empty when not installed.</summary>
        public string FullName { get; set; }

        public bool IsInstalled { get; set; }

        /// <summary>
        /// True for entries Windows ships as OS components rather than Store
        /// packages. Remote Desktop Connection is one: since Windows 11 24H2 it
        /// is a servicing "optional component" (mstsc.exe + the
        /// Microsoft-Windows-TS-OptionalComponents-Package manifest), which
        /// Settings → Installed apps can uninstall but which has no package
        /// Shields Up can remove through PackageManager.
        /// </summary>
        public bool IsWindowsComponent { get; set; }

        /// <summary>
        /// True when Windows itself offers a working uninstall surface for the
        /// entry (Settings → Installed apps). Used as a fallback when the
        /// component's own uninstaller fails.
        /// </summary>
        public bool CanOpenSettings { get; set; }

        /// <summary>
        /// True when the entry is an OS component with its own first-party
        /// uninstaller (Remote Desktop Connection: mstsc.exe /uninstall) rather
        /// than a package PackageManager can remove.
        /// </summary>
        public bool RemoveViaSystemCommand { get; set; }

        /// <summary>Full path to the OS component's executable (Windows component entries only).</summary>
        public string SystemBinaryPath { get; set; }

        /// <summary>Arguments that invoke its own uninstaller, e.g. /uninstall.</summary>
        public string SystemBinaryArgs { get; set; }

        /// <summary>One line about what the app is / what removal costs.</summary>
        public string Note { get; set; }

        /// <summary>Microsoft's reinstatement path, when removal is not a one-click Store reinstall.</summary>
        public string ReinstallUrl { get; set; }

        public string Version { get; set; }

        /// <summary>A Store package we can remove, or an OS component with its own uninstaller.</summary>
        public bool CanRemove => IsInstalled &&
            (RemoveViaSystemCommand || (!IsWindowsComponent && !string.IsNullOrEmpty(FullName)));

        /// <summary>Something the row can actually do.</summary>
        public bool CanAct => CanRemove || CanOpenSettings;

        public string ActionLabel => CanRemove ? "Remove" : CanOpenSettings ? "Open Settings" : "";

        public string StatusText =>
            IsWindowsComponent ? "Windows component"
            : IsInstalled ? "Installed"
            : "Not installed";
    }
}
