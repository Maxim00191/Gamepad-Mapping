using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Interfaces.Services;

public interface IControllerVisualService
{
    /// <summary>
    /// Maps an SVG element ID to a GamepadBinding.
    /// Returns null if the ID is not recognized.
    /// </summary>
    GamepadBinding? MapIdToBinding(string elementId);

    /// <summary>
    /// Gets the display name for an SVG element ID.
    /// </summary>
    string GetDisplayName(string elementId);
}
