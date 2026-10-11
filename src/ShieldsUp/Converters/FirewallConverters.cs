using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace ShieldsUp.Converters;

/// <summary>Rule enabled Yes/No → green/gray (port of AutoCommand's FirewallConverters).</summary>
public sealed class EnabledToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true
            ? new SolidColorBrush(Microsoft.UI.Colors.ForestGreen)
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x86, 0x86, 0x8B));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Allow/Block action → green/red.</summary>
public sealed class ActionToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value as string == "Allow"
            ? new SolidColorBrush(Microsoft.UI.Colors.ForestGreen)
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xD7, 0x00, 0x15));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
