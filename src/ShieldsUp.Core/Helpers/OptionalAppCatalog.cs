namespace ShieldsUp.Core.Helpers
{
    /// <summary>
    /// The apps Windows ships that people most often ask to get rid of, but
    /// that are too useful to put in the Remove Bloatware sweep (Camera, Paint,
    /// Snipping Tool…) — removal is offered one app at a time, on request.
    ///
    /// Package identities are the Appx names used by Get-AppxPackage /
    /// PackageManager. They are stable across Windows 11 releases; matching is
    /// by exact package name, never by display name, so a renamed app can never
    /// cause the wrong package to be removed.
    /// </summary>
    public static class OptionalAppCatalog
    {
        public sealed record Definition(
            string DisplayName,
            string PackageName,
            string Note,
            bool WindowsComponent = false);

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
            new("Remote Desktop Connection", "mstsc.exe",
                "mstsc.exe ships inside Windows and is not a Store package, so it cannot be " +
                "uninstalled. To stop other machines connecting to this one, turn off " +
                "Remote Desktop in Settings → System → Remote Desktop.",
                WindowsComponent: true),
        };
    }
}
