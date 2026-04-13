using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Interfaces.Services;

public interface IControllerVisualService
{
    /// <summary>
    /// Maps a binding value and type back to a logical control id.
    /// </summary>
    string? MapBindingToId(string value, GamepadBindingType type);

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

    /// <summary>
    /// Gets all mappings associated with a logical control id.
    /// </summary>
    IEnumerable<MappingEntry> GetMappingsForElement(string elementId, IEnumerable<MappingEntry> mappings);
}
