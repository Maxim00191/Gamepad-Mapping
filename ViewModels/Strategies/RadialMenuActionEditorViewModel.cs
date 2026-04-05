using System;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels.Strategies;

public partial class RadialMenuActionEditorViewModel : ActionEditorViewModelBase
{
    public RadialMenuActionEditorViewModel()
    {
    }

    [ObservableProperty]
    private string _radialMenuId = string.Empty;

    public override void SyncFrom(MappingEntry mapping)
    {
        RadialMenuId = mapping.RadialMenu?.RadialMenuId ?? string.Empty;
    }

    public override bool ApplyTo(MappingEntry mapping)
    {
        var rmId = (RadialMenuId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(rmId)) return false;

        mapping.ItemCycle = null;
        mapping.TemplateToggle = null;
        mapping.RadialMenu = new RadialMenuBinding { RadialMenuId = rmId };
        mapping.ActionId = null;
        mapping.KeyboardKey = string.Empty;
        mapping.HoldKeyboardKey = string.Empty;
        mapping.HoldThresholdMs = null;

        return true;
    }

    public override void Clear()
    {
        RadialMenuId = string.Empty;
    }
}
