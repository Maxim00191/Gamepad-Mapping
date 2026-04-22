#nullable enable
using System;

namespace GamepadMapperGUI.Interfaces.Services.Editing;

/// <summary>Per-surface undo/redo stack with snapshot capture/apply supplied by the host workspace.</summary>
public interface IEditorHistory
{
    bool CanUndo { get; }

    bool CanRedo { get; }

    void Clear();

    void RecordCheckpoint();

    void ExecuteTransaction(Action action);

    void Undo();

    void Redo();

    event EventHandler? HistoryChanged;
}
