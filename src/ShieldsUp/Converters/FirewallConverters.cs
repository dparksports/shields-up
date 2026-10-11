using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace ShieldsUp.Converters;

/// <summary>Rule enabled Yes/No → green/gray (port of AutoCommand's FirewallConverters).</summary>
public sealed class EnabledToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true
            ? new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen)
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);
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
            ? new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen)
            : new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
