#nullable enable

using System;
using System.Collections.Generic;
using Gamepad_Mapping.Interfaces.Services.ControllerVisual;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Utils.ControllerVisual;

public static class ControllerMappingFromDisplayFormatter
{
    public static string FormatInputLine(IControllerVisualService visual, MappingEntry mapping)
    {
        if (mapping.From is null || string.IsNullOrEmpty(mapping.From.Value))
            return string.Empty;

        var raw = mapping.From.Value;
        var type = mapping.From.Type;
        if (raw.IndexOf('+', StringComparison.Ordinal) < 0)
        {
            var id = visual.MapBindingToId(raw, type);
            var label = id is not null ? visual.GetDisplayName(id) : raw;
            return ControllerMappingOverlayLabelText.NormalizeForOverlay(label);
        }

        var parts = raw.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var labels = new List<string>(parts.Length);
        foreach (var p in parts)
        {
            var id = visual.MapBindingToId(p, type);
            var label = id is not null ? visual.GetDisplayName(id) : p;
            labels.Add(ControllerMappingOverlayLabelText.NormalizeForOverlay(label));
        }

        return string.Join(ControllerMappingOverlayFormatting.ChordPartSeparator, labels);
    }
}
