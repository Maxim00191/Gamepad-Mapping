using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using GamepadMapperGUI.Models.Core.Community;

namespace Gamepad_Mapping.Utils.Converters;

public sealed class CommunityTemplateComplianceSeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CommunityTemplateComplianceSeverity s)
            return Brushes.Gray;

        return s switch
        {
            CommunityTemplateComplianceSeverity.Ok =>
                Application.Current?.TryFindResource("AppAccentBrush") as Brush ?? Brushes.SteelBlue,
            CommunityTemplateComplianceSeverity.Warning =>
                Application.Current?.TryFindResource("AppAccentTextBrush") as Brush ?? Brushes.DarkOrange,
            _ =>
                Application.Current?.TryFindResource("AppAccentTextBrush") as Brush ?? Brushes.DarkRed
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
