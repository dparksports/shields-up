using Microsoft.UI.Xaml;
using Windows.Storage;

namespace ControlCore.Services;

/// <summary>
/// Light/Dark/System theme selection, persisted in HKCU\Software\ControlCore (same pattern as
/// AutoCommand's ThemeService, but using WinUI's native RequestedTheme instead of brush swapping).
/// </summary>
public static class ThemeService
{
    private const string RegKey = @"Software\ControlCore";
    private const string RegValue = "Theme";

    public static string Saved
    {
        get
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegKey);
            return key?.GetValue(RegValue) as string ?? "Default";
        }
        set
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RegKey);
            key.SetValue(RegValue, value ?? "Default");
        }
    }

    public static void ApplySavedTheme()
    {
        if (App.MainWnd?.Content is FrameworkElement root)
            ApplyTo(root, Saved);
    }

    public static void ApplyTo(FrameworkElement root, string theme)
    {
        root.RequestedTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }
}
