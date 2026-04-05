using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services;

namespace Gamepad_Mapping.Utils;

public sealed class KeyboardCatalogOutputKindToLocalizationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is KeyboardCatalogOutputKind kind)
        {
            var loc = Application.Current.Resources["Loc"] as TranslationService;
            if (loc != null)
                return loc[$"CatalogOutputKind_{kind}"];
            return kind.ToString();
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
