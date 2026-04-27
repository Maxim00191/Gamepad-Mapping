#nullable enable
using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Models.State;

/// <summary>Undo/redo payload for the radial menus catalog slice.</summary>
public sealed class RadialMenusWorkspaceSnapshot
{
    public List<RadialMenuDefinition> RadialMenus { get; set; } = [];

    public List<string> SelectedRadialMenuIds { get; set; } = [];
}
