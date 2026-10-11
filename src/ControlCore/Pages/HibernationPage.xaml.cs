using ControlCore.Core.Helpers;
using Microsoft.UI.Xaml.Media;

namespace ControlCore.Pages;

public sealed partial class HibernationPage : Page
{
    public HibernationPage()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await CheckHibernateStatus();
    }

    private async Task CheckHibernateStatus()
    {
        string output = await ProcessRunner.RunAsync("powercfg", "/a");

        bool hibernateAvailable = output.Contains("Hibernate") && !output.Contains("Hibernate is not available");
        bool hibernateEnabled = !output.Contains("Hibernation has not been enabled") && hibernateAvailable;

        DispatcherQueue.TryEnqueue(() =>
        {
            if (hibernateEnabled)
            {
                HibernateStatusText.Text = "⚠ Hibernate is ENABLED — hiberfile exists on disk";
                HibernateStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);
            }
            else
            {
                HibernateStatusText.Text = "✓ Hibernate is disabled";
                HibernateStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen);
            }

            PowerStatesText.Text = output.Trim();
        });
    }

    private async void DisableHibernateBtn_Click(object sender, RoutedEventArgs e)
    {
        await ProcessRunner.RunAsync("powercfg", "/hibernate off");
        await CheckHibernateStatus();
    }

    private async void EnableHibernateBtn_Click(object sender, RoutedEventArgs e)
    {
        await ProcessRunner.RunAsync("powercfg", "/hibernate on");
        await CheckHibernateStatus();
    }

    private async void TrueShutdownBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm("Perform a true shutdown? All unsaved work will be lost.",
                "Confirm Shutdown"))
            return;

        ProcessRunner.RunDetached("shutdown", "/s /t 0");
    }
}
