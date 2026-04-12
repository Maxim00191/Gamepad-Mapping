using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.Utils;

public class GamepadBindingTypeToLocalizationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is GamepadBindingType t)
        {
            if (Application.Current.Resources["Loc"] is TranslationService loc)
                return loc[$"GamepadBindingType_{t}"];
            return t.ToString();
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
