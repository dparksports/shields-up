using Microsoft.UI.Xaml.Input;
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

    /// <summary>
    /// True when the display's work area is too small for the wide layout
    /// (the 800x600 / 1024x768-class desktop Windows comes up on before the
    /// GPU driver lands, or a high-DPI TV panel). Pages read this to explain
    /// the compact presentation instead of looking broken.
    /// </summary>
    public bool IsSmallDisplay { get; private set; }

    /// <summary>Work area in DIPs, e.g. "800×560" — used by the first-run notice.</summary>
    public string WorkAreaText { get; private set; } = "";

    /// <summary>Page titles for the Ctrl+K switcher (the pane is a rail when compact).</summary>
    private static readonly (string Tag, string Title)[] PageTitles =
    {
        ("QuickScan", "Quick Scan"),
        ("AttackSurface", "Attack Surface"),
        ("Firewall", "Firewall"),
        ("Bloatware", "Bloatware"),
        ("Telemetry", "Telemetry"),
        ("Privacy", "Privacy"),
        ("UpdateControl", "Update Control"),
        ("Tasks", "Tasks"),
        ("Startup", "Startup"),
        ("Hibernation", "Hibernation"),
        ("Hardening", "OS Hardening"),
        ("DbxSafety", "DBX Safety"),
        ("Unblock", "Unblock"),
        ("Settings", "Settings"),
    };

    private int _navigations;
    private bool _smallDisplayConsentOffered;

    public MainWindow()
    {
        InitializeComponent();
        Title = $"Shields Up v{ThisVersion()}";
        // Light theme: the white NavigationView XAML extends into the title bar
        // area instead of the system-drawn caption
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        // Some pages' visual trees aren't realized at Navigated-time — retry
        // once Loaded fires so their panels get shadows too
        ContentFrame.Navigated += OnNavigated;
        Closed += (_, _) => SaveWindowSize();
        ConfigureWindowChrome();
        // Land on the first page instead of an empty frame
        Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
        Core.Services.AppLogger.Log("shell", "MainWindow created");

        // Anonymous usage count (opt-in, off by default): the consent dialog
        // is offered here (max 3 times, ever), and consenting installs ping
        // once a day. Inert while UsagePingService.Endpoint is unconfigured.
        // Spec + server code: telemetry/ in the repository.
        //
        // On a small display the dialog is deliberately NOT raised on launch:
        // it would cover most of a 800x600-class window before the user has
        // done anything. It is offered once, on the first navigation away
        // from Quick Scan instead (see OnNavigated).
        ContentFrame.Loaded += async (_, _) =>
        {
            if (IsSmallDisplay) return;
            await UsagePingPrompt.RunStartupFlowAsync(ContentFrame.XamlRoot);
        };
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        if (ContentFrame.Content is FrameworkElement fe && !fe.IsLoaded)
            fe.Loaded += OnPageLoadedForElevation;
        else
            ApplyPanelElevation();

        // Small displays: don't interrupt first launch with a modal. Offer the
        // (optional, revocable) usage count once the user has started moving
        // around the app instead.
        if (IsSmallDisplay && !_smallDisplayConsentOffered && ++_navigations >= 2)
        {
            _smallDisplayConsentOffered = true;
            _ = OfferConsentAfterFirstUseAsync();
        }
    }

    private async Task OfferConsentAfterFirstUseAsync()
    {
        try
        {
            if (ContentFrame.XamlRoot is null) return;
            await UsagePingPrompt.RunStartupFlowAsync(ContentFrame.XamlRoot);
        }
        catch (Exception ex)
        {
            Core.Services.AppLogger.LogException("usage ping offer (deferred)", ex);
        }
    }

    private void OnPageLoadedForElevation(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
            fe.Loaded -= OnPageLoadedForElevation;
        ApplyPanelElevation();
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

    private const string WindowRegKey = @"Software\ShieldsUp\Window";
    // Sizes are persisted in DIPs so a window restored on a different scaling
    // factor keeps its apparent size. The pixel-valued names are what older
    // builds wrote; they are read once, then rewritten in DIPs.
    private const string WidthDipValue = "WidthDip";
    private const string HeightDipValue = "HeightDip";
    private const string LegacyWidthValue = "Width";
    private const string LegacyHeightValue = "Height";

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    private static double ScaleFor(IntPtr hWnd)
    {
        try
        {
            uint dpi = GetDpiForWindow(hWnd);
            return dpi > 0 ? dpi / 96.0 : 1.0;
        }
        catch
        {
            return 1.0;
        }
    }

    private void ConfigureWindowChrome()
    {
        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWnd = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(wndId);
            double scale = ScaleFor(hWnd);

            // Work area, not screen: the taskbar is not ours to draw over, and
            // a docked/undocked monitor can change this between sessions.
            var display = Microsoft.UI.Windowing.DisplayArea
                .GetFromWindowId(wndId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
            if (display is null)
            {
                // No display information: fall back to the old restore path
                // rather than not sizing the window at all.
                var (fallbackWidth, fallbackHeight) = LoadSavedSizeDip(scale);
                appWnd.Resize(new Windows.Graphics.SizeInt32
                {
                    Width = Math.Max(720, fallbackWidth ?? 1284),
                    Height = Math.Max(520, fallbackHeight ?? 850),
                });
                SetIconAndCaptionColors(appWnd);
                return;
            }

            var area = display.WorkArea;
            int workWidthDip = (int)Math.Floor(area.Width / scale);
            int workHeightDip = (int)Math.Floor(area.Height / scale);

            // Never request more than the desktop can show. A fresh Windows
            // install is the worst case (basic display adapter: 800x600), and
            // a 1284x850 request there puts the caption buttons off-screen.
            var (savedWidth, savedHeight) = LoadSavedSizeDip(scale);
            var plan = Core.Layout.WindowPlacement.Compute(workWidthDip, workHeightDip, savedWidth, savedHeight);

            int widthPx = (int)Math.Round(plan.Width * scale);
            int heightPx = (int)Math.Round(plan.Height * scale);
            appWnd.Resize(new Windows.Graphics.SizeInt32 { Width = widthPx, Height = heightPx });
            appWnd.Move(new Windows.Graphics.PointInt32
            {
                X = area.X + Math.Max(0, (area.Width - widthPx) / 2),
                Y = area.Y + Math.Max(0, (area.Height - heightPx) / 2),
            });

            // Same rule that decided to open maximized: the desktop is too
            // small for the wide layout, so pages may wrap/hide columns.
            IsSmallDisplay = plan.Maximize;
            WorkAreaText = $"{workWidthDip}×{workHeightDip}";

            try
            {
                if (appWnd.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    // The floor used to be a hard 980x620 — above an 800x600
                    // screen, so the window could never be made to fit. Clamp
                    // it to the work area; compact displays open maximized.
                    presenter.PreferredMinimumWidth = Math.Min(Core.Layout.WindowPlacement.MinWidth, workWidthDip);
                    presenter.PreferredMinimumHeight = Math.Min(Core.Layout.WindowPlacement.MinHeight, workHeightDip);
                    if (plan.Maximize) presenter.Maximize();
                }
            }
            catch
            {
                // Min-size clamps and the maximize hint are niceties, not requirements.
            }

            SetIconAndCaptionColors(appWnd);

            Core.Services.AppLogger.Log("shell",
                $"window placed {widthPx}x{heightPx}px at {scale:P0} scale, work area {WorkAreaText} DIPs" +
                (plan.Maximize ? " (maximized: compact display)" : ""));
        }
        catch
        {
            // Cosmetic only — never block startup over the window chrome.
        }
    }

    /// <summary>
    /// Icon + caption-button palette. Split out so the no-display fallback
    /// still gets the light chrome.
    /// </summary>
    private static void SetIconAndCaptionColors(Microsoft.UI.Windowing.AppWindow appWnd)
    {
        try
        {
            string ico = Path.Combine(AppContext.BaseDirectory, "Assets", "ShieldsUp.ico");
            if (File.Exists(ico)) appWnd.SetIcon(ico);

            // Caption buttons (minimize / maximize / close) are system-drawn
            // overlays whose GLYPH color otherwise follows the OS theme. The
            // title bar below them is hard-coded white, so on a dark-theme
            // Windows session they rendered white-on-white — invisible until
            // hover painted a background behind them. Pin every state to the
            // app's light palette so the buttons are always drawn for the
            // chrome they actually sit on.
            var titleBar = appWnd.TitleBar;
            var glyph = Windows.UI.Color.FromArgb(0xFF, 0x1D, 0x1D, 0x1F);   // the title text's near-black
            titleBar.ButtonForegroundColor = glyph;
            titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);  // white bar shows through
            titleBar.ButtonHoverForegroundColor = glyph;
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(0xFF, 0xE9, 0xE9, 0xE9);
            titleBar.ButtonPressedForegroundColor = glyph;
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(0xFF, 0xD9, 0xD9, 0xD9);
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A);
            titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        }
        catch
        {
            // Cosmetic only.
        }
    }

    private (int? Width, int? Height) LoadSavedSizeDip(double scale)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(WindowRegKey);
            if (key is null) return (null, null);
            if (key.GetValue(WidthDipValue) is int w && key.GetValue(HeightDipValue) is int h && w > 0 && h > 0)
                return (w, h);
            if (key.GetValue(LegacyWidthValue) is int pw && key.GetValue(LegacyHeightValue) is int ph && pw > 0 && ph > 0)
                return ((int)Math.Round(pw / scale), (int)Math.Round(ph / scale));
        }
        catch { /* fall through to the design size */ }
        return (null, null);
    }

    private void SaveWindowSize()
    {
        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWnd = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(wndId);
            double scale = ScaleFor(hWnd);

            // A maximized exit must not turn the restored (normal) size into
            // a full-monitor window — keep the last normal size instead.
            // (AppWindow.State is absent from this WinAppSDK projection;
            // OverlappedPresenter.State carries the same information.)
            if (appWnd.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter &&
                presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
                return;

            int widthDip = Math.Max(1, (int)Math.Round(appWnd.Size.Width / scale));
            int heightDip = Math.Max(1, (int)Math.Round(appWnd.Size.Height / scale));
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(WindowRegKey);
            key.SetValue(WidthDipValue, widthDip, Microsoft.Win32.RegistryValueKind.DWord);
            key.SetValue(HeightDipValue, heightDip, Microsoft.Win32.RegistryValueKind.DWord);
            Core.Services.AppLogger.Log("shell", $"window size saved: {widthDip}x{heightDip} DIPs");
        }
        catch
        {
            // Persistence is cosmetic — never block exit.
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

    /// <summary>
    /// Ctrl+K. On displays narrow enough for the pane to collapse to an icon
    /// rail, this is the fastest way to reach any of the fourteen pages — and
    /// the only way that does not depend on how much width the rail eats.
    /// </summary>
    private async void QuickSwitch_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        try
        {
            var tag = await Helpers.PageSwitcher.ShowAsync(Nav.XamlRoot, PageTitles);
            if (string.IsNullOrEmpty(tag)) return;

            var item = Nav.MenuItems.OfType<NavigationViewItem>()
                .Concat(Nav.FooterMenuItems.OfType<NavigationViewItem>())
                .FirstOrDefault(i => (i.Tag as string) == tag);
            if (item is not null) Nav.SelectedItem = item;
        }
        catch (Exception ex)
        {
            Core.Services.AppLogger.LogException("page switcher", ex);
        }
    }
}
