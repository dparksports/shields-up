using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ShieldsUp.Converters;

/// <summary>
/// Shows a control only when the bound flag is true. Used by the Bloatware
/// page's optional-apps list: rows that cannot be removed (not installed, or
/// an OS component such as mstsc.exe) get no Remove button at all rather than
/// a disabled one that still looks actionable.
/// </summary>
public sealed class TrueToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
