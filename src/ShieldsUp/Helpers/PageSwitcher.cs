using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ShieldsUp.Helpers;

/// <summary>
/// The Ctrl+K page switcher. When the window is narrow the navigation pane
/// collapses to an icon rail (and below 640 DIPs to a hamburger), so the
/// switcher is what keeps all fourteen pages one keystroke away — no pane
/// width required, no scrolling through a flyout.
/// </summary>
internal static class PageSwitcher
{
    public static async Task<string> ShowAsync(XamlRoot root, IReadOnlyList<(string Tag, string Title)> pages)
    {
        if (root is null || pages is null || pages.Count == 0) return null;

        var list = new ListView
        {
            Height = 300,
            SelectionMode = ListViewSelectionMode.Single,
            IsItemClickEnabled = true,
        };
        var search = new TextBox
        {
            PlaceholderText = "Type to filter pages",
            Margin = new Thickness(0, 0, 0, 8),
        };

        void Fill(string query)
        {
            list.Items.Clear();
            foreach (var page in pages)
            {
                if (!string.IsNullOrEmpty(query) &&
                    page.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                list.Items.Add(new ListViewItem { Content = page.Title, Tag = page.Tag });
            }
            if (list.Items.Count > 0) list.SelectedIndex = 0;
        }

        Fill("");

        string picked = null;
        void Pick()
        {
            picked = (list.SelectedItem as ListViewItem)?.Tag as string;
            if (picked is null && list.Items.Count > 0)
                picked = (list.Items[0] as ListViewItem)?.Tag as string;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Go to page",
            Content = new StackPanel { Children = { search, list } },
            PrimaryButtonText = "Open",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        search.TextChanged += (_, _) => Fill(search.Text ?? "");
        search.KeyDown += (_, e) =>
        {
            if (e.Key != Windows.System.VirtualKey.Enter) return;
            Pick();
            if (picked is null) return;
            e.Handled = true;
            dialog.Hide();
        };
        list.ItemClick += (_, _) =>
        {
            Pick();
            if (picked is not null) dialog.Hide();
        };
        dialog.PrimaryButtonClick += (_, e) =>
        {
            Pick();
            if (picked is null) e.Cancel = true;   // nothing matched: stay open
        };
        dialog.Opened += (_, _) => search.Focus(FocusState.Programmatic);

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? picked : null;
    }
}
