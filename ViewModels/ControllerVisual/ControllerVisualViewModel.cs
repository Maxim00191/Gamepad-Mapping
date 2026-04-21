using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gamepad_Mapping.Interfaces.Services.ControllerVisual;
using Gamepad_Mapping.Models.Core.Visual;
using Gamepad_Mapping.Services.ControllerVisual;
using Gamepad_Mapping.Utils.ControllerVisual;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.ControllerVisual;

namespace Gamepad_Mapping.ViewModels.ControllerVisual;

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

    private readonly IControllerVisualLayoutHelper _layoutHelper;

    private readonly IControllerMappingOverlayLabelComposer _mappingOverlayLabelComposer;

    public ControllerMappingOverlayPrimaryLabelMode OverlayPrimaryLabelMode { get; set; } =
        ControllerMappingOverlayLabelModeParser.DefaultMode;

    public bool OverlayShowSecondary { get; set; } = true;

    public ObservableCollection<ControllerMappingOverlayItem> OverlayItems { get; } = [];

    public IControllerVisualLoader Loader { get; }

    public IControllerVisualHighlightService HighlightService { get; }

    private IEnumerable<MappingEntry>? _lastMappings;

    public ControllerVisualViewModel(
        IControllerVisualService visualService,
        IControllerVisualLayoutSource layoutSource,
        IControllerVisualLoader loader,
        IControllerVisualHighlightService highlightService,
        IControllerMappingOverlayLabelComposer mappingOverlayLabelComposer,
        IControllerVisualLayoutHelper? layoutHelper = null)
    {
        _visualService = visualService;
        _layoutSource = layoutSource;
        _layoutHelper = layoutHelper ?? new ControllerVisualLayoutHelper();
        _mappingOverlayLabelComposer = mappingOverlayLabelComposer;
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
        
        if (_visualService.MapIdToBinding(elementId) is null) return;

        SelectedElementName = elementId;
    }

    public void RequestCreateMapping()
    {
        if (string.IsNullOrEmpty(SelectedElementName)) return;

        if (_visualService.MapIdToBinding(SelectedElementName) is null) return;

        OnPropertyChanged(nameof(SelectedElementName));
    }

    private void ApplySceneToOverlayItems(ControllerVisualSceneState scene)
    {
        var isAnyHovered = scene.Elements.Any(e => e.Highlight == ControllerVisualHighlightKind.Hover);

        foreach (var elementState in scene.Elements)
        {
            var item = OverlayItems.FirstOrDefault(i => i.ElementId == elementState.ElementId);
            if (item != null)
            {
                item.IsHovered = elementState.Highlight == ControllerVisualHighlightKind.Hover;
                item.IsSelected = elementState.Highlight == ControllerVisualHighlightKind.Selected;
                item.IsDimmed = elementState.IsDimmed;
                item.IsChordPart = elementState.Highlight == ControllerVisualHighlightKind.ChordSecondary;

                item.IsLeaderLineVisible = !isAnyHovered || item.IsHovered || item.IsSelected;
            }
        }
    }

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
            item.EstimatedWidth = labelSize.Width;
            item.EstimatedHeight = labelSize.Height;
        }

        _layoutHelper.ArrangeOverlayItems(OverlayItems, labelSizes, viewport);
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

        var pad = 96d;
        if (labelSizes.Length > 0)
        {
            var maxW = labelSizes.Max(z => z.Width);
            var maxH = labelSizes.Max(z => z.Height);
            pad = Math.Max(pad, Math.Max(maxW, maxH) + 24d);
        }

        return new Rect(-pad, -pad, w + 2d * pad, h + 2d * pad);
    }

    public void UpdateOverlay(IEnumerable<MappingEntry> mappings)
    {
        _lastMappings = mappings;
        var allMappings = mappings as IReadOnlyList<MappingEntry> ?? mappings.ToList();
        var items = new List<ControllerMappingOverlayItem>();
        foreach (var elementId in _visualService.EnumerateMappedLogicalControlIds())
        {
            var elementMappings = _visualService.GetMappingsForElement(elementId, allMappings).ToList();
            if (elementMappings.Count == 0) continue;

            var snap = _mappingOverlayLabelComposer.Compose(
                elementId,
                elementMappings,
                allMappings,
                SelectedElementName,
                OverlayPrimaryLabelMode,
                OverlayShowSecondary);

            var item = new ControllerMappingOverlayItem
            {
                ElementId = elementId,
                PrimaryLabel = snap.PrimaryLabel,
                SecondaryLabel = snap.SecondaryLabel,
                StackPrimaryAndSecondary = snap.StackPrimaryAndSecondary,
                HasExtraMappings = snap.HasExtraMappings,
                OverlayToolTip = snap.OverlayToolTip,
                IsCombination = snap.IsCombination
            };
            items.Add(item);
        }

        OverlayItems.Clear();
        foreach (var item in items) OverlayItems.Add(item);

        ApplyOverlayAnchorsToItems();
        UpdateVisualStates();

        if (!string.IsNullOrEmpty(HoveredElementId))
            HoveredElementName = ResolveInteractionLabel(HoveredElementId);
    }

    public void UpdateVisualStates()
    {
        UpdateHighlightService();
    }

    partial void OnHoveredElementIdChanged(string? value) => UpdateVisualStates();

    partial void OnSelectedElementNameChanged(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            HoveredElementId = null;
            HoveredElementName = null;
        }

        if (_lastMappings is not null)
            UpdateOverlay(_lastMappings);
        else
            UpdateHighlightService();
    }

    public void RefreshActiveLayout()
    {
        ActiveLayout = _layoutSource.GetActiveLayout();
    }

    public string GetElementDisplayName(string logicalId) =>
        _visualService.GetDisplayName(logicalId) ?? logicalId;

    public string GetElementInteractionLabel(string logicalId) => ResolveInteractionLabel(logicalId);

    private string ResolveInteractionLabel(string logicalId)
    {
        if (_lastMappings is not null)
        {
            var mapped = _visualService.GetMappingsForElement(logicalId, _lastMappings).FirstOrDefault();
            if (mapped is not null)
            {
                var label = ControllerMappingOverlayLabelText.NormalizeForOverlay(mapped.OutputSummaryForControllerOverlay);
                if (!string.IsNullOrWhiteSpace(label))
                    return label;
            }
        }

        return _visualService.GetDisplayName(logicalId) ?? logicalId;
    }

    [RelayCommand]
    private void SelectElement(string? elementId)
    {
        SelectedElementName = elementId;
    }

    [RelayCommand]
    private void HoverElement(string? elementId)
    {
        HoveredElementId = elementId;
        HoveredElementName = string.IsNullOrEmpty(elementId) ? null : ResolveInteractionLabel(elementId);
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
