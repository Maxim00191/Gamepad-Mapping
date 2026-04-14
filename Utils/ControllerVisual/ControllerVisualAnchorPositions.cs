using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Gamepad_Mapping.Utils.ControllerVisual;

public static class ControllerVisualAnchorPositions
{
    public static IReadOnlyDictionary<string, Point> FromInteractionLayer(Canvas layer)
    {
        var spineX = layer.ActualWidth > 0 ? layer.ActualWidth * 0.5 : layer.Width * 0.5;
        var dict = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in layer.Children)
        {
            if (child is not FrameworkElement fe || fe.Tag is not string id)
                continue;
            if (TryGetOutwardAnchorInCanvas(fe, layer, spineX, out var anchor))
                dict[id] = anchor;
        }

        return dict;
    }

    public static bool TryGetOutwardAnchorInCanvas(FrameworkElement element, Canvas canvas, double spineX, out Point anchor)
    {
        anchor = default;
        if (!TryGetBoundsInCanvas(element, canvas, out var b))
            return false;

        var midY = b.Top + b.Height * 0.5;
        var cx = b.Left + b.Width * 0.5;

        if (element is Path path && path.Data is not null)
        {
            try
            {
                var geometry = path.Data;
                var transform = element.TransformToAncestor(canvas);
                var flattened = geometry.GetFlattenedPathGeometry();
                var bestX = cx < spineX ? double.MaxValue : double.MinValue;
                var bestPoint = new Point(cx, midY);
                bool found = false;

                foreach (var figure in flattened.Figures)
                {
                    var startP = transform.Transform(figure.StartPoint);
                    UpdateBestPoint(startP, cx < spineX, ref bestX, ref bestPoint, ref found);

                    foreach (var segment in figure.Segments)
                    {
                        if (segment is PolyLineSegment poly)
                        {
                            foreach (var p in poly.Points)
                            {
                                var worldP = transform.Transform(p);
                                UpdateBestPoint(worldP, cx < spineX, ref bestX, ref bestPoint, ref found);
                            }
                        }
                        else if (segment is LineSegment line)
                        {
                            var worldP = transform.Transform(line.Point);
                            UpdateBestPoint(worldP, cx < spineX, ref bestX, ref bestPoint, ref found);
                        }
                    }
                }
                
                if (found)
                {
                    anchor = bestPoint;
                    return true;
                }
            }
            catch
            {
            }
        }

        anchor = cx < spineX
            ? new Point(b.Left, midY)
            : new Point(b.Right, midY);
        return true;
    }

    private static void UpdateBestPoint(Point p, bool isLeftLabel, ref double bestX, ref Point bestPoint, ref bool found)
    {
        if (isLeftLabel)
        {
            if (p.X >= bestX) return;
        }
        else if (p.X <= bestX)
        {
            return;
        }

        bestX = p.X;
        bestPoint = p;
        found = true;
    }

    public static bool TryGetCenterInCanvas(FrameworkElement element, Canvas canvas, out Point center)
    {
        if (!TryGetBoundsInCanvas(element, canvas, out var b))
        {
            center = default;
            return false;
        }

        center = new Point(b.Left + b.Width * 0.5, b.Top + b.Height * 0.5);
        return true;
    }

    private static bool TryGetBoundsInCanvas(FrameworkElement element, Canvas canvas, out Rect bounds)
    {
        bounds = default;
        try
        {
            Rect localBounds;
            switch (element)
            {
                case Path path when path.Data is not null:
                    localBounds = path.Data.Bounds;
                    break;
                case Rectangle rect:
                    localBounds = new Rect(0, 0, rect.Width, rect.Height);
                    break;
                default:
                    return false;
            }

            var t = element.TransformToAncestor(canvas);
            if (localBounds.Width <= 0 || localBounds.Height <= 0)
            {
                var corner = localBounds.TopLeft;
                var p = t.Transform(corner);
                bounds = new Rect(p.X, p.Y, 0, 0);
                return true;
            }

            bounds = t.TransformBounds(localBounds);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
