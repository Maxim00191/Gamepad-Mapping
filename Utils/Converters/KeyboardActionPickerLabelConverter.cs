using System;
using System.Globalization;
using System.Windows.Data;

namespace Gamepad_Mapping.Utils;

/// <summary>
/// Formats keyboard catalog rows for combo boxes: description (or keyboard key fallback) plus id (see <c>KeyboardActionPicker_OptionFormat</c>).
/// </summary>
public sealed class KeyboardActionPickerLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4)
            return string.Empty;

        var id = values[0]?.ToString() ?? string.Empty;
        var desc = values[1]?.ToString();
        var format = values[2]?.ToString() ?? "{0} ({1})";
        var manual = values[3]?.ToString() ?? string.Empty;
        var keyFallback = values.Length > 4 ? values[4]?.ToString() : null;

        if (string.IsNullOrEmpty(id))
            return manual;

        string primaryLabel;
        if (!string.IsNullOrWhiteSpace(desc))
            primaryLabel = desc.Trim();
        else if (!string.IsNullOrWhiteSpace(keyFallback))
            primaryLabel = keyFallback.Trim();
        else
            return id;

        try
        {
            return string.Format(culture, format, primaryLabel, id);
        }
        catch (FormatException)
        {
            return $"{primaryLabel} ({id})";
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
