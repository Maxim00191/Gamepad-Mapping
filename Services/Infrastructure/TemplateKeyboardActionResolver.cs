using System;
using System.Collections.Generic;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>Resolves <see cref="MappingEntry.ActionId"/> using <see cref="GameProfileTemplate.KeyboardActions"/> after JSON load.</summary>
public static class TemplateKeyboardActionResolver
{
    /// <summary>
    /// Non-throwing checks aligned with the community <c>validate_templates.py</c> resolver pass
    /// and <see cref="Apply"/> invariants.
    /// </summary>
    public static IReadOnlyList<string> CollectResolutionErrors(GameProfileTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var errors = new List<string>();
        if (template.Mappings is null)
            return errors;

        var mappings = template.Mappings;
        var catalog = template.KeyboardActions;
        if (catalog is null || catalog.Count == 0)
        {
            for (var i = 0; i < mappings.Count; i++)
            {
                var m = mappings[i];
                var aid = (m.ActionId ?? string.Empty).Trim();
                var hid = (m.HoldActionId ?? string.Empty).Trim();
                if (aid.Length > 0)
                {
                    errors.Add(
                        $"mappings[{i}]: references actionId '{aid}' but keyboardActions is missing or empty.");
                }

                if (hid.Length > 0)
                {
                    errors.Add(
                        $"mappings[{i}]: references holdActionId '{hid}' but keyboardActions is missing or empty.");
                }
            }

            return errors;
        }

        var idMap = new Dictionary<string, KeyboardActionDefinition>(StringComparer.OrdinalIgnoreCase);
        for (var j = 0; j < catalog.Count; j++)
        {
            var a = catalog[j];
            var aid = (a.Id ?? string.Empty).Trim();
            if (aid.Length == 0)
            {
                errors.Add($"keyboardActions[{j}]: id is empty.");
                continue;
            }

            if (!idMap.TryAdd(aid, a))
                errors.Add($"Duplicate keyboardActions id '{aid}'.");
        }

        for (var i = 0; i < mappings.Count; i++)
        {
            var m = mappings[i];
            var actionId = (m.ActionId ?? string.Empty).Trim();
            var holdId = (m.HoldActionId ?? string.Empty).Trim();
            if (actionId.Length == 0 && holdId.Length == 0)
                continue;

            if (actionId.Length > 0)
            {
                if (m.ItemCycle is not null || m.TemplateToggle is not null || m.RadialMenu is not null)
                {
                    errors.Add(
                        $"mappings[{i}]: actionId cannot be used together with itemCycle, templateToggle, or radialMenu on the same mapping.");
                }
                else if (!idMap.TryGetValue(actionId, out var defn))
                {
                    errors.Add($"mappings[{i}]: unknown keyboardActions id '{actionId}'.");
                }
                else
                {
                    var k = (defn.KeyboardKey ?? string.Empty).Trim();
                    if (k.Length == 0 && defn.TemplateToggle is null && defn.RadialMenu is null && defn.ItemCycle is null)
                    {
                        errors.Add(
                            $"mappings[{i}]: keyboardActions id '{actionId}' has no keyboardKey, templateToggle, radialMenu, or itemCycle.");
                    }
                }
            }

            if (holdId.Length > 0 && !idMap.ContainsKey(holdId))
                errors.Add($"mappings[{i}]: unknown keyboardActions id '{holdId}' (holdActionId).");
        }

        return errors;
    }

    public static void Apply(GameProfileTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        template.Mappings ??= [];

        var catalog = template.KeyboardActions;
        if (catalog is null || catalog.Count == 0)
        {
            foreach (var m in template.Mappings)
            {
                if (!string.IsNullOrWhiteSpace(m.ActionId))
                {
                    throw new InvalidOperationException(
                        $"Mapping references actionId '{m.ActionId!.Trim()}' but keyboardActions is missing or empty.");
                }
            }

            return;
        }

        var map = new Dictionary<string, KeyboardActionDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in catalog)
        {
            var id = (a.Id ?? string.Empty).Trim();
            if (id.Length == 0)
                throw new InvalidOperationException("A keyboardActions entry has an empty id.");

            if (!map.TryAdd(id, a))
                throw new InvalidOperationException($"Duplicate keyboardActions id '{id}'.");
        }

        foreach (var m in template.Mappings)
        {
            if (string.IsNullOrWhiteSpace(m.ActionId) && string.IsNullOrWhiteSpace(m.HoldActionId))
                continue;

            if (!string.IsNullOrWhiteSpace(m.ActionId))
            {
                if (m.ItemCycle is not null || m.TemplateToggle is not null || m.RadialMenu is not null)
                {
                    throw new InvalidOperationException(
                        "actionId cannot be used together with itemCycle, templateToggle, or radialMenu on the same mapping.");
                }

                var id = m.ActionId!.Trim();
                if (!map.TryGetValue(id, out var def))
                    throw new InvalidOperationException($"Unknown keyboardActions id '{id}' referenced by a mapping.");

                var key = (def.KeyboardKey ?? string.Empty).Trim();
                if (key.Length == 0 && def.TemplateToggle == null && def.RadialMenu == null && def.ItemCycle == null)
                    throw new InvalidOperationException($"keyboardActions id '{id}' has no keyboardKey, templateToggle, radialMenu, or itemCycle.");

                var desc = (def.Description ?? string.Empty).Trim();
                m.ApplyKeyboardActionResolution(key, desc.Length > 0 ? desc : null, def.TemplateToggle, def.RadialMenu, def.ItemCycle);
            }

            if (!string.IsNullOrWhiteSpace(m.HoldActionId))
            {
                var holdId = m.HoldActionId!.Trim();
                if (!map.TryGetValue(holdId, out var holdDef))
                    throw new InvalidOperationException($"Unknown keyboardActions id '{holdId}' referenced by a mapping (holdActionId).");

                var holdKey = (holdDef.KeyboardKey ?? string.Empty).Trim();
                m.ApplyHoldKeyboardActionResolution(holdKey, holdDef.TemplateToggle, holdDef.RadialMenu, holdDef.ItemCycle);
            }
        }
    }
}

