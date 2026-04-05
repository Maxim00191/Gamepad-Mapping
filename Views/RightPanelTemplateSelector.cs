#nullable enable

using System.Windows;
using System.Windows.Controls;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Views;

public sealed class RightPanelTemplateSelector : DataTemplateSelector
{
    public DataTemplate? MappingTemplate { get; set; }

    public DataTemplate? KeyboardActionTemplate { get; set; }

    public DataTemplate? RadialMenuTemplate { get; set; }

    public DataTemplate? PlaceholderTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is ProfileRightPanelSurface surface)
        {
            return surface switch
            {
                ProfileRightPanelSurface.Mapping => MappingTemplate,
                ProfileRightPanelSurface.KeyboardAction => KeyboardActionTemplate,
                ProfileRightPanelSurface.RadialMenu => RadialMenuTemplate,
                _ => PlaceholderTemplate,
            };
        }

        return PlaceholderTemplate;
    }
}
