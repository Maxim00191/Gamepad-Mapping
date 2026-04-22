#nullable enable
using System;
using System.Collections.Generic;
using GamepadMapperGUI.Interfaces.Services.Editing;
using Gamepad_Mapping.Utils;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services.Editing;

/// <summary>
/// Snapshot-based undo/redo for a single serialized workspace slice (<typeparamref name="TSnapshot"/>).
/// </summary>
public sealed class EditorHistoryService<TSnapshot> : IEditorHistory where TSnapshot : class
{
    private sealed record StackEntry(string Json);

    private readonly List<StackEntry> _undo = [];
    private readonly List<StackEntry> _redo = [];
    private readonly int _maxUndoEntries;
    private bool _applying;

    private readonly Func<TSnapshot> _capture;
    private readonly Action<TSnapshot> _apply;
    private readonly Func<bool> _canRecord;

    public EditorHistoryService(
        Func<TSnapshot> capture,
        Action<TSnapshot> apply,
        Func<bool> canRecord,
        int maxUndoEntries = 50)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _canRecord = canRecord ?? throw new ArgumentNullException(nameof(canRecord));
        _maxUndoEntries = Math.Clamp(maxUndoEntries, 1, 500);
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public event EventHandler? HistoryChanged;

    public void Clear()
    {
        if (_undo.Count == 0 && _redo.Count == 0)
            return;

        WorkspaceDebugTrace.Log("history", $"EditorHistory<{typeof(TSnapshot).Name}> Clear (undo={_undo.Count}, redo={_redo.Count})");
        _undo.Clear();
        _redo.Clear();
        RaiseHistoryChanged();
    }

    public void RecordCheckpoint()
    {
        if (_applying || !_canRecord())
            return;

        var snap = _capture();
        var json = Serialize(snap);
        var entry = new StackEntry(json);
        if (_undo.Count > 0 && string.Equals(_undo[^1].Json, json, StringComparison.Ordinal))
            return;

        _undo.Add(entry);
        TrimOldest(_undo);
        _redo.Clear();
        RaiseHistoryChanged();
    }

    private int _transactionNestingLevel;

    public void ExecuteTransaction(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_applying || !_canRecord())
        {
            action();
            return;
        }

        _transactionNestingLevel++;
        try
        {
            if (_transactionNestingLevel == 1)
            {
                var before = _capture();
                var beforeJson = Serialize(before);
                var undoCountBefore = _undo.Count;
                var redoBefore = _redo.ToArray();

                _undo.Add(new StackEntry(beforeJson));
                TrimOldest(_undo);
                _redo.Clear();

                try
                {
                    action();
                }
                catch
                {
                    RollbackNoOpCheckpoint(undoCountBefore, beforeJson, redoBefore);
                    throw;
                }

                var after = _capture();
                var afterJson = Serialize(after);

                if (string.Equals(afterJson, beforeJson, StringComparison.Ordinal))
                    RollbackNoOpCheckpoint(undoCountBefore, beforeJson, redoBefore);
                else
                    RaiseHistoryChanged();
            }
            else
            {
                action();
            }
        }
        finally
        {
            _transactionNestingLevel--;
        }
    }

    public void Undo()
    {
        if (!CanUndo || !_canRecord())
            return;

        WorkspaceDebugTrace.Log("history", $"EditorHistory<{typeof(TSnapshot).Name}> Undo begin");
        var current = _capture();
        var previous = PopEntry(_undo);
        if (previous is null)
            return;

        _redo.Add(new StackEntry(Serialize(current)));
        TrimNewestIfOverCap(_redo);

        _applying = true;
        try
        {
            var restored = Deserialize(previous.Json);
            if (restored is not null)
                _apply(restored);
        }
        finally
        {
            _applying = false;
        }

        WorkspaceDebugTrace.Log("history", $"EditorHistory<{typeof(TSnapshot).Name}> Undo end");
        RaiseHistoryChanged();
    }

    public void Redo()
    {
        if (!CanRedo || !_canRecord())
            return;

        WorkspaceDebugTrace.Log("history", $"EditorHistory<{typeof(TSnapshot).Name}> Redo begin");
        var current = _capture();
        var next = PopEntry(_redo);
        if (next is null)
            return;

        _undo.Add(new StackEntry(Serialize(current)));
        TrimOldest(_undo);

        _applying = true;
        try
        {
            var restored = Deserialize(next.Json);
            if (restored is not null)
                _apply(restored);
        }
        finally
        {
            _applying = false;
        }

        WorkspaceDebugTrace.Log("history", $"EditorHistory<{typeof(TSnapshot).Name}> Redo end");
        RaiseHistoryChanged();
    }

    private static StackEntry? PopEntry(List<StackEntry> stack)
    {
        if (stack.Count == 0)
            return null;

        var last = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return last;
    }

    private void TrimOldest<T>(List<T> list)
    {
        while (list.Count > _maxUndoEntries)
            list.RemoveAt(0);
    }

    private void TrimNewestIfOverCap<T>(List<T> list)
    {
        while (list.Count > _maxUndoEntries)
            list.RemoveAt(list.Count - 1);
    }

    private void RollbackNoOpCheckpoint(int undoCountBefore, string beforeJson, IReadOnlyList<StackEntry> redoBefore)
    {
        if (_undo.Count > undoCountBefore)
            _undo.RemoveAt(_undo.Count - 1);
        else if (_undo.Count > 0 && string.Equals(_undo[^1].Json, beforeJson, StringComparison.Ordinal))
            _undo.RemoveAt(_undo.Count - 1);

        _redo.Clear();
        foreach (var r in redoBefore)
            _redo.Add(r);
    }

    private void RaiseHistoryChanged() =>
        HistoryChanged?.Invoke(this, EventArgs.Empty);

    private static readonly JsonSerializerSettings SnapshotSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Formatting = Formatting.None
    };

    private static string Serialize(TSnapshot t) =>
        JsonConvert.SerializeObject(t, SnapshotSettings);

    private static TSnapshot? Deserialize(string json) =>
        JsonConvert.DeserializeObject<TSnapshot>(json, SnapshotSettings);
}
