using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels.Strategies;

/// <summary>
/// Factory interface for creating action editor strategies.
/// </summary>
public interface IActionEditorFactory
{
    /// <summary>
    /// Creates a strategy based on the action type.
    /// </summary>
    ActionEditorViewModelBase Create(MappingActionType actionType);

    /// <summary>
    /// Creates the appropriate strategy for an existing mapping.
    /// </summary>
    ActionEditorViewModelBase CreateForMapping(MappingEntry mapping);
}
