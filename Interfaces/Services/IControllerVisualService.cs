using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Interfaces.Services;

public interface IControllerVisualService
{
    IEnumerable<string> EnumerateMappedLogicalControlIds();

    /// <summary>
    /// Maps a logical control id (see controller layout manifest) to a GamepadBinding.
    /// Returns null if the ID is not recognized.
    /// </summary>
    GamepadBinding? MapIdToBinding(string elementId);

    /// <summary>
    /// Gets the display name for a logical control id.
    /// </summary>
    string GetDisplayName(string elementId);
}
