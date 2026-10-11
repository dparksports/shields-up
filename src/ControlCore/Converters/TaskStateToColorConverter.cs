using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace ControlCore.Converters;

/// <summary>Task state string → colored brush (ported from AutoCommand's FirewallConverters).</summary>
public sealed class TaskStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            "Running" => new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen),
            "Ready" => new SolidColorBrush(Microsoft.UI.Colors.LightSkyBlue),
            "Disabled" => new SolidColorBrush(Microsoft.UI.Colors.Gray),
            "Queued" => new SolidColorBrush(Microsoft.UI.Colors.Goldenrod),
            _ => new SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
