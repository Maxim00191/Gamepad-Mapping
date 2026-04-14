#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Gamepad_Mapping.Interfaces.Services.ControllerVisual;
using Gamepad_Mapping.Models.Core.Visual;
using Gamepad_Mapping.Utils.ControllerVisual;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Services.ControllerVisual;

public sealed class ControllerMappingOverlayLabelComposer(
    IControllerVisualService visualService,
    IControllerChordContextResolver chordContextResolver) : IControllerMappingOverlayLabelComposer
{
    private readonly IControllerVisualService _visual = visualService;
    private readonly IControllerChordContextResolver _chords = chordContextResolver;

    public ControllerMappingOverlayLabelSnapshot Compose(
        string elementId,
        IReadOnlyList<MappingEntry> elementMappings,
        IEnumerable<MappingEntry> allMappings,
        string? selectedElementId,
        ControllerMappingOverlayPrimaryLabelMode primaryLabelMode,
        bool overlayShowSecondary)
    {
        if (elementMappings.Count == 0)
        {
            return new ControllerMappingOverlayLabelSnapshot(
                string.Empty,
                null,
                false,
                false,
                null,
                false);
        }

        var chordPartnerMapping = TryGetChordPartnerMapping(elementId, selectedElementId, allMappings);
        if (chordPartnerMapping is not null)
            return ComposeChordPartner(
                elementId,
                elementMappings,
                chordPartnerMapping,
                primaryLabelMode,
                overlayShowSecondary);

        return ComposeStandard(elementId, elementMappings, primaryLabelMode, overlayShowSecondary);
    }

    private MappingEntry? TryGetChordPartnerMapping(
        string elementId,
        string? selectedElementId,
        IEnumerable<MappingEntry> allMappings)
    {
        if (string.IsNullOrEmpty(selectedElementId)) return null;
        if (string.Equals(selectedElementId, elementId, StringComparison.OrdinalIgnoreCase)) return null;
        return _chords.FindChordMappingBetween(selectedElementId, elementId, allMappings);
    }

    private ControllerMappingOverlayLabelSnapshot ComposeChordPartner(
        string elementId,
        IReadOnlyList<MappingEntry> elementMappings,
        MappingEntry chordMapping,
        ControllerMappingOverlayPrimaryLabelMode primaryLabelMode,
        bool overlayShowSecondary)
    {
        var displayName = _visual.GetDisplayName(elementId) ?? elementId;
        var normalizedDisplay = ControllerMappingOverlayLabelText.NormalizeForOverlay(displayName);
        var normalizedSummary =
            ControllerMappingOverlayLabelText.NormalizeForOverlay(chordMapping.OutputSummaryForControllerOverlay);

        var chordLine = FormatChordDisplayLine(chordMapping);
        var extrasOnElement = elementMappings.Count(m => !SameFrom(m, chordMapping));
        var extraMappingCount = overlayShowSecondary && extrasOnElement > 0 ? extrasOnElement : 0;

        string primaryText;
        string? secondary;
        var stackLabels = false;

        switch (primaryLabelMode)
        {
            case ControllerMappingOverlayPrimaryLabelMode.PhysicalControl:
                primaryText = normalizedDisplay;
                secondary = BuildChordSecondaryLine(chordLine, extraMappingCount);
                break;

            case ControllerMappingOverlayPrimaryLabelMode.ActionAndPhysicalControl:
            {
                var actionLine = string.IsNullOrWhiteSpace(normalizedSummary) ? normalizedDisplay : normalizedSummary;
                primaryText = actionLine;
                if (string.Equals(actionLine, normalizedDisplay, StringComparison.Ordinal))
                {
                    secondary = BuildChordSecondaryLine(chordLine, extraMappingCount);
                }
                else
                {
                    stackLabels = true;
                    secondary = BuildStackedActionPhysicalChordSecondary(
                        normalizedDisplay,
                        chordLine,
                        extraMappingCount);
                }

                break;
            }

            default:
                primaryText = string.IsNullOrWhiteSpace(normalizedSummary) ? normalizedDisplay : normalizedSummary;
                secondary = BuildChordSecondaryLine(chordLine, extraMappingCount);
                if (!string.IsNullOrEmpty(secondary) && !string.IsNullOrWhiteSpace(primaryText))
                    stackLabels = true;
                break;
        }

        var hasExtraMappings = extraMappingCount > 0;
        var toolTip = BuildOverlayToolTip(normalizedDisplay, normalizedSummary, secondary);
        var isCombination = !string.IsNullOrEmpty(chordMapping.From?.Value) && chordMapping.From.Value.Contains('+');

        return new ControllerMappingOverlayLabelSnapshot(
            primaryText,
            secondary,
            stackLabels,
            hasExtraMappings,
            toolTip,
            isCombination);
    }

    private static string? BuildChordSecondaryLine(string chordLine, int extraMappingCount)
    {
        if (string.IsNullOrEmpty(chordLine) && extraMappingCount <= 0) return null;
        if (string.IsNullOrEmpty(chordLine))
            return $"+{extraMappingCount}";
        if (extraMappingCount <= 0)
            return chordLine;
        return $"{chordLine}{ControllerMappingOverlayFormatting.AuxiliarySeparator}+{extraMappingCount}";
    }

    private static string? BuildStackedActionPhysicalChordSecondary(
        string normalizedDisplay,
        string chordLine,
        int extraMappingCount)
    {
        var core = $"{normalizedDisplay}{ControllerMappingOverlayFormatting.AuxiliarySeparator}{chordLine}";
        if (extraMappingCount <= 0) return core;
        return $"{core}{ControllerMappingOverlayFormatting.AuxiliarySeparator}+{extraMappingCount}";
    }

    private ControllerMappingOverlayLabelSnapshot ComposeStandard(
        string elementId,
        IReadOnlyList<MappingEntry> elementMappings,
        ControllerMappingOverlayPrimaryLabelMode primaryLabelMode,
        bool overlayShowSecondary)
    {
        var primaryMapping = elementMappings[0];
        var displayName = _visual.GetDisplayName(elementId) ?? elementId;
        var actionSummary = primaryMapping.OutputSummaryForControllerOverlay ?? string.Empty;
        var normalizedDisplay = ControllerMappingOverlayLabelText.NormalizeForOverlay(displayName);
        var normalizedSummary = ControllerMappingOverlayLabelText.NormalizeForOverlay(actionSummary);

        var extraMappingCount = overlayShowSecondary && elementMappings.Count > 1 ? elementMappings.Count - 1 : 0;

        string primaryText;
        string? secondary;
        var stackLabels = false;

        if (primaryLabelMode == ControllerMappingOverlayPrimaryLabelMode.ActionAndPhysicalControl)
        {
            var actionLine = string.IsNullOrWhiteSpace(normalizedSummary) ? normalizedDisplay : normalizedSummary;
            primaryText = actionLine;
            if (string.Equals(actionLine, normalizedDisplay, StringComparison.Ordinal))
            {
                secondary = extraMappingCount > 0 ? $"+{extraMappingCount}" : null;
            }
            else
            {
                stackLabels = true;
                secondary = extraMappingCount > 0
                    ? $"{normalizedDisplay}{ControllerMappingOverlayFormatting.AuxiliarySeparator}+{extraMappingCount}"
                    : normalizedDisplay;
            }
        }
        else
        {
            primaryText = primaryLabelMode == ControllerMappingOverlayPrimaryLabelMode.PhysicalControl
                ? normalizedDisplay
                : (string.IsNullOrWhiteSpace(normalizedSummary) ? normalizedDisplay : normalizedSummary);
            secondary = extraMappingCount > 0 ? $"+{extraMappingCount}" : null;
        }

        var toolTip = BuildOverlayToolTip(normalizedDisplay, normalizedSummary, secondary);
        var isCombination = !string.IsNullOrEmpty(primaryMapping.From?.Value) && primaryMapping.From.Value.Contains('+');

        return new ControllerMappingOverlayLabelSnapshot(
            primaryText,
            secondary,
            stackLabels,
            extraMappingCount > 0,
            toolTip,
            isCombination);
    }

    private string FormatChordDisplayLine(MappingEntry mapping)
    {
        if (mapping.From is null || string.IsNullOrEmpty(mapping.From.Value)) return string.Empty;

        var parts = mapping.From.Value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return string.Empty;

        var labels = new List<string>(parts.Length);
        foreach (var p in parts)
        {
            var id = _visual.MapChordSegmentToLogicalControlId(p) ?? _visual.MapBindingToId(p, mapping.From.Type);
            var label = id is not null ? _visual.GetDisplayName(id) : p;
            labels.Add(ControllerMappingOverlayLabelText.NormalizeForOverlay(label));
        }

        return string.Join(ControllerMappingOverlayFormatting.ChordPartSeparator, labels);
    }

    private static bool SameFrom(MappingEntry a, MappingEntry b) =>
        a.From?.Type == b.From?.Type
        && string.Equals(a.From?.Value, b.From?.Value, StringComparison.Ordinal);

    private static string? BuildOverlayToolTip(string normalizedDisplay, string normalizedSummary, string? secondary)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(normalizedDisplay))
            parts.Add(normalizedDisplay);
        if (!string.IsNullOrWhiteSpace(normalizedSummary) && !string.Equals(normalizedSummary, normalizedDisplay, StringComparison.Ordinal))
            parts.Add(normalizedSummary);
        if (!string.IsNullOrWhiteSpace(secondary))
            parts.Add(secondary);
        return parts.Count > 0 ? string.Join(Environment.NewLine, parts) : null;
    }
}
