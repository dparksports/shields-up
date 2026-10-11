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
        ["Unblock"] = typeof(UnblockPage),
        ["Settings"] = typeof(SettingsPage),
    };

    public Frame Frame => ContentFrame;

    public MainWindow()
    {
        InitializeComponent();
        Title = $"Shields Up v{ThisVersion()}";
        // Light theme: the white NavigationView XAML extends into the title bar
        // area instead of the system-drawn caption
        ExtendsContentIntoTitleBar = true;
        ContentFrame.Navigated += (_, _) => ApplyPanelElevation();
        ConfigureWindowChrome();
        // Land on the first page instead of an empty frame
        Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
        Core.Services.AppLogger.Log("shell", "MainWindow created");
    }

    /// <summary>
    /// Gives every ContentPanel its rounded-corner drop shadow after each
    /// navigation. Each panel gets its OWN ThemeShadow instance (a shared one
    /// from a style setter crashes natively once receivers are registered),
    /// a small z-translation for depth, and the page root as the shadow
    /// receiver an in-tree shadow needs in order to render. Any failure here
    /// must degrade to "no shadow", never to a crash.
    /// </summary>
    private void ApplyPanelElevation()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ContentFrame.Content is not FrameworkElement root) return;
            var panelStyle = (Style)Application.Current.Resources["ContentPanel"];
            int elevated = 0;
            foreach (var border in FindDescendants<Border>(root))
            {
                if (!ReferenceEquals(border.Style, panelStyle)) continue;
                try
                {
                    var shadow = new ThemeShadow();
                    shadow.Receivers.Add(root);
                    border.Shadow = shadow;
                    border.Translation = new System.Numerics.Vector3(0, 0, 8);
                    elevated++;
                }
                catch (Exception ex)
                {
                    // Shadow is cosmetic — log and continue, never crash.
                    Core.Services.AppLogger.LogException("panel shadow (skipped)", ex);
                }
            }
            Core.Services.AppLogger.Log("shell", $"page '{root.GetType().Name}' — {elevated} content panel(s) elevated");
        });
    }

    private static IEnumerable<T> FindDescendants<T>(Microsoft.UI.Xaml.DependencyObject root)
        where T : Microsoft.UI.Xaml.DependencyObject
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T matched) yield return matched;
            foreach (var descendant in FindDescendants<T>(child))
                yield return descendant;
        }
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
