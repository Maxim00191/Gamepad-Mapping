using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Processing;

/// <summary>
/// A unique identifier for tracking the state of a specific mapping's analog-to-digital transition.
/// Using a record struct ensures value-based equality without string allocations.
/// </summary>
internal readonly record struct AnalogStateId(
    GamepadBindingType Side,
    MappingActionType ActionType,
    string? Identifier = null,
    string? SecondaryIdentifier = null
);
