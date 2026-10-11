using ShieldsUp.Core.Models;
using ShieldsUp.Core.Services;
using Microsoft.UI.Xaml.Data;

namespace ShieldsUp.Converters;

/// <summary>
/// Resolves an AppPackageItem grid row to its bloatware-toggle button label,
/// tooltip ("tooltip") or visibility ("visibility"). System components and the
/// synthetic OneDrive row are not toggleable and collapse.
/// </summary>
public sealed class BloatwareStateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not AppPackageItem app) return Param(parameter) == "visibility" ? Visibility.Collapsed : "";
        bool nonToggleable = app.IsSystem || app.FullName == AppManagerService.OneDriveMarkerFullName;

        string kind = Param(parameter);
        if (kind == "visibility")
            return nonToggleable ? Visibility.Collapsed : Visibility.Visible;

        if (nonToggleable) return "";

        bool tooltip = kind == "tooltip";
        switch (AppManagerService.GetMatchSource(app.PackageName, app.Name, out _))
        {
            case AppManagerService.BloatwareMatchSource.DefaultPattern:
                return tooltip
                    ? "Matched by a default bloatware pattern — click to stop treating it as bloatware."
                    : "✓ Bloatware";
            case AppManagerService.BloatwareMatchSource.CustomPattern:
                return tooltip
                    ? "Matched by a custom pattern — click to remove that pattern."
                    : "✓ Custom pattern";
            case AppManagerService.BloatwareMatchSource.CustomPackage:
                return tooltip
                    ? "Added as a custom bloatware app — click to remove it."
                    : "✓ Custom app";
            default:
                return tooltip
                    ? "Click to treat this app as bloatware (adds its exact package name)."
                    : "＋ Bloatware";
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static string Param(object parameter) => parameter as string ?? "";
}
