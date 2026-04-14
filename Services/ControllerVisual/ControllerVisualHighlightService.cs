using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Gamepad_Mapping.Interfaces.Services.ControllerVisual;
using Gamepad_Mapping.Models.Core.Visual;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Services.ControllerVisual;

public partial class ControllerVisualHighlightService : ObservableObject, IControllerVisualHighlightService
{
    private readonly IControllerVisualService _visualService;
    private readonly IControllerChordContextResolver _chordContextResolver;

    [ObservableProperty]
    private ControllerVisualSceneState _currentScene = new(Array.Empty<ControllerElementVisualState>());

    public ControllerVisualHighlightService(
        IControllerVisualService visualService,
        IControllerChordContextResolver chordContextResolver)
    {
        _visualService = visualService;
        _chordContextResolver = chordContextResolver;
    }

    public void UpdateContext(string? hoveredId, string? selectedId, IEnumerable<MappingEntry> mappings)
    {
        var elements = new List<ControllerElementVisualState>();
        var allMappedIds = _visualService.EnumerateMappedLogicalControlIds().ToList();

        var chordIds = _chordContextResolver.GetChordParticipantElementIds(selectedId, mappings);

        foreach (var id in allMappedIds)
        {
            var highlight = ControllerVisualHighlightKind.None;
            if (id == hoveredId) highlight = ControllerVisualHighlightKind.Hover;
            else if (id == selectedId) highlight = ControllerVisualHighlightKind.Selected;
            else if (chordIds.Contains(id)) highlight = ControllerVisualHighlightKind.ChordSecondary;

            var isDimmed = false;
            if (!string.IsNullOrEmpty(hoveredId))
            {
                isDimmed = id != hoveredId;
            }
            else if (!string.IsNullOrEmpty(selectedId))
            {
                isDimmed = id != selectedId && !chordIds.Contains(id);
            }

            elements.Add(new ControllerElementVisualState(id, highlight, isDimmed));
        }

        CurrentScene = new ControllerVisualSceneState(elements);
    }
}
