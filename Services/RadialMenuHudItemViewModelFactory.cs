using GamepadMapperGUI.Models;
using Gamepad_Mapping.ViewModels;

namespace GamepadMapperGUI.Services;

internal static class RadialMenuHudItemViewModelFactory
{
    public static RadialMenuItemViewModel Create(
        RadialMenuHudItem item,
        int segmentIndex,
        int segmentCount,
        RadialMenuHudLabelMode labelMode)
    {
        var desc = item.DisplayName;
        var key = item.KeyboardKeyLabel.Trim();
        var vm = new RadialMenuItemViewModel
        {
            ActionId = item.ActionId,
            Icon = item.Icon,
            SegmentIndex = segmentIndex,
            SegmentCount = segmentCount
        };

        switch (labelMode)
        {
            case RadialMenuHudLabelMode.DescriptionOnly:
                vm.PrimaryCaption = desc;
                vm.SecondaryCaption = null;
                break;
            case RadialMenuHudLabelMode.KeyboardKeyOnly:
                vm.PrimaryCaption = string.IsNullOrEmpty(key) ? desc : key;
                vm.SecondaryCaption = null;
                break;
            default:
                vm.PrimaryCaption = desc;
                vm.SecondaryCaption = string.IsNullOrEmpty(key) ? null : key;
                break;
        }

        return vm;
    }
}
