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

            // Since Windows 11 23H2 the classic RDP client ships as an app that
            // Settings → Installed apps can uninstall; on older builds mstsc.exe
            // is part of Windows and there is nothing to remove. Both states are
            // reported honestly rather than offering a button that cannot work.
            new("Remote Desktop Connection", "Microsoft.RemoteDesktopConnection",
                "The classic RDP client. Uninstalling it also removes the RemoteApp and Desktop " +
                "Connections control panel.",
                Aliases: new[] { "Microsoft.Windows.RemoteDesktopConnection", "Microsoft.RDC" },
                SystemBinary: "mstsc.exe",
                SystemBinaryNote: "This build does not list Remote Desktop Connection as an app, so there " +
                "is nothing to uninstall. Windows 11 23H2 and later offer it under Settings → Installed " +
                "apps; on earlier builds mstsc.exe is part of Windows. To stop incoming connections " +
                "instead, turn off Remote Desktop in Settings → System → Remote Desktop.",
                MatchByDisplayName: true,
                ReinstallUrl: "https://go.microsoft.com/fwlink/?linkid=2247659"),
        };
    }
}
