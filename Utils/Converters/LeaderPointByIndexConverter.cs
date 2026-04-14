using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Gamepad_Mapping.Utils.Converters;

public class LeaderPointByIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Point[] points && parameter is string indexStr && int.TryParse(indexStr, out var index))
        {
            if (index < 0) index = points.Length + index;
            if (index >= 0 && index < points.Length)
            {
                return points[index];
            }
        }
        return new Point(0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}
