using GamepadMapperGUI.Models.ControllerVisual;

namespace Gamepad_Mapping.Interfaces.Services;

public interface IControllerVisualLayoutSource
{
    ControllerVisualLayoutDescriptor GetActiveLayout();
}
