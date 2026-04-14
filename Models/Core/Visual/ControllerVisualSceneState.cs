using System;
using System.Collections.Generic;

namespace Gamepad_Mapping.Models.Core.Visual;

/// <summary>
/// An immutable DTO representing the visual state of a single controller element.
/// </summary>
public record ControllerElementVisualState(
    string ElementId,
    ControllerVisualHighlightKind Highlight,
    bool IsDimmed,
    string? PrimaryLabel = null,
    string? SecondaryLabel = null,
    string? ToolTip = null
);

/// <summary>
/// An immutable DTO representing the entire visual scene of the controller.
/// </summary>
public record ControllerVisualSceneState(
    IReadOnlyList<ControllerElementVisualState> Elements
);
