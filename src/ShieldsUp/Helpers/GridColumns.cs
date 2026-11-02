using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;

namespace ShieldsUp.Helpers;

/// <summary>
/// Column priority for the DataGrids. At 800x600 the content area is roughly
/// 704 DIPs wide — less than the fixed column widths the grids were built
/// with — so secondary columns are dropped (not squeezed) below set widths.
/// The identity/state columns always survive; the rest come back as soon as
/// the window is wide enough again.
/// </summary>
internal static class GridColumns
{
    public static void Apply(DataGrid grid, double pageWidth, params (string Header, double MinWidth)[] rules)
    {
        if (grid is null || rules is null) return;
        foreach (var rule in rules)
        {
            foreach (var column in grid.Columns)
            {
                if (!string.Equals(column.Header as string, rule.Header, StringComparison.Ordinal)) continue;
                column.Visibility = pageWidth >= rule.MinWidth ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
