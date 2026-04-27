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

public sealed class KeyboardActionsEditorWorkspace : JsonEditorWorkspaceBase<KeyboardActionDefinition, KeyboardActionsWorkspaceSnapshot>
{
    public KeyboardActionsEditorWorkspace(
        IWorkspaceState host,
        IProfileDomainService domain,
        IAppToastService toast)
        : base(host, domain, toast)
    {
    }

    public override EditorWorkspaceKind Kind => EditorWorkspaceKind.KeyboardActions;

    protected override ObservableCollection<KeyboardActionDefinition> WorkspaceItems => Host.KeyboardActions;

    protected override string ItemLogName => "keyboard actions";

    public void AddNewAction()
    {
        History.ExecuteTransaction(() =>
        {
            var newAction = new KeyboardActionDefinition
            {
                Id = Domain.EnsureUniqueId(null, Host.KeyboardActions.Select(a => a.Id), "action"),
                KeyboardKey = string.Empty,
                Description = string.Empty
            };
            Host.KeyboardActions.Add(newAction);
            Selection.SelectedItem = newAction;
            Selection.UpdateSelection(new[] { newAction });
        });
    }

    public void UpdateSelectedFromCatalog()
    {
        if (Selection.SelectedItem is null)
            return;

        Host.PullKeyboardCatalogDescriptionPair(out var primary, out var secondary);
        Host.PushKeyboardCatalogDescriptionPair(primary, secondary);
        Host.SyncCatalogOutputKindFromSelection();
        Host.RefreshMappingEngineDefinitions();
        Host.RefreshAfterRulePastedFromClipboard();
    }

    protected override KeyboardActionsWorkspaceSnapshot CaptureSnapshot()
    {
        var json = JsonConvert.SerializeObject(Host.KeyboardActions.ToList(), SnapshotSettings);
        var list = JsonConvert.DeserializeObject<List<KeyboardActionDefinition>>(json, SnapshotSettings) ?? [];
        return new KeyboardActionsWorkspaceSnapshot
        {
            KeyboardActions = list,
            SelectedKeyboardActionIds = Selection.SelectedItems.Select(a => a.Id).ToList()
        };
    }

    protected override void ApplySnapshot(KeyboardActionsWorkspaceSnapshot snapshot)
    {
        Host.KeyboardActions.Clear();
        foreach (var a in snapshot.KeyboardActions)
            Host.KeyboardActions.Add(a);

        if (snapshot.SelectedKeyboardActionIds.Count > 0)
        {
            var items = Host.KeyboardActions
                .Where(a => snapshot.SelectedKeyboardActionIds.Any(id => IdEquals(a.Id, id)))
                .Cast<object>()
                .ToList();
            Selection.UpdateSelection(items);
        }
        else
        {
            Selection.ResetTo(null);
        }

        Host.RefreshAfterRulePastedFromClipboard();
    }

    protected override bool TryCloneToken(JToken token, out KeyboardActionDefinition? clone)
    {
        clone = token.ToObject<KeyboardActionDefinition>();
        if (clone is null)
        {
            return false;
        }
        clone.Id = Domain.EnsureUniqueId(clone.Id, Host.KeyboardActions.Select(x => x.Id), "action");
        return true;
    }
}
