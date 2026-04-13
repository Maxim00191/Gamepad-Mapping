using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gamepad_Mapping.Interfaces.Services;
using Gamepad_Mapping.Models.Core.Visual;
using Gamepad_Mapping.Services;
using Gamepad_Mapping.Utils.ControllerSvg;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.ControllerVisual;

namespace Gamepad_Mapping.ViewModels;

public partial class ControllerVisualViewModel : ObservableObject
{
    private readonly IControllerVisualService _visualService;
    private readonly IControllerVisualLayoutSource _layoutSource;

    [ObservableProperty]
    private string? _hoveredElementName;

    [ObservableProperty]
    private string? _hoveredElementId;

    [ObservableProperty]
    private string? _selectedElementName;

    [ObservableProperty]
    private MappingEntry? _selectedMapping;

    [ObservableProperty]
    private ControllerVisualLayoutDescriptor _activeLayout;

    private IReadOnlyDictionary<string, Point>? _overlayAnchorPositions;

    private Size? _overlayLayoutViewport;

    public ControllerMappingOverlayPrimaryLabelMode OverlayPrimaryLabelMode { get; set; } =
        ControllerMappingOverlayPrimaryLabelMode.ActionSummary;

    public bool OverlayShowSecondary { get; set; } = true;

    public ObservableCollection<ControllerMappingOverlayItem> OverlayItems { get; } = [];

    public IControllerVisualLoader Loader { get; }

    public IControllerVisualHighlightService HighlightService { get; }

    private IEnumerable<MappingEntry>? _lastMappings;

    public ControllerVisualViewModel(
        IControllerVisualService visualService,
        IControllerVisualLayoutSource layoutSource,
        IControllerVisualLoader loader,
        IControllerVisualHighlightService highlightService)
    {
        _visualService = visualService;
        _layoutSource = layoutSource;
        Loader = loader;
        HighlightService = highlightService;
        _activeLayout = layoutSource.GetActiveLayout();
        
        HighlightService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IControllerVisualHighlightService.CurrentScene))
            {
                ApplySceneToOverlayItems(HighlightService.CurrentScene);
            }
        };
    }

    [RelayCommand]
    private void CreateMappingForElement(string? elementId)
    {
        if (string.IsNullOrEmpty(elementId)) return;
        
        var binding = _visualService.MapIdToBinding(elementId);
        if (binding == null) return;

        // Notify that we want to create a mapping for this binding
        // This is handled by the MappingEditorViewModel which subscribes to changes
        SelectedElementName = elementId;
    }

    public void RequestCreateMapping()
    {
        if (string.IsNullOrEmpty(SelectedElementName)) return;
        
        var binding = _visualService.MapIdToBinding(SelectedElementName);
        if (binding == null) return;

        // This will be picked up by MappingEditorViewModel's property change listener
        // which calls BeginCreateNewMapping
        OnPropertyChanged(nameof(SelectedElementName)); 
    }

    private void ApplySceneToOverlayItems(ControllerVisualSceneState scene)
    {
        // Update existing OverlayItems based on the scene state
        foreach (var elementState in scene.Elements)
        {
            var item = OverlayItems.FirstOrDefault(i => i.ElementId == elementState.ElementId);
            if (item != null)
            {
                item.IsHovered = elementState.Highlight == ControllerVisualHighlightKind.Hover;
                item.IsSelected = elementState.Highlight == ControllerVisualHighlightKind.Selected;
                item.IsDimmed = elementState.IsDimmed;
                item.IsChordPart = elementState.Highlight == ControllerVisualHighlightKind.ChordSecondary;
            }
        }
    }

    private readonly IControllerVisualLayoutHelper _layoutHelper = new ControllerVisualLayoutHelper();

    public void ApplyOverlayAnchorPositions(IReadOnlyDictionary<string, Point> positions, Size? layoutViewport = null)
    {
        _overlayAnchorPositions = positions;
        _overlayLayoutViewport = layoutViewport;
        ApplyOverlayAnchorsToItems();
    }

    private void ApplyOverlayAnchorsToItems()
    {
        if (_overlayAnchorPositions is null) return;

        var labelSizes = OverlayItems
            .Select(i => ControllerMappingOverlayLabelSizeEstimator.Estimate(i))
            .ToArray();

        var viewport = BuildOverlayLayoutRect(_overlayLayoutViewport, labelSizes);

        for (var index = 0; index < OverlayItems.Count; index++)
        {
            var item = OverlayItems[index];
            var labelSize = labelSizes[index];

            if (!_overlayAnchorPositions.TryGetValue(item.ElementId, out var p))
                continue;

            item.X = p.X;
            item.Y = p.Y;

            var layoutResult = _layoutHelper.CalculateLayout(item.ElementId, p, labelSize, viewport);

            item.Quadrant = layoutResult.Quadrant;
            item.LabelX = layoutResult.LabelBoxPosition.X - p.X;
            item.LabelY = layoutResult.LabelBoxPosition.Y - p.Y;

            item.LeaderLinePoints = layoutResult.LeaderLinePoints
                .Select(q => new Point(q.X - p.X, q.Y - p.Y))
                .ToArray();
        }

        _layoutHelper.ResolveOverlaps(OverlayItems, viewport, labelSizes);
    }

    private static Rect BuildOverlayLayoutRect(Size? diagramSize, Size[] labelSizes)
    {
        const double defaultW = 300d;
        const double defaultH = 250d;
        var w = defaultW;
        var h = defaultH;
        if (diagramSize is { Width: > 0, Height: > 0 } ds)
        {
            w = ds.Width;
            h = ds.Height;
        }

        var pad = 80d;
        if (labelSizes.Length > 0)
        {
            var maxW = labelSizes.Max(z => z.Width);
            var maxH = labelSizes.Max(z => z.Height);
            pad = Math.Max(pad, Math.Max(maxW, maxH) + 16d);
        }

        return new Rect(-pad, -pad, w + 2d * pad, h + 2d * pad);
    }

    public void UpdateOverlay(IEnumerable<MappingEntry> mappings)
    {
        _lastMappings = mappings;
        var items = new List<ControllerMappingOverlayItem>();
        foreach (var elementId in _visualService.EnumerateMappedLogicalControlIds())
        {
            var elementMappings = _visualService.GetMappingsForElement(elementId, mappings).ToList();
            if (elementMappings.Count == 0) continue;

            var primaryMapping = elementMappings[0];
            var displayName = _visualService.GetDisplayName(elementId) ?? elementId;
            var actionSummary = primaryMapping.OutputSummaryForGrid ?? string.Empty;
            var primaryText = OverlayPrimaryLabelMode == ControllerMappingOverlayPrimaryLabelMode.PhysicalControl
                ? displayName
                : actionSummary;

            string? secondary = null;
            if (OverlayShowSecondary && elementMappings.Count > 1)
                secondary = $"+{elementMappings.Count - 1}";

            var item = new ControllerMappingOverlayItem
            {
                ElementId = elementId,
                PrimaryLabel = primaryText,
                SecondaryLabel = secondary,
                OverlayToolTip = BuildOverlayToolTip(displayName, actionSummary, secondary),
                IsCombination = !string.IsNullOrEmpty(primaryMapping.From?.Value) && primaryMapping.From.Value.Contains('+')
            };
            items.Add(item);
        }

        OverlayItems.Clear();
        foreach (var item in items) OverlayItems.Add(item);

        ApplyOverlayAnchorsToItems();
        UpdateVisualStates();
    }

    private static string? BuildOverlayToolTip(string displayName, string actionSummary, string? secondary)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(displayName))
            parts.Add(displayName);
        if (!string.IsNullOrWhiteSpace(actionSummary) && !string.Equals(actionSummary, displayName, StringComparison.Ordinal))
            parts.Add(actionSummary);
        if (!string.IsNullOrWhiteSpace(secondary))
            parts.Add(secondary);
        return parts.Count > 0 ? string.Join(Environment.NewLine, parts) : null;
    }

    public void UpdateVisualStates()
    {
        UpdateHighlightService();
    }

    partial void OnHoveredElementIdChanged(string? value) => UpdateVisualStates();
    partial void OnSelectedElementNameChanged(string? value) => UpdateVisualStates();

    public void RefreshActiveLayout()
    {
        ActiveLayout = _layoutSource.GetActiveLayout();
    }

    public string GetElementDisplayName(string logicalId) =>
        _visualService.GetDisplayName(logicalId) ?? logicalId;

    [RelayCommand]
    private void SelectElement(string? elementId)
    {
        SelectedElementName = elementId;
        UpdateHighlightService();
    }

    [RelayCommand]
    private void HoverElement(string? elementId)
    {
        HoveredElementId = elementId;
        HoveredElementName = string.IsNullOrEmpty(elementId) ? null : _visualService.GetDisplayName(elementId);
        UpdateHighlightService();
    }

    private void UpdateHighlightService()
    {
        HighlightService.UpdateContext(
            HoveredElementId,
            SelectedElementName,
            _lastMappings ?? Enumerable.Empty<MappingEntry>());
    }
}
