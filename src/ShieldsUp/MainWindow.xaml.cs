using ShieldsUp.Pages;

namespace ShieldsUp;

public sealed partial class MainWindow : Window
{
    private static readonly Dictionary<string, Type> PageMap = new()
    {
        ["QuickScan"] = typeof(QuickScanPage),
        ["AttackSurface"] = typeof(AttackSurfacePage),
        ["Firewall"] = typeof(FirewallPage),
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
        Title = $"Shields Up v{ThisVersion()}";
        // Dark-only app frame: the dark NavigationView XAML extends into the
        // title bar area instead of the system-drawn (light) caption
        ExtendsContentIntoTitleBar = true;
        ContentFrame.Navigated += (_, _) => { };
        ConfigureWindowChrome();
        // Land on the first page instead of an empty frame
        Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
    }

    private static string ThisVersion() =>
        typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    private void ConfigureWindowChrome()
    {
        try
        {
            string ico = Path.Combine(AppContext.BaseDirectory, "Assets", "ShieldsUp.ico");
            if (!File.Exists(ico)) return;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            Microsoft.UI.Windowing.AppWindow.GetFromWindowId(wndId).SetIcon(ico);
        }
        catch
        {
            // Cosmetic only — never block startup over the window chrome.
        }
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag &&
            PageMap.TryGetValue(tag, out var pageType))
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
