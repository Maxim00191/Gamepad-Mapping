#nullable enable
using System;
using GamepadMapperGUI.Interfaces.Services.Editing;

namespace GamepadMapperGUI.Services.Editing;

/// <summary>No-op history (e.g. Community tab with no editing surface).</summary>
public sealed class EmptyEditorHistory : IEditorHistory
{
    public static EmptyEditorHistory Instance { get; } = new();

    private EmptyEditorHistory()
    {
    }

    public bool CanUndo => false;

    public bool CanRedo => false;

    public event EventHandler? HistoryChanged
    {
        add { }
        remove { }
    }

    public void Clear()
    {
    }

    public void RecordCheckpoint()
    {
    }

    public void ExecuteTransaction(Action action) => action();

    public void Undo()
    {
    }

    public void Redo()
    {
    }
}
