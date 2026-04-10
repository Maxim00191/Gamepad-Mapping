using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Gamepad_Mapping.Utils;

/// <summary>
/// Returns Visibility.Visible if the input string is not null or whitespace; otherwise returns Visibility.Collapsed.
/// </summary>
public class StringHasContentToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = value as string;
        return string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
