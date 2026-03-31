using System.Collections.Generic;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

internal static class ComboLeadSemantics
{
    /// <summary>
    /// When <paramref name="explicitFromTemplate"/> is non-null (possibly empty), those buttons are the combo leads.
    /// When null, leads are <see cref="InferFromMappings"/> from the current profile.
    /// </summary>
    public static HashSet<GamepadButtons> ResolveLeads(
        IReadOnlyCollection<MappingEntry> mappings,
        HashSet<GamepadButtons>? explicitFromTemplate)
    {
        if (explicitFromTemplate is not null)
            return explicitFromTemplate;
        return InferFromMappings(mappings);
    }

    /// <summary>
    /// Non–face buttons that appear in any chord with specificity ≥ 2 (modifier-style, template-derived default).
    /// </summary>
    public static HashSet<GamepadButtons> InferFromMappings(IReadOnlyCollection<MappingEntry> mappings)
    {
        var leads = new HashSet<GamepadButtons>();
        foreach (var mapping in mappings)
        {
            if (mapping?.From is null || mapping.From.Type != GamepadBindingType.Button)
                continue;
            if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var chord, out var reqRt, out var reqLt, out _))
                continue;
            if (ChordResolver.ChordSpecificity(chord, reqRt, reqLt) < 2)
                continue;
            foreach (var b in chord)
            {
                if (!ChordResolver.IsFaceActionButton(b))
                    leads.Add(b);
            }
        }

        return leads;
    }

    public static HashSet<GamepadButtons>? ParseDeclaredNames(IReadOnlyList<string>? declaredNames)
    {
        if (declaredNames is null)
            return null;
        var parsed = new HashSet<GamepadButtons>();
        foreach (var name in declaredNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            if (Enum.TryParse<GamepadButtons>(name.Trim(), ignoreCase: true, out var b) && b != GamepadButtons.None)
                parsed.Add(b);
        }

        return parsed;
    }
}
