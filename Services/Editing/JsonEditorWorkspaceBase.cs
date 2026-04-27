#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GamepadMapperGUI.Interfaces.Services.Editing;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;

namespace GamepadMapperGUI.Services.Editing;

public abstract class JsonEditorWorkspaceBase<TItem, TSnapshot> : IEditorWorkspace<TItem>
    where TItem : class
    where TSnapshot : class
{
    private readonly EditorHistoryService<TSnapshot> _history;
    private readonly InMemoryEditorClipboard<string> _clipboard = new();

    protected JsonEditorWorkspaceBase(
        IWorkspaceState host,
        IProfileDomainService domain,
        IAppToastService toast,
        Func<bool>? canRecord = null)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Domain = domain ?? throw new ArgumentNullException(nameof(domain));
        Toast = toast ?? throw new ArgumentNullException(nameof(toast));
        Selection = new SelectionService<TItem>();

        _history = new EditorHistoryService<TSnapshot>(
            CaptureSnapshot,
            ApplySnapshot,
            canRecord ?? (() => Host.SelectedTemplate is not null));

        _history.HistoryChanged += (_, _) => RaiseStateChanged();
        Selection.SelectionChanged += (_, _) => RaiseStateChanged();
    }

    protected IWorkspaceState Host { get; }

    protected IProfileDomainService Domain { get; }

    protected IAppToastService Toast { get; }

    protected static readonly JsonSerializerSettings SnapshotSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Formatting = Formatting.None
    };

    protected abstract ObservableCollection<TItem> WorkspaceItems { get; }

    protected abstract string ItemLogName { get; }

    protected abstract TSnapshot CaptureSnapshot();

    protected abstract void ApplySnapshot(TSnapshot snapshot);

    public abstract EditorWorkspaceKind Kind { get; }

    public ISelectionService<TItem> Selection { get; }

    public IEditorHistory History => _history;

    public IEditorClipboard<string> Clipboard => _clipboard;

    public event EventHandler? StateChanged;

    public virtual bool CanCopy =>
        Host.SelectedTemplate is not null && HasSelection();

    public virtual bool CanPaste =>
        Host.SelectedTemplate is not null && _clipboard.HasContent;

    public virtual bool CanDelete =>
        Host.SelectedTemplate is not null && HasSelection();

    public virtual bool CanSelectAll =>
        Host.SelectedTemplate is not null && WorkspaceItems.Count > 0;

    public void Copy()
    {
        try
        {
            var json = SerializeSelection();
            if (string.IsNullOrEmpty(json))
                return;

            Toast.LogDebug($"Copying {ItemLogName}: {json}");
            _clipboard.Store(json);
            RaiseStateChanged();
        }
        catch (Exception ex)
        {
            Toast.ShowError("ProfileRuleClipboard_CopyFailedTitle", "ProfileRuleClipboard_CopyFailedMessage", ex.Message);
        }
    }

    public void Paste()
    {
        if (!CanPaste || !_clipboard.TryGet(out var json) || string.IsNullOrEmpty(json))
            return;

        _history.ExecuteTransaction(() =>
        {
            try
            {
                Toast.LogDebug($"Pasting {ItemLogName}: {json}");
                var added = PasteCore(json);
                if (added.Count == 0)
                    return;

                Selection.SelectedItem = added[^1];
                Selection.UpdateSelection(added.Cast<object>().ToList());
                OnWorkspaceMutated();
            }
            catch (Exception ex)
            {
                Toast.ShowError("ProfileRuleClipboard_PasteFailedTitle", "ProfileRuleClipboard_PasteFailedMessage", ex.Message);
            }
        });
    }

    public void Delete()
    {
        if (!CanDelete)
            return;

        _history.ExecuteTransaction(() =>
        {
            Toast.LogDebug($"Deleting {ItemLogName}");
            DeleteCore();
            OnWorkspaceMutated();
        });
    }

    public void SelectAll()
    {
        if (!CanSelectAll)
            return;

        _history.ExecuteTransaction(() => Selection.SelectAll(WorkspaceItems));
    }

    public virtual void Reload(GameProfileTemplate? template)
    {
        _history.Clear();
        _clipboard.Clear();
        Selection.ResetTo(WorkspaceItems.FirstOrDefault());
        RaiseStateChanged();
    }

    public void ClearClipboard()
    {
        _clipboard.Clear();
        RaiseStateChanged();
    }

    protected virtual void OnWorkspaceMutated()
    {
        Host.RefreshMappingEngineDefinitions();
        Host.RefreshAfterRulePastedFromClipboard();
    }

    protected abstract bool TryCloneToken(JToken token, out TItem? clone);

    protected virtual IReadOnlyList<TItem> PasteCore(string json)
    {
        List<TItem> added = [];
        foreach (var token in ParseJson(json))
        {
            if (!TryCloneToken(token, out var clone) || clone is null)
                continue;

            WorkspaceItems.Add(clone);
            added.Add(clone);
        }

        return added;
    }

    protected virtual void DeleteCore()
    {
        var toDelete = SelectedItems();
        var allItems = WorkspaceItems.ToList();
        var lastIndex = toDelete.Count > 0 ? allItems.IndexOf(toDelete[^1]) : -1;

        foreach (var item in toDelete)
            WorkspaceItems.Remove(item);

        if (WorkspaceItems.Count > 0)
        {
            var nextIndex = Math.Clamp(lastIndex, 0, WorkspaceItems.Count - 1);
            Selection.ResetTo(WorkspaceItems[nextIndex]);
        }
        else
        {
            Selection.ResetTo(null);
        }
    }

    protected List<TItem> SelectedItems()
    {
        var items = Selection.SelectedItems.Count > 0
            ? Selection.SelectedItems.ToList()
            : Selection.SelectedItem is { } selected
                ? [selected]
                : [];
        return items;
    }

    protected string SerializeSelection()
    {
        var items = SelectedItems();
        return items.Count > 1
            ? JsonConvert.SerializeObject(items, SnapshotSettings)
            : items.Count == 1
                ? JsonConvert.SerializeObject(items[0], SnapshotSettings)
                : string.Empty;
    }

    protected static IEnumerable<JToken> ParseJson(string json)
    {
        var token = JToken.Parse(json);
        return token is JArray arr ? arr : [token];
    }

    protected static bool IdEquals(string? a, string? b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    protected void RaiseStateChanged() =>
        StateChanged?.Invoke(this, EventArgs.Empty);

    private bool HasSelection() =>
        Selection.SelectedItems.Count > 0 || Selection.SelectedItem is not null;
}
