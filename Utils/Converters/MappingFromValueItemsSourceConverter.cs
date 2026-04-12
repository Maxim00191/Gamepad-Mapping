using System;
using System.Globalization;
using System.Windows.Data;
using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Utils;

public class MappingFromValueItemsSourceConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[1] is not MappingEditorViewModel editor)
            return Array.Empty<string>();

        if (values[0] is not GamepadBindingType kind)
            return Array.Empty<string>();

        return kind switch
        {
            GamepadBindingType.Button => editor.AvailableGamepadButtons,
            GamepadBindingType.LeftThumbstick or GamepadBindingType.RightThumbstick => editor.AvailableThumbstickFromValues,
            _ => Array.Empty<string>()
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
