namespace ShieldsUp.Core.Helpers
{
    /// <summary>
    /// The apps Windows ships that people most often ask to get rid of, but
    /// that are too useful to put in the Remove Bloatware sweep (Camera, Paint,
    /// Snipping Tool…) — removal is offered one app at a time, on request.
    ///
    /// Package identities are the Appx names used by Get-AppxPackage /
    /// PackageManager. Matching is by exact package name first, then by the
    /// documented aliases below, and only for entries that opt in, by the
    /// package's own display name. A renamed package therefore fails closed as
    /// "Not installed" instead of removing the wrong thing.
    /// </summary>
    public static class OptionalAppCatalog
    {
        public sealed record Definition(
            string DisplayName,
            string PackageName,
            string Note,
            bool WindowsComponent = false,
            string[] Aliases = null,
            string SystemBinary = null,
            string SystemBinaryNote = null,
            string SystemBinaryUninstallArgs = null,
            bool MatchByDisplayName = false,
            string ReinstallUrl = null);

        public static readonly Definition[] Apps =
        {
            new("Bing", "Microsoft.BingSearch",
                "Microsoft's search/news app. Start-menu web results are controlled separately."),
            new("Clock", "Microsoft.WindowsAlarms",
                "Alarms & Clock — timers, alarms and the world clock."),
            new("Camera", "Microsoft.WindowsCamera",
                "Camera app. Some other apps use it as their capture picker."),
            new("Paint", "Microsoft.Paint",
                "Paint. Reinstall it from the Microsoft Store whenever you need it."),
            new("Weather", "Microsoft.BingWeather",
                "MSN Weather app — live tile and forecast."),
            new("Clipchamp", "Clipchamp.Clipchamp",
                "Microsoft's video editor. Clips made with it stay in your Videos folder."),
            new("Snipping Tool", "Microsoft.ScreenSketch",
                "Screenshot and screen-recording tool. Print Screen opens it by default."),

            // Windows 11 ships the classic RDP client as a servicing "optional
            // component" (mstsc.exe + Microsoft-Windows-TS-OptionalComponents-
            // Package), not as a Store package, so PackageManager cannot remove
            // it. The client carries its own first-party uninstaller —
            // "mstsc.exe /uninstall /noPromptBeforeRestart" — which is what the
            // row now runs; Settings drives the same component removal.
            new("Remote Desktop Connection", "Microsoft.RemoteDesktopConnection",
                "The classic RDP client. Uninstalling it also removes the RemoteApp and Desktop " +
                "Connections control panel.",
                Aliases: new[] { "Microsoft.Windows.RemoteDesktopConnection", "Microsoft.RDC" },
                SystemBinary: "mstsc.exe",
                SystemBinaryNote: "Windows installs this as an optional component, and it carries its own " +
                "uninstaller — the row runs mstsc.exe /uninstall /noPromptBeforeRestart. The removal can " +
                "need a restart to finish; Microsoft's installer puts it back.",
                SystemBinaryUninstallArgs: "/uninstall /noPromptBeforeRestart",
                MatchByDisplayName: true,
                ReinstallUrl: "https://go.microsoft.com/fwlink/?linkid=2247659"),
        };
    }
}
