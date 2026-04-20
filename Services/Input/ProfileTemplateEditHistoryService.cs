#nullable enable
using System;
using System.Collections.Generic;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services.Input;

/// <summary>
/// Snapshot-based undo/redo using JSON round-trip of <see cref="GameProfileTemplate"/> workspace payloads.
/// </summary>
public sealed class ProfileTemplateEditHistoryService : IProfileTemplateEditHistoryService
{
    private readonly List<string> _undo = [];
    private readonly List<string> _redo = [];
    private readonly int _maxUndoEntries;
    private bool _applying;

    private readonly Func<GameProfileTemplate?> _capture;
    private readonly Action<GameProfileTemplate> _apply;
    private readonly Func<bool> _canRecord;

    public ProfileTemplateEditHistoryService(
        Func<GameProfileTemplate?> capture,
        Action<GameProfileTemplate> apply,
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

        _undo.Clear();
        _redo.Clear();
        RaiseHistoryChanged();
    }

    public void RecordCheckpoint()
    {
        if (_applying || !_canRecord())
            return;

        var current = _capture();
        if (current is null)
            return;

        var json = SerializeTemplate(current);
        _undo.Add(json);
        TrimOldest(_undo);
        _redo.Clear();
        RaiseHistoryChanged();
    }

    public void Undo()
    {
        if (!CanUndo || !_canRecord())
            return;

        var current = _capture();
        if (current is null)
            return;

        var previousJson = Pop(_undo);
        if (previousJson is null)
            return;

        _redo.Add(SerializeTemplate(current));
        TrimNewestIfOverCap(_redo);

        _applying = true;
        try
        {
            var restored = DeserializeTemplate(previousJson);
            if (restored is not null)
                _apply(restored);
        }
        finally
        {
            _applying = false;
        }

        RaiseHistoryChanged();
    }

    public void Redo()
    {
        if (!CanRedo || !_canRecord())
            return;

        var current = _capture();
        if (current is null)
            return;

        var nextJson = Pop(_redo);
        if (nextJson is null)
            return;

        _undo.Add(SerializeTemplate(current));
        TrimOldest(_undo);

        _applying = true;
        try
        {
            var restored = DeserializeTemplate(nextJson);
            if (restored is not null)
                _apply(restored);
        }
        finally
        {
            _applying = false;
        }

        RaiseHistoryChanged();
    }

    private static string? Pop(List<string> stack)
    {
        if (stack.Count == 0)
            return null;

        var last = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return last;
    }

    private void TrimOldest(List<string> list)
    {
        while (list.Count > _maxUndoEntries)
            list.RemoveAt(0);
    }

    private void TrimNewestIfOverCap(List<string> list)
    {
        while (list.Count > _maxUndoEntries)
            list.RemoveAt(list.Count - 1);
    }

    private void RaiseHistoryChanged() =>
        HistoryChanged?.Invoke(this, EventArgs.Empty);

    private static string SerializeTemplate(GameProfileTemplate t) =>
        JsonConvert.SerializeObject(t);

    private static GameProfileTemplate? DeserializeTemplate(string json) =>
        JsonConvert.DeserializeObject<GameProfileTemplate>(json);
}
