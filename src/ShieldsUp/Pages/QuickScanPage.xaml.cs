using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using ShieldsUp.Core.Services;
using Microsoft.UI.Xaml.Media;

namespace ShieldsUp.Pages;

public sealed partial class QuickScanPage : Page
{
    private readonly ObservableCollection<SetupStepItem> _items = new();
    private bool _running;

    public QuickScanPage()
    {
        InitializeComponent();
        StepsList.ItemsSource = _items;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0)
        {
            foreach (var step in QuickScanService.Steps)
                _items.Add(new SetupStepItem(step));
            await VerifyAllAsync();
        }
    }

    private async Task VerifyAllAsync()
    {
        SetupStatusText.Text = "Checking current state…";
        var checks = _items.Select(async item =>
        {
            var (ok, detail) = await item.Step.Verify();
            item.SetResult(ok, detail);
        });
        await Task.WhenAll(checks);
        int okCount = _items.Count(i => i.IsOk);
        SetupStatusText.Text = $"{okCount} of {_items.Count} steps already applied. Press Apply to complete the rest.";
    }

    private async void ApplyAllBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;

        var sb = new StringBuilder();
        sb.AppendLine("This will apply ALL of the following in one pass:\n");
        foreach (var item in _items)
            sb.AppendLine($"• {item.Title}");
        sb.AppendLine("\nDevices, firewall rules, apps and system settings will be modified. Continue?");

        if (!await Helpers.Dialogs.Confirm(sb.ToString(), "Confirm One-Click Setup", defaultIsPrimary: false))
            return;

        _running = true;
        ApplyAllBtn.IsEnabled = false;
        OverallProgress.Maximum = _items.Count;
        OverallProgress.Value = 0;

        int succeeded = 0;
        var failed = new List<string>();
        var progress = new Progress<string>(msg => DispatcherQueue.TryEnqueue(() => SetupStatusText.Text = msg));

        try
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                item.SetRunning();
                OverallProgress.Value = i;

                try
                {
                    var (success, detail) = await item.Step.Apply(progress);
                    item.SetResult(success, detail);
                    if (success) succeeded++;
                    else failed.Add(item.Title);
                }
                catch (Exception ex)
                {
                    item.SetResult(false, $"error: {ex.Message}");
                    failed.Add(item.Title);
                }

                OverallProgress.Value = i + 1;
            }

            // Bird's-eye confirmation: re-read live state for every step
            await VerifyAllAsync();

            SetupStatusText.Text = $"Complete — {succeeded}/{_items.Count} steps applied"
                                   + (failed.Count > 0 ? $"; failed: {string.Join(", ", failed)}" : "")
                                   + ". Reboot recommended.";

            if (failed.Count > 0)
                await Helpers.Dialogs.Error(
                    $"Applied {succeeded} of {_items.Count} steps.\n\nFailed:\n- {string.Join("\n- ", failed)}",
                    "One-Click Setup Complete");
            else
                await Helpers.Dialogs.Inform(
                    $"Applied {succeeded} of {_items.Count} steps.\n\nA reboot is recommended so firewall, IPv6, UAC and device changes fully take effect.",
                    "One-Click Setup Complete");
        }
        finally
        {
            _running = false;
            ApplyAllBtn.IsEnabled = true;
        }
    }

    /// <summary>Bindable wrapper around a SetupStep for the checklist UI.</summary>
    public class SetupStepItem : INotifyPropertyChanged
    {
        private static readonly Brush GrayBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray);
        private static readonly Brush GreenBrush = new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen);
        private static readonly Brush AmberBrush = new SolidColorBrush(Microsoft.UI.Colors.Orange);

        public SetupStep Step { get; }

        public SetupStepItem(SetupStep step)
        {
            Step = step;
            _icon = "•";
            _statusText = "Checking…";
            _statusBrush = GrayBrush;
        }

        public string Title => Step.Title;
        public string Description => Step.Description;

        private string _icon;
        public string Icon
        {
            get => _icon;
            private set { _icon = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon))); }
        }

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText))); }
        }

        private Brush _statusBrush;
        public Brush StatusBrush
        {
            get => _statusBrush;
            private set { _statusBrush = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusBrush))); }
        }

        public bool IsOk { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void SetRunning()
        {
            Icon = "⏳";
            StatusText = "Running…";
            StatusBrush = GrayBrush;
        }

        public void SetResult(bool ok, string detail)
        {
            IsOk = ok;
            Icon = ok ? "✓" : "⚠";
            StatusText = detail ?? (ok ? "OK" : "not applied");
            StatusBrush = ok ? GreenBrush : AmberBrush;
        }
    }
}
