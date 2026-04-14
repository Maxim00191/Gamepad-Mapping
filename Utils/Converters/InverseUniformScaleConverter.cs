using System;
using System.Globalization;
using System.Windows.Data;

namespace Gamepad_Mapping.Utils.Converters;

public sealed class InverseUniformScaleConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4)
            return 1d;

        var actualWidth = ToPositiveDouble(values[0]);
        var actualHeight = ToPositiveDouble(values[1]);
        var sourceWidth = ToPositiveDouble(values[2]);
        var sourceHeight = ToPositiveDouble(values[3]);

        if (actualWidth <= 0d || actualHeight <= 0d || sourceWidth <= 0d || sourceHeight <= 0d)
            return 1d;

        var uniformScale = Math.Min(actualWidth / sourceWidth, actualHeight / sourceHeight);
        if (!double.IsFinite(uniformScale) || uniformScale <= 0d)
            return 1d;

        var result = 1d / uniformScale;
        if (parameter is string s && s.Equals("Negate", StringComparison.OrdinalIgnoreCase))
            return -result;

        return result;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static double ToPositiveDouble(object value) =>
        value is double d && double.IsFinite(d) && d > 0d ? d : 0d;
}
