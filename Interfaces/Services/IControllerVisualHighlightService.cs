using System;
using System.ComponentModel;
using Gamepad_Mapping.Models.Core.Visual;

namespace Gamepad_Mapping.Interfaces.Services;

/// <summary>
/// Produces the visual scene state for the controller based on the current editor context.
/// </summary>
public interface IControllerVisualHighlightService : INotifyPropertyChanged
{
    /// <summary>
    /// Gets the current visual scene state.
    /// </summary>
    ControllerVisualSceneState CurrentScene { get; }

    /// <summary>
    /// Updates the scene state based on the provided interaction context.
    /// </summary>
    void UpdateContext(string? hoveredId, string? selectedId, IEnumerable<GamepadMapperGUI.Models.MappingEntry> mappings);
}
