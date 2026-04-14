#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Gamepad_Mapping.Models.Core.Visual;
using Gamepad_Mapping.Utils.ControllerVisual;

namespace Gamepad_Mapping.Services;

public class ControllerVisualLayoutHelper : IControllerVisualLayoutHelper
{
    private const double Margin = 12d;
    private const double MinVerticalGap = 16d;
    private const int MaxClampIterations = 48;

    private const double TagSafeGapFromBody = 50d;

    private const double BandAnchorBlend = 0.62;

    private const double LayoutEpsilon = 0.01;

    private const int WingResolveIterations = 80;

    public void ArrangeOverlayItems(
        IList<ControllerMappingOverlayItem> items,
        IReadOnlyList<Size> labelSizes,
        Rect viewport)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(labelSizes);
        if (labelSizes.Count != items.Count)
            throw new ArgumentException("labelSizes must align with items.", nameof(labelSizes));

        if (items.Count == 0) return;

        var spineX = viewport.X + viewport.Width * 0.5;
        var centerY = viewport.Y + viewport.Height * 0.5;
        var body = EstimateBodyBounds(viewport);

        var left = new List<int>();
        var right = new List<int>();
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].X < spineX) left.Add(i);
            else right.Add(i);
        }

        left.Sort(CompareForLayoutOrder(items));
        right.Sort(CompareForLayoutOrder(items));

        PlaceWing(items, labelSizes, viewport, centerY, body, left, isLeftWing: true);
        PlaceWing(items, labelSizes, viewport, centerY, body, right, isLeftWing: false);

        for (var iter = 0; iter < MaxClampIterations; iter++)
        {
            var changed = false;
            for (var i = 0; i < items.Count; i++)
            {
                if (ClampToViewport(items[i], labelSizes[i], viewport, Margin))
                {
                    UpdateLeaderLine(items[i], labelSizes[i], body.Left, body.Left + body.Width, items[i].LeaderLaneIndex);
                    changed = true;
                }
            }

            if (!changed) break;
        }
    }

    private static Comparison<int> CompareForLayoutOrder(IList<ControllerMappingOverlayItem> items) =>
        (a, b) =>
        {
            var ba = ControllerOverlayLabelBandClassifier.GetBand(items[a].ElementId);
            var bb = ControllerOverlayLabelBandClassifier.GetBand(items[b].ElementId);
            var c = ba.CompareTo(bb);
            if (c != 0) return c;
            return items[a].Y.CompareTo(items[b].Y);
        };

    public ControllerLabelLayoutResult CalculateLayout(
        string elementId,
        Point anchor,
        Size labelSize,
        Rect viewport)
    {
        var item = new ControllerMappingOverlayItem
        {
            ElementId = elementId,
            X = anchor.X,
            Y = anchor.Y
        };
        var list = new List<ControllerMappingOverlayItem> { item };
        var sizes = new[] { labelSize };
        ArrangeOverlayItems(list, sizes, viewport);

        var body = EstimateBodyBounds(viewport);
        var labelPos = new Point(anchor.X + item.LabelX, anchor.Y + item.LabelY);
        var labelWorld = new Rect(labelPos, labelSize);
        var geom = ControllerMappingOverlayLeaderGeometry.BuildAnchorRelativeBezierLeader(
            anchor, labelWorld, item.Quadrant, body.Left, body.Left + body.Width, item.LeaderLaneIndex);
        return new ControllerLabelLayoutResult(anchor, labelPos, geom, item.Quadrant);
    }

    private static Rect EstimateBodyBounds(Rect viewport)
    {
        var spine = viewport.X + viewport.Width * 0.5;
        var halfReach = Math.Min(spine - viewport.Left, viewport.Right - spine);
        var bodyHalf = halfReach * 0.39;
        return new Rect(spine - bodyHalf, viewport.Y, bodyHalf * 2, viewport.Height);
    }

    private static void PlaceWing(
        IList<ControllerMappingOverlayItem> items,
        IReadOnlyList<Size> labelSizes,
        Rect viewport,
        double centerY,
        Rect body,
        List<int> indices,
        bool isLeftWing)
    {
        if (indices.Count == 0) return;

        var margin = Margin;
        var availableHeight = Math.Max(0, viewport.Height - 2d * margin);
        var n = indices.Count;
        var heights = indices.Select(i => labelSizes[i].Height).ToArray();

        var tops = new double[n];

        for (var k = 0; k < n; k++)
        {
            var idx = indices[k];
            var band = ControllerOverlayLabelBandClassifier.GetBand(items[idx].ElementId);
            var targetCenter = ControllerOverlayLabelBandClassifier.GetBandTargetCenterY(band, viewport, margin);
            var blendedCenter = items[idx].Y * (1d - BandAnchorBlend) + targetCenter * BandAnchorBlend;
            tops[k] = blendedCenter - heights[k] * 0.5;
        }

        for (var iter = 0; iter < WingResolveIterations; iter++)
        {
            var changed = false;
            for (var k = 1; k < n; k++)
            {
                var prev = indices[k - 1];
                var hPrev = labelSizes[prev].Height;
                var minTop = tops[k - 1] + hPrev + MinVerticalGap;
                if (tops[k] < minTop - LayoutEpsilon)
                {
                    tops[k] = minTop;
                    changed = true;
                }
            }

            var last = indices[n - 1];
            var lastH = labelSizes[last].Height;
            var bottomLimit = viewport.Bottom - margin;
            if (tops[n - 1] + lastH > bottomLimit + LayoutEpsilon)
            {
                var shift = tops[n - 1] + lastH - bottomLimit;
                for (var k = 0; k < n; k++)
                    tops[k] -= shift;
                changed = true;
            }

            var topLimit = viewport.Top + margin;
            if (tops[0] < topLimit - LayoutEpsilon)
            {
                var shift = topLimit - tops[0];
                for (var k = 0; k < n; k++)
                    tops[k] += shift;
                changed = true;
            }

            if (!changed) break;
        }

        var span = tops[n - 1] + heights[n - 1] - tops[0];
        var slack = availableHeight - span;
        if (slack > 0.5 && n > 0)
        {
            var shift = slack * 0.5;
            for (var k = 0; k < n; k++)
                tops[k] += shift;

            if (tops[n - 1] + heights[n - 1] > viewport.Bottom - margin)
            {
                var overflow = tops[n - 1] + heights[n - 1] - (viewport.Bottom - margin);
                for (var k = 0; k < n; k++)
                    tops[k] -= overflow;
            }

            if (tops[0] < viewport.Top + margin)
            {
                var under = viewport.Top + margin - tops[0];
                for (var k = 0; k < n; k++)
                    tops[k] += under;
            }
        }

        var bodyLeft = body.Left;
        var bodyRight = body.Left + body.Width;

        var leftColumnRightEdge = body.Left - TagSafeGapFromBody;
        var rightColumnLeftEdge = body.Right + TagSafeGapFromBody;

        for (var k = 0; k < n; k++)
        {
            var i = indices[k];
            var item = items[i];
            var w = labelSizes[i].Width;

            double labelAbsX;
            if (isLeftWing)
            {
                labelAbsX = leftColumnRightEdge - w;
                if (labelAbsX < viewport.Left + margin)
                    labelAbsX = viewport.Left + margin;
            }
            else
            {
                labelAbsX = rightColumnLeftEdge;
                if (labelAbsX + w > viewport.Right - margin)
                    labelAbsX = viewport.Right - margin - w;
            }

            item.LabelX = labelAbsX - item.X;
            item.LabelY = tops[k] - item.Y;
            item.IsLeftColumn = isLeftWing;
            item.LeaderLaneIndex = k;

            item.Quadrant = isLeftWing
                ? (item.Y < centerY ? ControllerLabelQuadrant.TopLeft : ControllerLabelQuadrant.BottomLeft)
                : (item.Y < centerY ? ControllerLabelQuadrant.TopRight : ControllerLabelQuadrant.BottomRight);

            UpdateLeaderLine(item, labelSizes[i], bodyLeft, bodyRight, k);
        }
    }

    private static void UpdateLeaderLine(ControllerMappingOverlayItem item, Size labelSize, double bodyLeft, double bodyRight, int laneIndex)
    {
        var anchor = new Point(item.X, item.Y);
        var labelWorld = new Rect(item.X + item.LabelX, item.Y + item.LabelY, labelSize.Width, labelSize.Height);
        item.LeaderLineGeometry = ControllerMappingOverlayLeaderGeometry.BuildAnchorRelativeBezierLeader(
            anchor, labelWorld, item.Quadrant, bodyLeft, bodyRight, laneIndex);
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
}
