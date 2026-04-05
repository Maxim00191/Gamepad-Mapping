using System;
using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services;

/// <summary>Resolves <see cref="MappingEntry.ActionId"/> using <see cref="GameProfileTemplate.KeyboardActions"/> after JSON load.</summary>
public static class TemplateKeyboardActionResolver
{
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
            if (string.IsNullOrWhiteSpace(m.ActionId))
                continue;

            if (m.ItemCycle is not null || m.TemplateToggle is not null || m.RadialMenu is not null)
            {
                throw new InvalidOperationException(
                    "actionId cannot be used together with itemCycle, templateToggle, or radialMenu on the same mapping.");
            }

            var id = m.ActionId!.Trim();
            if (!map.TryGetValue(id, out var def))
                throw new InvalidOperationException($"Unknown keyboardActions id '{id}' referenced by a mapping.");

            var key = (def.KeyboardKey ?? string.Empty).Trim();
            if (key.Length == 0 && def.TemplateToggle == null)
                throw new InvalidOperationException($"keyboardActions id '{id}' has no keyboardKey or templateToggle.");

            var desc = (def.Description ?? string.Empty).Trim();
            m.ApplyKeyboardActionResolution(key, desc.Length > 0 ? desc : null, def.TemplateToggle);
        }
    }
}
