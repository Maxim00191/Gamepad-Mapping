using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;

namespace Gamepad_Mapping.Utils;

/// <summary>
/// Converts a MappingActionType enum value to its localized string.
/// </summary>
public class MappingActionTypeToLocalizationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MappingActionType actionType)
        {
            var loc = Application.Current.Resources["Loc"] as TranslationService;
            if (loc != null)
            {
                return loc[$"ActionType_{actionType}"];
            }
            return actionType.ToString();
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

