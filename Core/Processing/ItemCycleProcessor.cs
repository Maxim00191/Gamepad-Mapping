using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

public sealed class ItemCycleProcessor
{
    private int _itemCycleSlotIndex = -1;

    public bool TryPrepareItemCycleStep(
        ItemCycleBinding cycle,
        out Key digitKey,
        out DispatchedOutput customOutput,
        out bool useCustomOutput,
        out Key[] modifierKeys,
        out string label,
        out string errorStatus)
    {
        digitKey = Key.None;
        customOutput = default;
        useCustomOutput = false;
        modifierKeys = Array.Empty<Key>();
        label = string.Empty;
        errorStatus = "Item cycle: invalid itemCycle fields.";

        if (!InputTokenResolver.TryParseItemCycleModifierKeys(cycle.WithKeys, out modifierKeys))
        {
            errorStatus = "Item cycle: invalid key name in itemCycle.withKeys";
            return false;
        }

        var n = Math.Clamp(cycle.SlotCount, 1, 9);
        int idx;
        if (_itemCycleSlotIndex < 0)
            idx = cycle.Direction == ItemCycleDirection.Next ? 0 : (n - 1);
        else if (cycle.Direction == ItemCycleDirection.Next)
            idx = (_itemCycleSlotIndex + 1) % n;
        else
            idx = (_itemCycleSlotIndex - 1 + n) % n;

        _itemCycleSlotIndex = idx;
        var modText = modifierKeys.Length > 0 ? string.Join('+', modifierKeys) + "+" : string.Empty;

        if (ItemCycleUsesCustomLoopKeys(cycle))
        {
            useCustomOutput = true;
            var token = cycle.Direction == ItemCycleDirection.Next
                ? cycle.LoopForwardKey!.Trim()
                : cycle.LoopBackwardKey!.Trim();
            if (!InputTokenResolver.TryResolveMappedOutput(token, out customOutput, out var baseLabel))
            {
                errorStatus = "Item cycle: invalid loopForwardKey or loopBackwardKey token";
                return false;
            }

            label = $"{modText}{baseLabel} (slot {idx + 1}/{n})";
            return true;
        }

        digitKey = Key.D1 + idx;
        label = $"{modText}{digitKey} (slot {idx + 1}/{n})";
        return true;
    }

    private static bool ItemCycleUsesCustomLoopKeys(ItemCycleBinding cycle)
    {
        var f = cycle.LoopForwardKey?.Trim() ?? string.Empty;
        var b = cycle.LoopBackwardKey?.Trim() ?? string.Empty;
        return f.Length > 0 && b.Length > 0;
    }

    public void Reset()
    {
        _itemCycleSlotIndex = -1;
    }
}
