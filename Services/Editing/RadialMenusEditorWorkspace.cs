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
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Services.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;

namespace GamepadMapperGUI.Services.Editing;

public sealed class RadialMenusEditorWorkspace : IEditorWorkspace<RadialMenuDefinition>
{
    private readonly IWorkspaceState _host;
    private readonly IProfileDomainService _domain;
    private readonly IAppToastService _toast;
    private readonly EditorHistoryService<RadialMenusWorkspaceSnapshot> _history;
    private readonly InMemoryEditorClipboard<string> _clipboard = new();

    public RadialMenusEditorWorkspace(
        IWorkspaceState host,
        IProfileDomainService domain,
        IAppToastService toast)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _domain = domain ?? throw new ArgumentNullException(nameof(domain));
        _toast = toast ?? throw new ArgumentNullException(nameof(toast));
        Selection = new SelectionService<RadialMenuDefinition>();

        _history = new EditorHistoryService<RadialMenusWorkspaceSnapshot>(
            CaptureSnapshot,
            ApplySnapshot,
            () => _host.SelectedTemplate is not null);

        _history.HistoryChanged += (_, _) => RaiseStateChanged();
        Selection.SelectionChanged += (_, _) => RaiseStateChanged();
    }

    public EditorWorkspaceKind Kind => EditorWorkspaceKind.RadialMenus;

    public ISelectionService<RadialMenuDefinition> Selection { get; }

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

    public bool CanSelectAll => _host.SelectedTemplate is not null && _host.RadialMenus.Count > 0;

    public void Copy()
    {
        try
        {
            var json = SerializeSelection();
            if (!string.IsNullOrEmpty(json))
            {
                _toast.LogDebug($"Copying radial menus: {json}");
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
                _toast.LogDebug($"Pasting radial menus: {json}");
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
            _toast.LogDebug("Deleting radial menus");
            DeleteCore();
            _host.RefreshMappingEngineDefinitions();
            _host.RefreshAfterRulePastedFromClipboard();
        });
    }

    public void SelectAll()
    {
        if (!CanSelectAll)
            return;

        _history.ExecuteTransaction(() => Selection.SelectAll(_host.RadialMenus));
    }

    public void Reload(GameProfileTemplate? template)
    {
        _history.Clear();
        _clipboard.Clear();
        Selection.ResetTo(_host.RadialMenus.FirstOrDefault());
        RaiseStateChanged();
    }

    public void AddNewMenu()
    {
        _history.ExecuteTransaction(() =>
        {
            var newMenu = new RadialMenuDefinition
            {
                Id = _domain.EnsureUniqueId(null, _host.RadialMenus.Select(r => r.Id), "radial"),
                DisplayName = AppUiLocalization.GetString("RadialMenu_DefaultDisplayName"),
                Joystick = "RightStick",
                Items = new ObservableCollection<RadialMenuItem>()
            };
            _host.RadialMenus.Add(newMenu);
            Selection.SelectedItem = newMenu;
            Selection.UpdateSelection(new[] { newMenu });
        });
    }

    public void UpdateSelectedFromCatalog()
    {
        if (Selection.SelectedItem is null)
            return;

        _host.PullRadialMenuDisplayNamePair(out var primary, out var secondary);
        _host.PushRadialMenuDisplayNamePair(primary, secondary);
        _host.RefreshMappingEngineDefinitions();
        _host.RefreshAfterRulePastedFromClipboard();
    }

    private static readonly JsonSerializerSettings SnapshotSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Formatting = Formatting.None
    };

    private RadialMenusWorkspaceSnapshot CaptureSnapshot()
    {
        var json = JsonConvert.SerializeObject(_host.RadialMenus.ToList(), SnapshotSettings);
        var list = JsonConvert.DeserializeObject<List<RadialMenuDefinition>>(json, SnapshotSettings) ?? [];
        return new RadialMenusWorkspaceSnapshot
        {
            RadialMenus = list,
            SelectedRadialMenuIds = Selection.SelectedItems.Select(r => r.Id).ToList()
        };
    }

    private void ApplySnapshot(RadialMenusWorkspaceSnapshot snapshot)
    {
        _host.RadialMenus.Clear();
        foreach (var r in snapshot.RadialMenus)
            _host.RadialMenus.Add(r);

        if (snapshot.SelectedRadialMenuIds.Count > 0)
        {
            var items = _host.RadialMenus
                .Where(r => snapshot.SelectedRadialMenuIds.Any(id => IdEquals(r.Id, id)))
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
                ? new List<RadialMenuDefinition> { item }
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
        var added = new List<RadialMenuDefinition>();
        foreach (var token in tokens)
        {
            if (token.ToObject<RadialMenuDefinition>() is { } clone)
            {
                clone.Id = _domain.EnsureUniqueId(clone.Id, _host.RadialMenus.Select(x => x.Id), "radial");
                if (clone.Items is not ObservableCollection<RadialMenuItem>)
                    clone.Items = new ObservableCollection<RadialMenuItem>(clone.Items ?? []);
                _host.RadialMenus.Add(clone);
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

        var allItems = _host.RadialMenus.ToList();
        var lastIndex = toDelete.Count > 0 ? allItems.IndexOf(toDelete[^1]) : -1;

        foreach (var item in toDelete)
            _host.RadialMenus.Remove(item);

        if (_host.RadialMenus.Count > 0)
        {
            var nextIndex = Math.Clamp(lastIndex, 0, _host.RadialMenus.Count - 1);
            Selection.ResetTo(_host.RadialMenus[nextIndex]);
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
