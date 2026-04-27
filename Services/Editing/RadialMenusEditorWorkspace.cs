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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GamepadMapperGUI.Services.Editing;

public sealed class RadialMenusEditorWorkspace : JsonEditorWorkspaceBase<RadialMenuDefinition, RadialMenusWorkspaceSnapshot>
{
    public RadialMenusEditorWorkspace(
        IWorkspaceState host,
        IProfileDomainService domain,
        IAppToastService toast)
        : base(host, domain, toast)
    {
    }

    public override EditorWorkspaceKind Kind => EditorWorkspaceKind.RadialMenus;

    protected override ObservableCollection<RadialMenuDefinition> WorkspaceItems => Host.RadialMenus;

    protected override string ItemLogName => "radial menus";

    public void AddNewMenu()
    {
        History.ExecuteTransaction(() =>
        {
            var newMenu = new RadialMenuDefinition
            {
                Id = Domain.EnsureUniqueId(null, Host.RadialMenus.Select(r => r.Id), "radial"),
                DisplayName = AppUiLocalization.GetString("RadialMenu_DefaultDisplayName"),
                Joystick = "RightStick",
                Items = new ObservableCollection<RadialMenuItem>()
            };
            Host.RadialMenus.Add(newMenu);
            Selection.SelectedItem = newMenu;
            Selection.UpdateSelection(new[] { newMenu });
        });
    }

    public void UpdateSelectedFromCatalog()
    {
        if (Selection.SelectedItem is null)
            return;

        Host.PullRadialMenuDisplayNamePair(out var primary, out var secondary);
        Host.PushRadialMenuDisplayNamePair(primary, secondary);
        Host.RefreshMappingEngineDefinitions();
        Host.RefreshAfterRulePastedFromClipboard();
    }

    protected override RadialMenusWorkspaceSnapshot CaptureSnapshot()
    {
        var json = JsonConvert.SerializeObject(Host.RadialMenus.ToList(), SnapshotSettings);
        var list = JsonConvert.DeserializeObject<List<RadialMenuDefinition>>(json, SnapshotSettings) ?? [];
        return new RadialMenusWorkspaceSnapshot
        {
            RadialMenus = list,
            SelectedRadialMenuIds = Selection.SelectedItems.Select(r => r.Id).ToList()
        };
    }

    protected override void ApplySnapshot(RadialMenusWorkspaceSnapshot snapshot)
    {
        Host.RadialMenus.Clear();
        foreach (var r in snapshot.RadialMenus)
            Host.RadialMenus.Add(r);

        if (snapshot.SelectedRadialMenuIds.Count > 0)
        {
            var items = Host.RadialMenus
                .Where(r => snapshot.SelectedRadialMenuIds.Any(id => IdEquals(r.Id, id)))
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

    protected override bool TryCloneToken(JToken token, out RadialMenuDefinition? clone)
    {
        clone = token.ToObject<RadialMenuDefinition>();
        if (clone is null)
        {
            return false;
        }

        clone.Id = Domain.EnsureUniqueId(clone.Id, Host.RadialMenus.Select(x => x.Id), "radial");
        if (clone.Items is not ObservableCollection<RadialMenuItem>)
            clone.Items = new ObservableCollection<RadialMenuItem>(clone.Items ?? []);
        return true;
    }
}
