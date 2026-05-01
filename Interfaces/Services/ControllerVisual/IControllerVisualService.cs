using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Interfaces.Services.ControllerVisual;

public interface IControllerVisualService
{
    /// <summary>
    /// Maps a binding value and type back to a logical control id.
    /// </summary>
    string? MapBindingToId(string value, GamepadBindingType type);

    /// <summary>
    /// Resolves one chord segment (e.g. <c>RightTrigger</c>, <c>B</c>) to a logical control id, independent of
    /// the stored <see cref="GamepadBindingType"/> on the parent mapping (templates may store LT/RT chords as <see cref="GamepadBindingType.Button"/>).
    /// </summary>
    string? MapChordSegmentToLogicalControlId(string segment);

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

    /// <summary>
    /// Whether <paramref name="mapping"/>'s input trigger is represented by <paramref name="elementId"/> on the diagram
    /// (including thumbstick surfaces, which aggregate axis/direction tokens).
    /// </summary>
    bool IsMappingOnLogicalControl(MappingEntry mapping, string elementId);

    /// <summary>
    /// True when <paramref name="elementId"/> is the diagram region for the PlayStation touch surface (click + gestures).
    /// </summary>
    bool IsTouchpadSurfaceLogicalControl(string elementId);
}
