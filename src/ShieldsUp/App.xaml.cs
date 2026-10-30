using ShieldsUp.Core.Agent;
using ShieldsUp.Core.Services;

namespace ShieldsUp;

public partial class App : Application
{
    public static MainWindow MainWnd { get; private set; }

    public App()
    {
        InitializeComponent();

        AppLogger.SessionHeader();
        AppLogger.EnsureCrashDumps();

        // The AI-access bridge engine (AutoCommand III's Engines.Bridge) under
        // Shields Up's identity, before anything can start a session. The
        // surface this app publishes is read-only; sessions are started from
        // Settings → AI access.
        ShieldsUpAgentBridge.Wire();

        UnhandledException += OnWinUIUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLogger.LogException("AppDomain unhandled exception",
                e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "unknown"));
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLogger.LogException("Unobserved task exception", e.Exception);
            e.SetObserved();
        };
        AppLogger.Log("startup", "exception handlers attached");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppLogger.Log("startup", "OnLaunched — creating MainWindow");
        MainWnd = new MainWindow();
        // a live AI session must never outlive the window that owns it: the
        // session file is what grants access, so it dies with the window
        MainWnd.Closed += (_, _) => ShieldsUpAgentBridge.Stop();
        MainWnd.Activate();
        AppLogger.Log("startup", "MainWindow activated");
    }

    private void OnWinUIUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        AppLogger.LogException($"XAML unhandled exception: {e.Message}", e.Exception);
        e.Handled = true;
    }
}
