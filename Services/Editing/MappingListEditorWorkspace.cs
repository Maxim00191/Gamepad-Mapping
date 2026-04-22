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

/// <summary>
/// Mappings list editing for either the Mappings tab or the Visual tab (shared <see cref="MappingEntry"/> collection,
/// independent selection, clipboard, and undo stack per instance).
/// </summary>
public sealed class MappingListEditorWorkspace : IEditorWorkspace<MappingEntry>
{
    private readonly IWorkspaceState _host;
    private readonly IProfileDomainService _domain;
    private readonly IAppToastService _toast;
    private readonly EditorHistoryService<MappingListWorkspaceSnapshot> _history;
    private readonly InMemoryEditorClipboard<string> _clipboard = new();

    public MappingListEditorWorkspace(
        EditorWorkspaceKind kind,
        IWorkspaceState host,
        IProfileDomainService domain,
        IAppToastService toast)
    {
        Kind = kind;
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _domain = domain ?? throw new ArgumentNullException(nameof(domain));
        _toast = toast ?? throw new ArgumentNullException(nameof(toast));
        Selection = new SelectionService<MappingEntry>();

        _history = new EditorHistoryService<MappingListWorkspaceSnapshot>(
            CaptureSnapshot,
            ApplySnapshot,
            () => _host.SelectedTemplate is not null);

        _history.HistoryChanged += (_, _) => RaiseStateChanged();
        Selection.SelectionChanged += (_, _) => RaiseStateChanged();
    }

    public EditorWorkspaceKind Kind { get; }

    public ISelectionService<MappingEntry> Selection { get; }

    public IEditorHistory History => _history;

    public IEditorClipboard<string> Clipboard => _clipboard;

    public event EventHandler? StateChanged;

    public bool CanCopy =>
        _host.SelectedTemplate is not null
        && !_host.IsCreatingNewMapping
        && (Selection.SelectedItems.Count > 0 || Selection.SelectedItem is not null);

    public bool CanPaste =>
        _host.SelectedTemplate is not null && _clipboard.HasContent;

    public bool CanDelete =>
        _host.SelectedTemplate is not null
        && (Selection.SelectedItems.Count > 0 || Selection.SelectedItem is not null);

    public bool CanSelectAll => _host.SelectedTemplate is not null && _host.Mappings.Count > 0;

    public void Copy()
    {
        try
        {
            var json = SerializeSelection();
            if (!string.IsNullOrEmpty(json))
            {
                _toast.LogDebug($"Copying mappings ({Kind}): {json}");
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
                _toast.LogDebug($"Pasting mappings ({Kind}): {json}");
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
            _toast.LogDebug($"Deleting mappings ({Kind})");
            DeleteCore();
            _host.RefreshMappingEngineDefinitions();
            _host.RefreshAfterRulePastedFromClipboard();
        });
    }

    public void SelectAll()
    {
        if (!CanSelectAll)
            return;

        _history.ExecuteTransaction(() => Selection.SelectAll(_host.Mappings));
    }

    public void Reload(GameProfileTemplate? template)
    {
        _history.Clear();
        _clipboard.Clear();
        _host.IsCreatingNewMapping = false;
        Selection.ResetTo(_host.Mappings.FirstOrDefault());
        RaiseStateChanged();
    }

    /// <summary>Begins the "new mapping" row flow (mirrors legacy <see cref="ProfileRuleClipboardKind.Mapping"/> Add).</summary>
    public void BeginCreateNewMapping()
    {
        _history.ExecuteTransaction(() =>
        {
            _host.IsCreatingNewMapping = true;
            Selection.SelectedItem = null;
        });
    }

    /// <summary>Persists a new mapping built from the editor fields.</summary>
    public void SaveNewMapping()
    {
        _history.ExecuteTransaction(() =>
        {
            if (!_host.TryBuildMappingFromEditorFields(out var entry, out _))
                return;

            _host.Mappings.Add(entry);
            Selection.SelectedItem = entry;
            _host.IsCreatingNewMapping = false;
            _host.NotifyConfigurationChanged(ProfileRuleClipboardKind.Mapping);
            _host.RefreshAfterRulePastedFromClipboard();
        });
    }

    /// <summary>Applies editor fields to the selected mapping without an inner history transaction.</summary>
    public void UpdateSelectedFromEditorFields()
    {
        var selected = Selection.SelectedItem;
        if (selected is null || !_host.TryBuildMappingFromEditorFields(out var entry, out _))
            return;

        selected.From = entry.From;
        selected.Trigger = entry.Trigger;
        selected.Descriptions = entry.Descriptions;
        selected.Description = entry.Description;
        selected.AnalogThreshold = entry.AnalogThreshold;
        selected.ActionId = entry.ActionId;
        selected.HoldActionId = entry.HoldActionId;
        selected.HoldThresholdMs = entry.HoldThresholdMs;
        selected.TemplateToggle = entry.TemplateToggle;
        selected.RadialMenu = entry.RadialMenu;
        selected.ItemCycle = entry.ItemCycle;
        selected.KeyboardKey = entry.KeyboardKey;
        selected.HoldKeyboardKey = entry.HoldKeyboardKey;
        _host.NotifyConfigurationChanged(ProfileRuleClipboardKind.Mapping);
        _host.RefreshAfterRulePastedFromClipboard();
    }

    private static readonly JsonSerializerSettings SnapshotSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Formatting = Formatting.None
    };

    private MappingListWorkspaceSnapshot CaptureSnapshot()
    {
        var mappingJson = JsonConvert.SerializeObject(_host.Mappings.ToList(), SnapshotSettings);
        var mappings = JsonConvert.DeserializeObject<List<MappingEntry>>(mappingJson, SnapshotSettings) ?? [];
        return new MappingListWorkspaceSnapshot
        {
            Mappings = mappings,
            SelectedMappingIds = Selection.SelectedItems.Select(m => m.Id).ToList(),
            IsCreatingNewMapping = _host.IsCreatingNewMapping
        };
    }

    private void ApplySnapshot(MappingListWorkspaceSnapshot snapshot)
    {
        _host.Mappings.Clear();
        foreach (var m in snapshot.Mappings)
            _host.Mappings.Add(m);

        if (snapshot.SelectedMappingIds.Count > 0)
        {
            var items = _host.Mappings
                .Where(m => snapshot.SelectedMappingIds.Any(id => IdEquals(m.Id, id)))
                .Cast<object>()
                .ToList();
            Selection.UpdateSelection(items);
        }
        else
        {
            Selection.ResetTo(null);
        }

        _host.IsCreatingNewMapping = snapshot.IsCreatingNewMapping;
        _host.RefreshAfterRulePastedFromClipboard();
    }

    private string SerializeSelection()
    {
        var items = Selection.SelectedItems.Count > 0
            ? Selection.SelectedItems.ToList()
            : Selection.SelectedItem is { } item
                ? new List<MappingEntry> { item }
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
        var added = new List<MappingEntry>();
        foreach (var token in tokens)
        {
            if (token.ToObject<MappingEntry>() is { } clone)
            {
                clone.ExecutableAction = null;
                clone.Id = _domain.EnsureUniqueId(clone.Id, _host.Mappings.Select(x => x.Id), "mapping");
                _host.Mappings.Add(clone);
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
        if (toDelete.Count == 0 && Selection.SelectedItem is { } selectedItem)
            toDelete.Add(selectedItem);

        var allItems = _host.Mappings.ToList();
        var lastIndex = toDelete.Count > 0 ? allItems.IndexOf(toDelete[^1]) : -1;

        foreach (var itemToDelete in toDelete)
            _host.Mappings.Remove(itemToDelete);

        if (_host.Mappings.Count > 0)
        {
            var nextIndex = Math.Clamp(lastIndex, 0, _host.Mappings.Count - 1);
            Selection.ResetTo(_host.Mappings[nextIndex]);
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

    /// <summary>Clears clipboard and raises <see cref="StateChanged"/>.</summary>
    public void ClearClipboard()
    {
        _clipboard.Clear();
        RaiseStateChanged();
    }
}
