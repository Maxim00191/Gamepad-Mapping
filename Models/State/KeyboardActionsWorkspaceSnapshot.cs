#nullable enable
using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Models.State;

/// <summary>Undo/redo payload for the keyboard actions catalog slice.</summary>
public sealed class KeyboardActionsWorkspaceSnapshot
{
    public List<KeyboardActionDefinition> KeyboardActions { get; set; } = [];

    public List<string> SelectedKeyboardActionIds { get; set; } = [];
}
