using ControlCore.Pages;

namespace ControlCore;

public sealed partial class MainWindow : Window
{
    private static readonly Dictionary<string, Type> PageMap = new()
    {
        ["AttackSurface"] = typeof(AttackSurfacePage),
        ["Bloatware"] = typeof(BloatwarePage),
        ["Telemetry"] = typeof(TelemetryPage),
        ["Privacy"] = typeof(PrivacyPage),
        ["UpdateControl"] = typeof(UpdateControlPage),
        ["Tasks"] = typeof(TasksPage),
        ["Startup"] = typeof(StartupPage),
        ["Hibernation"] = typeof(HibernationPage),
        ["Hardening"] = typeof(HardeningPage),
        ["DbxSafety"] = typeof(DbxSafetyPage),
        ["Settings"] = typeof(SettingsPage),
    };

    public Frame Frame => ContentFrame;

    public MainWindow()
    {
        InitializeComponent();
        Title = $"ControlCore v{ThisVersion()}";
        ExtendsContentIntoTitleBar = false;
        ContentFrame.Navigated += (_, _) => { };
        TrySetWindowIcon();
    }

    private void TrySetWindowIcon()
    {
        try
        {
            string ico = Path.Combine(AppContext.BaseDirectory, "Assets", "ControlCore.ico");
            if (!File.Exists(ico)) return;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            Microsoft.UI.Windowing.AppWindow.GetFromWindowId(wndId).SetIcon(ico);
        }
        catch
        {
            // Cosmetic only — never block startup over the title bar icon.
        }
    }

    private static string ThisVersion() =>
        typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag &&
            PageMap.TryGetValue(tag, out var pageType))
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
