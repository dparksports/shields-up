using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace ShieldsUp.Converters;

/// <summary>Task state string → colored brush (ported from AutoCommand's FirewallConverters).</summary>
public sealed class TaskStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            "Running" => new SolidColorBrush(Microsoft.UI.Colors.ForestGreen),
            "Ready" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x00, 0x7A, 0xFF)),
            "Disabled" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x86, 0x86, 0x8B)),
            "Queued" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00)),
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x86, 0x86, 0x8B))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
