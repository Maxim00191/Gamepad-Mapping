using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Gamepad_Mapping.Interfaces.Services;
using Gamepad_Mapping.Models.Core.Visual;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Services;

public partial class ControllerVisualHighlightService : ObservableObject, IControllerVisualHighlightService
{
    private readonly IControllerVisualService _visualService;
    
    [ObservableProperty]
    private ControllerVisualSceneState _currentScene = new(Array.Empty<ControllerElementVisualState>());

    public ControllerVisualHighlightService(IControllerVisualService visualService)
    {
        _visualService = visualService;
    }

    public void UpdateContext(string? hoveredId, string? selectedId, IEnumerable<MappingEntry> mappings)
    {
        var elements = new List<ControllerElementVisualState>();
        var allMappedIds = _visualService.EnumerateMappedLogicalControlIds().ToList();
        
        // Determine chord participants if something is selected
        var chordIds = GetChordParticipants(selectedId, mappings);

        foreach (var id in allMappedIds)
        {
            var highlight = ControllerVisualHighlightKind.None;
            if (id == hoveredId) highlight = ControllerVisualHighlightKind.Hover;
            else if (id == selectedId) highlight = ControllerVisualHighlightKind.Selected;
            else if (chordIds.Contains(id)) highlight = ControllerVisualHighlightKind.ChordSecondary;

            // Dimming logic: if something is hovered or selected, dim others unless they are part of the chord
            bool isDimmed = false;
            if (!string.IsNullOrEmpty(hoveredId))
            {
                isDimmed = id != hoveredId;
            }
            else if (!string.IsNullOrEmpty(selectedId))
            {
                // If we have a selection, we only highlight the selection and its chord participants
                isDimmed = id != selectedId && !chordIds.Contains(id);
            }

            elements.Add(new ControllerElementVisualState(id, highlight, isDimmed));
        }

        CurrentScene = new ControllerVisualSceneState(elements);
    }

    private HashSet<string> GetChordParticipants(string? selectedId, IEnumerable<MappingEntry> mappings)
    {
        var participants = new HashSet<string>();
        if (string.IsNullOrEmpty(selectedId)) return participants;

        var selectedBinding = _visualService.MapIdToBinding(selectedId);
        if (selectedBinding == null) return participants;

        // Find mappings that involve this binding as part of a chord
        foreach (var mapping in mappings)
        {
            if (mapping.From == null || string.IsNullOrEmpty(mapping.From.Value)) continue;
            
            var parts = mapping.From.Value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length <= 1) continue;

            // Check if our selected binding is one of the parts
            bool involvesSelected = parts.Any(p => 
                string.Equals(p, selectedBinding.Value, StringComparison.OrdinalIgnoreCase));

            if (involvesSelected)
            {
                // Add all other parts of this chord to participants
                foreach (var part in parts)
                {
                    var partId = _visualService.MapBindingToId(part, mapping.From.Type);
                    if (partId != null && partId != selectedId)
                    {
                        participants.Add(partId);
                    }
                }
            }
        }

        return participants;
    }
}
