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
        /// True for entries that are not Store packages at all (mstsc.exe ships
        /// inside Windows) — listed so users see the honest answer instead of
        /// hunting for a remove button that cannot exist.
        /// </summary>
        public bool IsWindowsComponent { get; set; }

        /// <summary>One line about what the app is / what removal costs.</summary>
        public string Note { get; set; }

        public string Version { get; set; }

        /// <summary>Only an installed, resolvable Store package can be removed.</summary>
        public bool CanRemove => IsInstalled && !IsWindowsComponent && !string.IsNullOrEmpty(FullName);

        public string StatusText =>
            IsWindowsComponent ? "Windows component"
            : IsInstalled ? "Installed"
            : "Not installed";
    }
}
