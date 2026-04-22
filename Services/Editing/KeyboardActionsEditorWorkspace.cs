#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Interfaces.Services.Editing;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Services.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;

namespace GamepadMapperGUI.Services.Editing;

public sealed class KeyboardActionsEditorWorkspace : IEditorWorkspace<KeyboardActionDefinition>
{
    private readonly IWorkspaceState _host;
    private readonly IProfileDomainService _domain;
    private readonly IAppToastService _toast;
    private readonly EditorHistoryService<KeyboardActionsWorkspaceSnapshot> _history;
    private readonly InMemoryEditorClipboard<string> _clipboard = new();

    public KeyboardActionsEditorWorkspace(
        IWorkspaceState host,
        IProfileDomainService domain,
        IAppToastService toast)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _domain = domain ?? throw new ArgumentNullException(nameof(domain));
        _toast = toast ?? throw new ArgumentNullException(nameof(toast));
        Selection = new SelectionService<KeyboardActionDefinition>();

        _history = new EditorHistoryService<KeyboardActionsWorkspaceSnapshot>(
            CaptureSnapshot,
            ApplySnapshot,
            () => _host.SelectedTemplate is not null);

        _history.HistoryChanged += (_, _) => RaiseStateChanged();
        Selection.SelectionChanged += (_, _) => RaiseStateChanged();
    }

    public EditorWorkspaceKind Kind => EditorWorkspaceKind.KeyboardActions;

    public ISelectionService<KeyboardActionDefinition> Selection { get; }

    public IEditorHistory History => _history;

    public IEditorClipboard<string> Clipboard => _clipboard;

    public event EventHandler? StateChanged;

    public bool CanCopy =>
        _host.SelectedTemplate is not null
        && (Selection.SelectedItems.Count > 0 || Selection.SelectedItem is not null);

    public bool CanPaste => _host.SelectedTemplate is not null && _clipboard.HasContent;

    public bool CanDelete =>
        _host.SelectedTemplate is not null
        && (Selection.SelectedItems.Count > 0 || Selection.SelectedItem is not null);

    public bool CanSelectAll => _host.SelectedTemplate is not null && _host.KeyboardActions.Count > 0;

    public void Copy()
    {
        try
        {
            var json = SerializeSelection();
            if (!string.IsNullOrEmpty(json))
            {
                _toast.LogDebug($"Copying keyboard actions: {json}");
                _clipboard.Store(json);
                RaiseStateChanged();
            }
        }
        catch (Exception ex)
        {
            _toast.ShowError("ProfileRuleClipboard_CopyFailedTitle", "ProfileRuleClipboard_CopyFailedMessage", ex.Message);
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
                _toast.LogDebug($"Pasting keyboard actions: {json}");
                PasteCore(json);
            }
            catch (Exception ex)
            {
                _toast.ShowError("ProfileRuleClipboard_PasteFailedTitle", "ProfileRuleClipboard_PasteFailedMessage", ex.Message);
            }
        });
    }

    public void Delete()
    {
        if (!CanDelete)
            return;

        _history.ExecuteTransaction(() =>
        {
            _toast.LogDebug("Deleting keyboard actions");
            DeleteCore();
            _host.RefreshMappingEngineDefinitions();
            _host.RefreshAfterRulePastedFromClipboard();
        });
    }

    public void SelectAll()
    {
        if (!CanSelectAll)
            return;

        _history.ExecuteTransaction(() => Selection.SelectAll(_host.KeyboardActions));
    }

    public void Reload(GameProfileTemplate? template)
    {
        _history.Clear();
        _clipboard.Clear();
        Selection.ResetTo(_host.KeyboardActions.FirstOrDefault());
        RaiseStateChanged();
    }

    public void AddNewAction()
    {
        _history.ExecuteTransaction(() =>
        {
            var newAction = new KeyboardActionDefinition
            {
                Id = _domain.EnsureUniqueId(null, _host.KeyboardActions.Select(a => a.Id), "action"),
                KeyboardKey = string.Empty,
                Description = string.Empty
            };
            _host.KeyboardActions.Add(newAction);
            Selection.SelectedItem = newAction;
            Selection.UpdateSelection(new[] { newAction });
        });
    }

    public void UpdateSelectedFromCatalog()
    {
        if (Selection.SelectedItem is null)
            return;

        _host.PullKeyboardCatalogDescriptionPair(out var primary, out var secondary);
        _host.PushKeyboardCatalogDescriptionPair(primary, secondary);
        _host.SyncCatalogOutputKindFromSelection();
        _host.RefreshMappingEngineDefinitions();
        _host.RefreshAfterRulePastedFromClipboard();
    }

    private static readonly JsonSerializerSettings SnapshotSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Formatting = Formatting.None
    };

    private KeyboardActionsWorkspaceSnapshot CaptureSnapshot()
    {
        var json = JsonConvert.SerializeObject(_host.KeyboardActions.ToList(), SnapshotSettings);
        var list = JsonConvert.DeserializeObject<List<KeyboardActionDefinition>>(json, SnapshotSettings) ?? [];
        return new KeyboardActionsWorkspaceSnapshot
        {
            KeyboardActions = list,
            SelectedKeyboardActionIds = Selection.SelectedItems.Select(a => a.Id).ToList()
        };
    }

    private void ApplySnapshot(KeyboardActionsWorkspaceSnapshot snapshot)
    {
        _host.KeyboardActions.Clear();
        foreach (var a in snapshot.KeyboardActions)
            _host.KeyboardActions.Add(a);

        if (snapshot.SelectedKeyboardActionIds.Count > 0)
        {
            var items = _host.KeyboardActions
                .Where(a => snapshot.SelectedKeyboardActionIds.Any(id => IdEquals(a.Id, id)))
                .Cast<object>()
                .ToList();
            Selection.UpdateSelection(items);
        }
        else
        {
            Selection.ResetTo(null);
        }

        _host.RefreshAfterRulePastedFromClipboard();
    }

    private string SerializeSelection()
    {
        var items = Selection.SelectedItems.Count > 0
            ? Selection.SelectedItems.ToList()
            : Selection.SelectedItem is { } item
                ? new List<KeyboardActionDefinition> { item }
                : [];

        return items.Count > 1
            ? JsonConvert.SerializeObject(items, SnapshotSettings)
            : items.Count == 1
                ? JsonConvert.SerializeObject(items[0], SnapshotSettings)
                : string.Empty;
    }

    private void PasteCore(string json)
    {
        var tokens = ParseJson(json);
        var added = new List<KeyboardActionDefinition>();
        foreach (var token in tokens)
        {
            if (token.ToObject<KeyboardActionDefinition>() is { } clone)
            {
                clone.Id = _domain.EnsureUniqueId(clone.Id, _host.KeyboardActions.Select(x => x.Id), "action");
                _host.KeyboardActions.Add(clone);
                added.Add(clone);
            }
        }

        if (added.Count == 0)
            return;

        Selection.SelectedItem = added[^1];
        Selection.UpdateSelection(added.Cast<object>().ToList());
        _host.RefreshMappingEngineDefinitions();
        _host.RefreshAfterRulePastedFromClipboard();
    }

    private void DeleteCore()
    {
        var toDelete = Selection.SelectedItems.ToList();
        if (toDelete.Count == 0 && Selection.SelectedItem is { } one)
            toDelete.Add(one);

        var allItems = _host.KeyboardActions.ToList();
        var lastIndex = toDelete.Count > 0 ? allItems.IndexOf(toDelete[^1]) : -1;

        foreach (var item in toDelete)
            _host.KeyboardActions.Remove(item);

        if (_host.KeyboardActions.Count > 0)
        {
            var nextIndex = Math.Clamp(lastIndex, 0, _host.KeyboardActions.Count - 1);
            Selection.ResetTo(_host.KeyboardActions[nextIndex]);
        }
        else
        {
            Selection.ResetTo(null);
        }
    }

    private static IEnumerable<JToken> ParseJson(string json)
    {
        var token = JToken.Parse(json);
        return token is JArray arr ? arr : new[] { token };
    }

    private static bool IdEquals(string? a, string? b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private void RaiseStateChanged() =>
        StateChanged?.Invoke(this, EventArgs.Empty);

    public void ClearClipboard()
    {
        _clipboard.Clear();
        RaiseStateChanged();
    }
}
