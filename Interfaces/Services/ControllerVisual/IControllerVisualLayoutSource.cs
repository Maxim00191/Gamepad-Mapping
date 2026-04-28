using GamepadMapperGUI.Models.ControllerVisual;

namespace Gamepad_Mapping.Interfaces.Services.ControllerVisual;

public interface IControllerVisualLayoutSource
{
    ControllerVisualLayoutDescriptor GetActiveLayout();

    ControllerVisualLayoutDescriptor GetLayoutForGamepadApi(string? gamepadApiId);
}
