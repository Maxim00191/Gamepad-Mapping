using System;
using System.Globalization;
using System.Windows.Data;

namespace Gamepad_Mapping.Utils;

/// <summary>
/// Formats keyboard catalog rows for combo boxes: description (or keyboard key fallback) plus id (see <c>KeyboardActionPicker_OptionFormat</c>).
/// </summary>
public sealed class KeyboardActionPickerLabelConverter : IMultiValueConverter
{
    public static string FormatLabel(
        string? id,
        string? description,
        string? format,
        string? manual,
        string? keyFallback,
        CultureInfo culture)
    {
        var normalizedId = id ?? string.Empty;
        var normalizedFormat = format ?? "{0} ({1})";
        var normalizedManual = manual ?? string.Empty;

        if (string.IsNullOrEmpty(normalizedId))
            return normalizedManual;

        string primaryLabel;
        if (!string.IsNullOrWhiteSpace(description))
            primaryLabel = description.Trim();
        else if (!string.IsNullOrWhiteSpace(keyFallback))
            primaryLabel = keyFallback.Trim();
        else
            return normalizedId;

        try
        {
            return string.Format(culture, normalizedFormat, primaryLabel, normalizedId);
        }
        catch (FormatException)
        {
            return $"{primaryLabel} ({normalizedId})";
        }
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4)
            return string.Empty;

        var id = values[0]?.ToString();
        var desc = values[1]?.ToString();
        var format = values[2]?.ToString();
        var manual = values[3]?.ToString();
        var keyFallback = values.Length > 4 ? values[4]?.ToString() : null;
        return FormatLabel(id, desc, format, manual, keyFallback, culture);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
