using System;
using System.Collections.Generic;
using System.Windows;

namespace Gamepad_Mapping.Models.Core.Visual;

/// <summary>
/// Defines the layout strategy for a callout label.
/// </summary>
public enum ControllerLabelQuadrant
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

/// <summary>
/// Result of a label layout calculation.
/// </summary>
public record ControllerLabelLayoutResult(
    Point Anchor,
    Point LabelBoxPosition,
    Point[] LeaderLinePoints,
    ControllerLabelQuadrant Quadrant
);

/// <summary>
/// Calculates label placements and leader lines for controller SVG overlay callouts.
/// </summary>
public interface IControllerVisualLayoutHelper
{
    ControllerLabelLayoutResult CalculateLayout(
        string elementId,
        Point anchor,
        Size labelSize,
        Rect viewport);

    void ResolveOverlaps(
        IList<ControllerMappingOverlayItem> items,
        Rect viewport,
        IReadOnlyList<Size> labelSizes);
}
