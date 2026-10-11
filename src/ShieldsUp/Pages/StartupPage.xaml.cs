using ShieldsUp.Core.Helpers;
using ShieldsUp.Core.Models;

namespace ShieldsUp.Pages;

public sealed partial class StartupPage : Page
{
    private List<StartupItem> _items = new();

    public StartupPage()
    {
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadItems();
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e) => await LoadItems();

    private async Task LoadItems()
    {
        _items = await Task.Run(() => RegistryHelper.GetStartupItems());
        DispatcherQueue.TryEnqueue(() => StartupGrid.ItemsSource = _items);
    }

    private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        if (StartupGrid.SelectedItem is not StartupItem item) return;

        if (!await Helpers.Dialogs.Confirm(
                $"Delete startup entry '{item.Name}' from {item.Location}?\n\nTarget: {item.Value}",
                "Confirm Delete"))
            return;

        await Task.Run(() => RegistryHelper.DeleteStartupItem(item));
        await LoadItems();
    }
}
