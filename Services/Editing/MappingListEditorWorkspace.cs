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
using GamepadMapperGUI.Models.State;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GamepadMapperGUI.Services.Editing;

/// <summary>
/// Mappings list editing for either the Mappings tab or the Visual tab (shared <see cref="MappingEntry"/> collection,
/// independent selection, clipboard, and undo stack per instance).
/// </summary>
public sealed class MappingListEditorWorkspace : JsonEditorWorkspaceBase<MappingEntry, MappingListWorkspaceSnapshot>
{
    public MappingListEditorWorkspace(
        EditorWorkspaceKind kind,
        IWorkspaceState host,
        IProfileDomainService domain,
        IAppToastService toast)
        : base(host, domain, toast)
    {
        Kind = kind;
    }

    public override EditorWorkspaceKind Kind { get; }

    protected override ObservableCollection<MappingEntry> WorkspaceItems => Host.Mappings;

    protected override string ItemLogName => $"mappings ({Kind})";

    public override bool CanCopy => base.CanCopy && !Host.IsCreatingNewMapping;

    public override void Reload(GameProfileTemplate? template)
    {
        Host.IsCreatingNewMapping = false;
        base.Reload(template);
    }

    /// <summary>Begins the "new mapping" row flow (mirrors legacy <see cref="ProfileRuleClipboardKind.Mapping"/> Add).</summary>
    public void BeginCreateNewMapping()
    {
        History.ExecuteTransaction(() =>
        {
            Host.IsCreatingNewMapping = true;
            Selection.SelectedItem = null;
        });
    }

    /// <summary>Persists a new mapping built from the editor fields.</summary>
    public void SaveNewMapping()
    {
        History.ExecuteTransaction(() =>
        {
            if (!Host.TryBuildMappingFromEditorFields(out var entry, out _))
                return;

            Host.Mappings.Add(entry);
            Selection.SelectedItem = entry;
            Host.IsCreatingNewMapping = false;
            Host.NotifyConfigurationChanged(ProfileRuleClipboardKind.Mapping);
            Host.RefreshAfterRulePastedFromClipboard();
        });
    }

    /// <summary>Applies editor fields to the selected mapping without an inner history transaction.</summary>
    public void UpdateSelectedFromEditorFields()
    {
        var selected = Selection.SelectedItem;
        if (selected is null || !Host.TryBuildMappingFromEditorFields(out var entry, out _))
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
        Host.NotifyConfigurationChanged(ProfileRuleClipboardKind.Mapping);
        Host.RefreshAfterRulePastedFromClipboard();
    }

    protected override MappingListWorkspaceSnapshot CaptureSnapshot()
    {
        var mappingJson = JsonConvert.SerializeObject(Host.Mappings.ToList(), SnapshotSettings);
        var mappings = JsonConvert.DeserializeObject<List<MappingEntry>>(mappingJson, SnapshotSettings) ?? [];
        return new MappingListWorkspaceSnapshot
        {
            Mappings = mappings,
            SelectedMappingIds = Selection.SelectedItems.Select(m => m.Id).ToList(),
            IsCreatingNewMapping = Host.IsCreatingNewMapping
        };
    }

    protected override void ApplySnapshot(MappingListWorkspaceSnapshot snapshot)
    {
        Host.Mappings.Clear();
        foreach (var m in snapshot.Mappings)
            Host.Mappings.Add(m);

        if (snapshot.SelectedMappingIds.Count > 0)
        {
            var items = Host.Mappings
                .Where(m => snapshot.SelectedMappingIds.Any(id => IdEquals(m.Id, id)))
                .Cast<object>()
                .ToList();
            Selection.UpdateSelection(items);
        }
        else
        {
            Selection.ResetTo(null);
        }

        Host.IsCreatingNewMapping = snapshot.IsCreatingNewMapping;
        Host.RefreshAfterRulePastedFromClipboard();
    }

    protected override bool TryCloneToken(JToken token, out MappingEntry? clone)
    {
        clone = token.ToObject<MappingEntry>();
        if (clone is null)
        {
            return false;
        }

        clone.ExecutableAction = null;
        clone.Id = Domain.EnsureUniqueId(clone.Id, Host.Mappings.Select(x => x.Id), "mapping");
        return true;
    }
}
