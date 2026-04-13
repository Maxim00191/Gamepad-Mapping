using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Gamepad_Mapping.Models.Core.Visual;

/// <summary>
/// A read model representing a mapping overlay item for the controller diagram.
/// </summary>
public partial class ControllerMappingOverlayItem : ObservableObject
{
    [ObservableProperty]
    private string _elementId = string.Empty;

    [ObservableProperty]
    private string _primaryLabel = string.Empty;

    [ObservableProperty]
    private string? _secondaryLabel;

    [ObservableProperty]
    private bool _isCombination;

    [ObservableProperty]
    private bool _isHovered;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isDimmed;

    [ObservableProperty]
    private Point[] _leaderLinePoints = Array.Empty<Point>();

    [ObservableProperty]
    private double _labelX;

    [ObservableProperty]
    private double _labelY;

    [ObservableProperty]
    private ControllerLabelQuadrant _quadrant;

    [ObservableProperty]
    private bool _isChordPart;

    [ObservableProperty]
    private string? _overlayToolTip;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;
}
