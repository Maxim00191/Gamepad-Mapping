#nullable enable
using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Models.State;

/// <summary>
/// Undo/redo payload for a mapping-list editor tab (Mappings or Visual): mappings slice plus list selection.
/// </summary>
public sealed class MappingListWorkspaceSnapshot
{
    public List<MappingEntry> Mappings { get; set; } = [];

    public List<string> SelectedMappingIds { get; set; } = [];

    public bool IsCreatingNewMapping { get; set; }
}
