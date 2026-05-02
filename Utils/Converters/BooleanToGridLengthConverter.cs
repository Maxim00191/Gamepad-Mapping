using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Gamepad_Mapping.Utils;

public sealed class BooleanToGridLengthConverter : IValueConverter
{
    public string TrueLength { get; set; } = "*";
    public string FalseLength { get; set; } = "0";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isTrue = value is true;
        return ParseGridLength(isTrue ? TrueLength : FalseLength);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static GridLength ParseGridLength(string source) =>
        GridLengthConverter2.TryParse(source, out var gridLength)
            ? gridLength
            : new GridLength(0);

    private static class GridLengthConverter2
    {
        private static readonly GridLengthConverter Converter = new();

        public static bool TryParse(string source, out GridLength value)
        {
            try
            {
                var parsed = Converter.ConvertFromInvariantString(source);
                if (parsed is GridLength gridLength)
                {
                    value = gridLength;
                    return true;
                }
            }
            catch (FormatException)
            {
            }
            catch (NotSupportedException)
            {
            }

            value = default;
            return false;
        }
    }
}
