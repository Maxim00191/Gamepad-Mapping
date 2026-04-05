using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services;

namespace Gamepad_Mapping.Utils;

/// <summary>
/// Converts a TriggerMoment enum value to its localized string.
/// </summary>
public class TriggerMomentToLocalizationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TriggerMoment trigger)
        {
            var loc = Application.Current.Resources["Loc"] as TranslationService;
            if (loc != null)
            {
                return loc[$"TriggerMoment_{trigger}"];
            }
            return trigger.ToString();
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
