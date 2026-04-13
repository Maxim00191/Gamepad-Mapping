using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Gamepad_Mapping.Utils.Converters;

public class DimmingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isDimmed)
        {
            return isDimmed ? 0.3 : 1.0;
        }
        return 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
