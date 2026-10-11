using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace ControlCore.Converters;

/// <summary>Package signature status string → colored brush (replaces the WPF Style.Triggers column).</summary>
public sealed class SignatureStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            "Valid (Store)" => new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen),
            "Valid (Enterprise)" => new SolidColorBrush(Microsoft.UI.Colors.MediumSeaGreen),
            "System component" => new SolidColorBrush(Microsoft.UI.Colors.Gray),
            "Developer-signed" => new SolidColorBrush(Microsoft.UI.Colors.Goldenrod),
            "Unsigned" => new SolidColorBrush(Microsoft.UI.Colors.OrangeRed),
            "Invalid/Error" => new SolidColorBrush(Microsoft.UI.Colors.OrangeRed),
            _ => new SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
