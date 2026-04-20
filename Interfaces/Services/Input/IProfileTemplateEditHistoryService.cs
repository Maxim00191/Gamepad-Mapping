#nullable enable
using System;

namespace GamepadMapperGUI.Interfaces.Services.Input;

/// <summary>
/// Undo/redo stack for in-memory profile template workspace edits (mappings, keyboard actions, radial menus, combo leads).
/// Snapshots are opaque to callers; the host supplies capture/apply delegates.
/// </summary>
public interface IProfileTemplateEditHistoryService
{
    bool CanUndo { get; }

    bool CanRedo { get; }

    /// <summary>Clears both stacks (e.g. after switching templates).</summary>
    void Clear();

    /// <summary>
    /// Call immediately before a mutating operation so the current workspace can be restored on Undo.
    /// </summary>
    void RecordCheckpoint();

    void Undo();

    void Redo();

    event EventHandler? HistoryChanged;
}
