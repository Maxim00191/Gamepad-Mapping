#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Gamepad_Mapping.Models.Core.Visual;

namespace Gamepad_Mapping.Services;

public class ControllerVisualLayoutHelper : IControllerVisualLayoutHelper
{
    private const double HorizontalOffset = 60;
    private const double VerticalOffset = 40;
    private const double Margin = 10;

    public ControllerLabelLayoutResult CalculateLayout(
        string elementId,
        Point anchor,
        Size labelSize,
        Rect viewport)
    {
        var centerX = viewport.X + viewport.Width / 2;
        var centerY = viewport.Y + viewport.Height / 2;

        var isLeft = anchor.X < centerX;
        var isTop = anchor.Y < centerY;

        double labelX;
        double labelY;

        if (isLeft)
        {
            labelX = Math.Max(viewport.Left + Margin, anchor.X - HorizontalOffset - labelSize.Width);
        }
        else
        {
            labelX = Math.Min(viewport.Right - labelSize.Width - Margin, anchor.X + HorizontalOffset);
        }

        if (isTop)
        {
            labelY = Math.Max(viewport.Top + Margin, anchor.Y - VerticalOffset - labelSize.Height);
        }
        else
        {
            labelY = Math.Min(viewport.Bottom - labelSize.Height - Margin, anchor.Y + VerticalOffset);
        }

        var labelPos = new Point(labelX, labelY);
        var quadrant = isLeft
            ? (isTop ? ControllerLabelQuadrant.TopLeft : ControllerLabelQuadrant.BottomLeft)
            : (isTop ? ControllerLabelQuadrant.TopRight : ControllerLabelQuadrant.BottomRight);

        Point connectionPoint;
        Point elbowPoint;

        switch (quadrant)
        {
            case ControllerLabelQuadrant.TopLeft:
                connectionPoint = new Point(labelX + labelSize.Width, labelY + labelSize.Height);
                elbowPoint = new Point(connectionPoint.X, anchor.Y);
                break;
            case ControllerLabelQuadrant.TopRight:
                connectionPoint = new Point(labelX, labelY + labelSize.Height);
                elbowPoint = new Point(connectionPoint.X, anchor.Y);
                break;
            case ControllerLabelQuadrant.BottomLeft:
                connectionPoint = new Point(labelX + labelSize.Width, labelY);
                elbowPoint = new Point(connectionPoint.X, anchor.Y);
                break;
            case ControllerLabelQuadrant.BottomRight:
            default:
                connectionPoint = new Point(labelX, labelY);
                elbowPoint = new Point(connectionPoint.X, anchor.Y);
                break;
        }

        return new ControllerLabelLayoutResult(
            anchor,
            labelPos,
            new[] { anchor, elbowPoint, connectionPoint },
            quadrant);
    }

    public void ResolveOverlaps(
        IList<ControllerMappingOverlayItem> items,
        Rect viewport,
        IReadOnlyList<Size> labelSizes)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(labelSizes);
        if (labelSizes.Count != items.Count)
            throw new ArgumentException("labelSizes must align with items.", nameof(labelSizes));

        if (items.Count == 0) return;

        const int maxIterations = 64;

        for (var iter = 0; iter < maxIterations; iter++)
        {
            var rects = new Rect[items.Count];
            for (var i = 0; i < items.Count; i++)
                rects[i] = GetAbsoluteLabelRect(items[i], labelSizes[i]);

            var changed = false;

            for (var i = 0; i < items.Count; i++)
            {
                for (var j = i + 1; j < items.Count; j++)
                {
                    if (!IntersectsWithGap(rects[i], rects[j], Margin))
                        continue;

                    var moved = TrySeparateAlongShallowAxis(items[j], rects[i], rects[j], Margin);
                    if (moved)
                    {
                        rects[j] = GetAbsoluteLabelRect(items[j], labelSizes[j]);
                        UpdateLeaderLine(items[j], labelSizes[j]);
                        changed = true;
                    }
                }
            }

            if (VerticalPackLowerPriority(items, labelSizes, rects))
                changed = true;

            for (var i = 0; i < items.Count; i++)
            {
                if (!ClampToViewport(items[i], labelSizes[i], viewport, Margin))
                    continue;

                UpdateLeaderLine(items[i], labelSizes[i]);
                changed = true;
            }

            if (!changed)
                break;
        }
    }

    private static bool VerticalPackLowerPriority(
        IList<ControllerMappingOverlayItem> items,
        IReadOnlyList<Size> labelSizes,
        Rect[] rects)
    {
        var order = Enumerable.Range(0, items.Count)
            .OrderBy(i => rects[i].Top)
            .ThenBy(i => rects[i].Left)
            .ToArray();

        var any = false;
        for (var oi = 1; oi < order.Length; oi++)
        {
            var j = order[oi];
            var itemJ = items[j];
            var rj = GetAbsoluteLabelRect(itemJ, labelSizes[j]);

            for (var ok = 0; ok < oi; ok++)
            {
                var i = order[ok];
                var ri = rects[i];
                if (!IntersectsWithGap(ri, rj, Margin))
                    continue;

                var delta = ri.Bottom + Margin - rj.Top;
                if (delta <= 0)
                    continue;

                itemJ.LabelY += delta;
                UpdateLeaderLine(itemJ, labelSizes[j]);
                rj = GetAbsoluteLabelRect(itemJ, labelSizes[j]);
                rects[j] = rj;
                any = true;
            }
        }

        return any;
    }

    private static Rect GetAbsoluteLabelRect(ControllerMappingOverlayItem item, Size labelSize) =>
        new(item.X + item.LabelX, item.Y + item.LabelY, labelSize.Width, labelSize.Height);

    private static bool IntersectsWithGap(Rect a, Rect b, double gap) =>
        !(a.Right + gap <= b.Left || b.Right + gap <= a.Left || a.Bottom + gap <= b.Top || b.Bottom + gap <= a.Top);

    private static bool TrySeparateAlongShallowAxis(
        ControllerMappingOverlayItem move,
        Rect fixedRect,
        Rect movingRect,
        double gap)
    {
        var overlapX = Math.Min(fixedRect.Right, movingRect.Right) - Math.Max(fixedRect.Left, movingRect.Left);
        var overlapY = Math.Min(fixedRect.Bottom, movingRect.Bottom) - Math.Max(fixedRect.Top, movingRect.Top);
        if (overlapX <= 0 || overlapY <= 0)
            return false;

        var sepX = overlapX + gap;
        var sepY = overlapY + gap;

        if (overlapX < overlapY)
        {
            var midA = fixedRect.Left + fixedRect.Width * 0.5;
            var midB = movingRect.Left + movingRect.Width * 0.5;
            move.LabelX += midB >= midA ? sepX : -sepX;
        }
        else
        {
            var midA = fixedRect.Top + fixedRect.Height * 0.5;
            var midB = movingRect.Top + movingRect.Height * 0.5;
            move.LabelY += midB >= midA ? sepY : -sepY;
        }

        return true;
    }

    private static bool ClampToViewport(ControllerMappingOverlayItem item, Size labelSize, Rect viewport, double margin)
    {
        var changed = false;
        var left = item.X + item.LabelX;
        var top = item.Y + item.LabelY;
        var w = labelSize.Width;
        var h = labelSize.Height;

        if (left < viewport.Left + margin)
        {
            item.LabelX += viewport.Left + margin - left;
            changed = true;
        }

        if (top < viewport.Top + margin)
        {
            item.LabelY += viewport.Top + margin - top;
            changed = true;
        }

        left = item.X + item.LabelX;
        top = item.Y + item.LabelY;

        if (left + w > viewport.Right - margin)
        {
            item.LabelX -= left + w - (viewport.Right - margin);
            changed = true;
        }

        if (top + h > viewport.Bottom - margin)
        {
            item.LabelY -= top + h - (viewport.Bottom - margin);
            changed = true;
        }

        return changed;
    }

    private static void UpdateLeaderLine(ControllerMappingOverlayItem item, Size labelSize)
    {
        var anchor = new Point(item.X, item.Y);
        var labelX = item.LabelX + item.X;
        var labelY = item.LabelY + item.Y;

        Point connectionPoint = item.Quadrant switch
        {
            ControllerLabelQuadrant.TopLeft => new Point(labelX + labelSize.Width, labelY + labelSize.Height),
            ControllerLabelQuadrant.TopRight => new Point(labelX, labelY + labelSize.Height),
            ControllerLabelQuadrant.BottomLeft => new Point(labelX + labelSize.Width, labelY),
            ControllerLabelQuadrant.BottomRight => new Point(labelX, labelY),
            _ => new Point(labelX, labelY)
        };

        var elbowPoint = new Point(connectionPoint.X, anchor.Y);
        var world = new[] { anchor, elbowPoint, connectionPoint };
        item.LeaderLinePoints = world
            .Select(p => new Point(p.X - anchor.X, p.Y - anchor.Y))
            .ToArray();
    }
}
