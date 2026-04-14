#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Gamepad_Mapping.Interfaces.Services.ControllerVisual;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Services.ControllerVisual;

public sealed class MappingsForLogicalControlQuery(IControllerVisualService visualService) : IMappingsForLogicalControlQuery
{
    private readonly IControllerVisualService _visual = visualService;

    public IReadOnlyList<MappingEntry> GetMappingsForLogicalControl(string elementId, IEnumerable<MappingEntry> mappings)
    {
        var list = mappings.Where(m => MappingInvolvesLogicalControl(m, elementId)).ToList();
        list.Sort(CompareMappingOrder);
        return list;
    }

    public bool MappingInvolvesLogicalControl(MappingEntry mapping, string elementId)
    {
        var binding = _visual.MapIdToBinding(elementId);
        if (binding is null || mapping.From is null)
            return false;

        if (mapping.From.Type != binding.Type)
            return false;

        var value = mapping.From.Value ?? string.Empty;
        if (value.IndexOf('+', StringComparison.Ordinal) < 0)
            return string.Equals(value, binding.Value, StringComparison.OrdinalIgnoreCase);

        var parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Any(p => string.Equals(p, binding.Value, StringComparison.OrdinalIgnoreCase));
    }

    public string? ResolvePrimaryLogicalControlIdForMapping(MappingEntry mapping)
    {
        if (mapping.From is null || string.IsNullOrEmpty(mapping.From.Value))
            return null;

        var type = mapping.From.Type;
        var value = mapping.From.Value;
        if (value.IndexOf('+', StringComparison.Ordinal) < 0)
            return _visual.MapBindingToId(value, type);

        foreach (var p in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var id = _visual.MapBindingToId(p, type);
            if (id is not null)
                return id;
        }

        return _visual.MapBindingToId(value, type);
    }

    private static int CompareMappingOrder(MappingEntry a, MappingEntry b)
    {
        var aChord = IsChord(a);
        var bChord = IsChord(b);
        if (aChord != bChord)
            return aChord ? 1 : -1;

        var av = a.From?.Value ?? string.Empty;
        var bv = b.From?.Value ?? string.Empty;
        var c = string.Compare(av, bv, StringComparison.OrdinalIgnoreCase);
        if (c != 0)
            return c;

        var ao = a.OutputSummaryForControllerOverlay ?? string.Empty;
        var bo = b.OutputSummaryForControllerOverlay ?? string.Empty;
        return string.Compare(ao, bo, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsChord(MappingEntry m) =>
        m.From?.Value?.IndexOf('+', StringComparison.Ordinal) >= 0;
}
