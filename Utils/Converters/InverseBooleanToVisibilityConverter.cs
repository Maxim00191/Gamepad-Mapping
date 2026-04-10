using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Gamepad_Mapping.Utils;

/// <summary>
/// Converts a boolean value to Visibility. Inverts the result if the parameter is "Inverse".
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = (bool)value;
        bool inverse = parameter as string == "Inverse";

        if (inverse)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
