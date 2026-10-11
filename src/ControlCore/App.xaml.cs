using ControlCore.Helpers;

namespace ControlCore;

public partial class App : Application
{
    public static MainWindow MainWnd { get; private set; }

    public App()
    {
        InitializeComponent();

        UnhandledException += OnWinUIUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            CrashLog.Write("AppDomain unhandled exception", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLog.Write("Unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services.ThemeService.ApplySavedTheme();
        MainWnd = new MainWindow();
        MainWnd.Activate();
    }

    private void OnWinUIUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        CrashLog.Write("XAML unhandled exception", e.Exception);
        e.Handled = true;
    }
}
