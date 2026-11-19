using Microsoft.UI.Xaml;
using ShieldsUp.Core.Services;

namespace ShieldsUp;

/// <summary>
/// UI-side of the opt-in install count: the bounded consent dialog
/// (ContentDialog rooted at the window) and the startup flow that wires it
/// to <see cref="UsagePingService"/>. The headless parts — consent state,
/// the once-per-UTC-day throttle, the random install id, the ping itself —
/// live in the Core service, which stays inert while no UI has asked.
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

            // Re-ask old "yes" answers once: rev 2's dialog discloses the
            // random install id, and consent given under the no-id payload
            // deserves the chance to be re-decided. Permanent "no" is never
            // re-asked (EnsureConsentRevision leaves it alone).
            UsagePingService.EnsureConsentRevision();

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
                   "If you opt in, it sends at most once per day — unique installs, not launches:",
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(new TextBlock
        {
            Text = "    { app: \"shields-up\", build date, Windows build, install id }",
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        });
        body.Children.Add(new TextBlock
        {
            Text = "• No hostnames, process names, or anything you do in the app.",
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(new TextBlock
        {
            Text = "• Your IP address is never stored — the server counts " +
                   "your country at the network edge and drops everything else.",
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(new TextBlock
        {
            Text = "• The install id is a random number the app invents " +
                   "itself — not from your hardware, account, or IP — so " +
                   "repeated launches count once. Delete " +
                   "HKCU\\Software\\ShieldsUp\\UsagePing anytime to withdraw " +
                   "consent and erase the id; opting back in later invents a " +
                   "fresh id no one can connect to the old one.",
            TextWrapping = TextWrapping.Wrap,
        });

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Count you in? (one random id, optional)",
            Content = body,
            PrimaryButtonText = "Yes, count me in",
            SecondaryButtonText = "No thanks",
            CloseButtonText = "Ask me later",
            DefaultButton = ContentDialogButton.Secondary,
        };
        return await dialog.ShowAsync();
    }
}
