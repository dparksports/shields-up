using ControlCore.Core.Helpers;
using Microsoft.UI.Xaml.Media;

namespace ControlCore.Pages;

public sealed partial class HardeningPage : Page
{
    private const string HostsFilePath = @"C:\Windows\System32\drivers\etc\hosts";

    public HardeningPage()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAll();
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e) => await RefreshAll();

    private async Task RefreshAll()
    {
        await Task.WhenAll(
            CheckLsaStatus(),
            CheckUacStatus(),
            CheckHostsFile(),
            CheckIpv6Status()
        );
    }

    // ── IPv6 Hardening ──
    private Task CheckIpv6Status()
    {
        return Task.Run(() =>
        {
            bool isDisabled = RegistryHelper.GetIpv6DisabledStatus();
            DispatcherQueue.TryEnqueue(() =>
            {
                Ipv6StatusText.Text = isDisabled ? "✓ IPv6 is DISABLED globally" : "⚠ IPv6 is currently ENABLED";
                Ipv6StatusText.Foreground = isDisabled
                    ? new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen)
                    : new SolidColorBrush(Microsoft.UI.Colors.Orange);
            });
        });
    }

    private async void Ipv6DisableBtn_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => RegistryHelper.SetIpv6DisabledStatus(true));
        await CheckIpv6Status();
        await Helpers.Dialogs.Inform("IPv6 has been disabled via registry (DisabledComponents=0xFF). A reboot is required.", "Info");
    }

    private async void Ipv6EnableBtn_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => RegistryHelper.SetIpv6DisabledStatus(false));
        await CheckIpv6Status();
        await Helpers.Dialogs.Inform("IPv6 has been enabled. A reboot is required.", "Info");
    }

    // ── LSA Protection ──
    private Task CheckLsaStatus()
    {
        return Task.Run(() =>
        {
            bool isProtected = RegistryHelper.GetLsaProtectionStatus();
            DispatcherQueue.TryEnqueue(() =>
            {
                LsaStatusText.Text = isProtected ? "✓ LSA Protection (RunAsPPL) is ENABLED" : "⚠ LSA Protection is DISABLED";
                LsaStatusText.Foreground = isProtected
                    ? new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen)
                    : new SolidColorBrush(Microsoft.UI.Colors.Orange);
            });
        });
    }

    private async void LsaEnableBtn_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => RegistryHelper.SetLsaProtectionStatus(true));
        await CheckLsaStatus();
        await Helpers.Dialogs.Inform("LSA Protection enabled. A reboot is required for this to take effect.", "Info");
    }

    private async void LsaDisableBtn_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => RegistryHelper.SetLsaProtectionStatus(false));
        await CheckLsaStatus();
    }

    // ── UAC Strictness ──
    private Task CheckUacStatus()
    {
        return Task.Run(() =>
        {
            bool isStrict = RegistryHelper.GetUacStrictness();
            DispatcherQueue.TryEnqueue(() =>
            {
                UacStatusText.Text = isStrict ? "✓ UAC is at Maximum Strictness" : "⚠ UAC is relaxed or disabled";
                UacStatusText.Foreground = isStrict
                    ? new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen)
                    : new SolidColorBrush(Microsoft.UI.Colors.Orange);
            });
        });
    }

    private async void UacStrictBtn_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => RegistryHelper.SetUacStrictness(true));
        await CheckUacStatus();
    }

    private async void UacRelaxBtn_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => RegistryHelper.SetUacStrictness(false));
        await CheckUacStatus();
    }

    // ── Hosts File ──
    private Task CheckHostsFile()
    {
        return Task.Run(() =>
        {
            try
            {
                if (File.Exists(HostsFilePath))
                {
                    string content = File.ReadAllText(HostsFilePath);

                    var lines = File.ReadAllLines(HostsFilePath);
                    int activeRules = 0;
                    bool suspicious = false;
                    foreach (var line in lines)
                    {
                        var t = line.Trim();
                        if (string.IsNullOrWhiteSpace(t) || t.StartsWith("#")) continue;
                        activeRules++;
                        if (!t.Contains("localhost") && !t.Contains("127.0.0.1") && !t.Contains("::1"))
                        {
                            suspicious = true;
                        }
                    }

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        HostsContentBox.Text = content;
                        if (suspicious)
                        {
                            HostsStatusText.Text = $"⚠ Found {activeRules} active redirect(s). Potentially suspicious.";
                            HostsStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
                        }
                        else if (activeRules > 0)
                        {
                            HostsStatusText.Text = $"✓ Found {activeRules} standard redirect(s).";
                            HostsStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen);
                        }
                        else
                        {
                            HostsStatusText.Text = "✓ Hosts file is clean (default).";
                            HostsStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen);
                        }
                    });
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        HostsContentBox.Text = "(File not found)";
                        HostsStatusText.Text = "✓ Hosts file missing (default behavior on some systems).";
                        HostsStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen);
                    });
                }
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() => HostsStatusText.Text = $"Error reading hosts: {ex.Message}");
            }
        });
    }

    private async void HostsResetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm("Reset hosts file to Windows default? All custom entries will be lost.",
                "Confirm Reset"))
            return;

        await Task.Run(() =>
        {
            try
            {
                string defaultContent = @"# Copyright (c) 1993-2009 Microsoft Corp.
#
# This is a sample HOSTS file used by Microsoft TCP/IP for Windows.
#
# 127.0.0.1       localhost
# ::1             localhost
";
                File.WriteAllText(HostsFilePath, defaultContent);
            }
            catch { }
        });
        await CheckHostsFile();
    }
}
