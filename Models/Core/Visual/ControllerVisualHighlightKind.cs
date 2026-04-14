using System;

namespace Gamepad_Mapping.Models.Core.Visual;

/// <summary>
/// Defines the visual emphasis level for a controller element.
/// </summary>
public enum ControllerVisualHighlightKind
{
    /// <summary>No special highlight.</summary>
    None,

    /// <summary>The element is currently being hovered by the pointer.</summary>
    Hover,

    /// <summary>The element is explicitly selected (primary focus).</summary>
    Selected,

    /// <summary>The element is part of a chord/combination related to the selection.</summary>
    ChordSecondary,

    /// <summary>The element is active or pressed in real-time (optional future use).</summary>
    Active
}
