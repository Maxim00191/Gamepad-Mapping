#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationUndoCoordinator : IAutomationUndoCoordinator
{
    private const int MaxStackDepth = 80;
    private readonly List<string> _undo = [];
    private readonly List<string> _redo = [];

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void PushCheckpoint(string serializedDocumentBeforeMutation)
    {
        if (string.IsNullOrEmpty(serializedDocumentBeforeMutation))
            return;

        _undo.Add(serializedDocumentBeforeMutation);
        while (_undo.Count > MaxStackDepth)
            _undo.RemoveAt(0);

        _redo.Clear();
    }

    public bool TryUndo(ref string serializedDocumentApply)
    {
        if (_undo.Count == 0)
            return false;

        var previous = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(serializedDocumentApply);
        serializedDocumentApply = previous;
        return true;
    }

    public bool TryRedo(ref string serializedDocumentApply)
    {
        if (_redo.Count == 0)
            return false;

        var next = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(serializedDocumentApply);
        serializedDocumentApply = next;
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
