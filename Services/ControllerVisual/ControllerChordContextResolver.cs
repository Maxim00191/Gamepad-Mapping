#nullable enable

using System;
using System.Collections.Generic;
using Gamepad_Mapping.Interfaces.Services.ControllerVisual;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Services.ControllerVisual;

public sealed class ControllerChordContextResolver(IControllerVisualService visualService) : IControllerChordContextResolver
{
    private readonly IControllerVisualService _visualService = visualService;

    public HashSet<string> GetChordParticipantElementIds(string? selectedElementId, IEnumerable<MappingEntry> mappings)
    {
        var participants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(selectedElementId)) return participants;

        var selectedBinding = _visualService.MapIdToBinding(selectedElementId);
        if (selectedBinding is null) return participants;

        foreach (var mapping in mappings)
        {
            if (mapping.From is null || string.IsNullOrEmpty(mapping.From.Value)) continue;

            var parts = mapping.From.Value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length <= 1) continue;

            var involvesSelected = parts.Any(p =>
                string.Equals(p, selectedBinding.Value, StringComparison.OrdinalIgnoreCase));

            if (!involvesSelected) continue;

            foreach (var part in parts)
            {
                var partId = _visualService.MapChordSegmentToLogicalControlId(part);
                if (partId is not null && !string.Equals(partId, selectedElementId, StringComparison.OrdinalIgnoreCase))
                    participants.Add(partId);
            }
        }

        return participants;
    }

    public MappingEntry? FindChordMappingBetween(
        string? selectedElementId,
        string elementId,
        IEnumerable<MappingEntry> mappings)
    {
        if (string.IsNullOrEmpty(selectedElementId) || string.IsNullOrEmpty(elementId)) return null;
        if (string.Equals(selectedElementId, elementId, StringComparison.OrdinalIgnoreCase)) return null;

        var selectedBinding = _visualService.MapIdToBinding(selectedElementId);
        var elementBinding = _visualService.MapIdToBinding(elementId);
        if (selectedBinding is null || elementBinding is null) return null;

        foreach (var mapping in mappings)
        {
            if (mapping.From is null || string.IsNullOrEmpty(mapping.From.Value)) continue;

            var parts = mapping.From.Value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length <= 1) continue;

            var hasSelected = parts.Any(p =>
                string.Equals(p, selectedBinding.Value, StringComparison.OrdinalIgnoreCase));
            var hasElement = parts.Any(p =>
                string.Equals(p, elementBinding.Value, StringComparison.OrdinalIgnoreCase));

            if (hasSelected && hasElement)
                return mapping;
        }

        return null;
    }
}
