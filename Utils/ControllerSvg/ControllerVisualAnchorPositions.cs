using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Gamepad_Mapping.Utils.ControllerSvg;

public static class ControllerVisualAnchorPositions
{
    public static IReadOnlyDictionary<string, Point> FromInteractionLayer(Canvas layer)
    {
        var dict = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in layer.Children)
        {
            if (child is not FrameworkElement fe || fe.Tag is not string id)
                continue;
            if (TryGetCenterInCanvas(fe, layer, out var center))
                dict[id] = center;
        }

        return dict;
    }

    public static bool TryGetCenterInCanvas(FrameworkElement element, Canvas canvas, out Point center)
    {
        center = default;
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

            if (localBounds.Width <= 0 || localBounds.Height <= 0)
            {
                var corner = localBounds.TopLeft;
                var t0 = element.TransformToAncestor(canvas);
                center = t0.Transform(corner);
                return true;
            }

            var t = element.TransformToAncestor(canvas);
            var transformed = t.TransformBounds(localBounds);
            center = new Point(
                transformed.Left + transformed.Width * 0.5,
                transformed.Top + transformed.Height * 0.5);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
