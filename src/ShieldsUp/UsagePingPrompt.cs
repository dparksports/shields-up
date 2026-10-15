using Microsoft.UI.Xaml;
using ShieldsUp.Core.Services;

namespace ShieldsUp;

/// <summary>
/// UI-side of the anonymous usage count: the bounded consent dialog
/// (ContentDialog rooted at the window) and the startup flow that wires it
/// to <see cref="UsagePingService"/>. The headless parts — consent state,
/// the once-per-UTC-day throttle, the ping itself — live in the Core
/// service, which stays inert while no UI has asked.
/// </summary>
public static class UsagePingPrompt
{
    /// <summary>Offer the count (max 3 times, ever), then ping if consented.
    /// Call once after the window is up, on the UI thread. Inert while
    /// UsagePingService.Endpoint is unconfigured.</summary>
    public static async System.Threading.Tasks.Task RunStartupFlowAsync(XamlRoot xamlRoot)
    {
        try
        {
            if (string.IsNullOrEmpty(UsagePingService.Endpoint)) return;

            var consent = UsagePingService.Consent;
            if (consent == "ask")
            {
                if (UsagePingService.AskCount >= UsagePingService.MaxAsks) return; // asked enough — never again

                var answer = await AskConsentAsync(xamlRoot);
                consent = answer switch
                {
                    ContentDialogResult.Primary => "yes",
                    ContentDialogResult.Secondary => "no",
                    _ => "ask", // dismissed — counts as an ask, stays "ask"
                };
                UsagePingService.SetConsent(consent);
            }

            if (consent == "yes")
                await UsagePingService.SendDailyPingAsync();
        }
        catch { /* the count must never break startup — silently fine */ }
    }

    private static async Task<ContentDialogResult> AskConsentAsync(XamlRoot xamlRoot)
    {
        var body = new StackPanel { Spacing = 8, MaxWidth = 420 };
        body.Children.Add(new TextBlock
        {
            Text = "Shields Up sends nothing — unless you say yes here. " +
                   "If you opt in, it sends at most once per day:",
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(new TextBlock
        {
            Text = "    { app: \"shields-up\", build date, Windows build }",
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        });
        body.Children.Add(new TextBlock
        {
            Text = "That is the entire payload. No identifier of any kind — " +
                   "not even a random install id — and your IP address is " +
                   "never stored. It adds 1 to an anonymous daily count you " +
                   "can watch live on the community graph page. Withdraw " +
                   "anytime by deleting HKCU\\Software\\ShieldsUp\\UsagePing.",
            TextWrapping = TextWrapping.Wrap,
        });

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Count you in? (anonymous, optional)",
            Content = body,
            PrimaryButtonText = "Yes, count me in",
            SecondaryButtonText = "No thanks",
            CloseButtonText = "Ask me later",
            DefaultButton = ContentDialogButton.Secondary,
        };
        return await dialog.ShowAsync();
    }
}
