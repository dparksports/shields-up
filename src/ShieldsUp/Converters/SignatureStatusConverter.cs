using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace ShieldsUp.Converters;

/// <summary>Package signature status string → colored brush (replaces the WPF Style.Triggers column).</summary>
public sealed class SignatureStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            "Valid (Store)" => new SolidColorBrush(Microsoft.UI.Colors.ForestGreen),
            "Valid (Enterprise)" => new SolidColorBrush(Microsoft.UI.Colors.ForestGreen),
            "System component" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x86, 0x86, 0x8B)),
            "Developer-signed" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00)),
            "Unsigned" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xD7, 0x00, 0x15)),
            "Invalid/Error" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xD7, 0x00, 0x15)),
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x86, 0x86, 0x8B))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
