using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace Gamepad_Mapping.Utils.ControllerVisual;

/// <summary>
/// Centralized engine for SVG-to-Overlay coordinate transformations and geometry calculations.
/// </summary>
public static class ControllerVisualOverlayGeometryEngine
{
    private const double EdgeComparisonEpsilon = 0.1;

    public static Rect TransformToViewport(Matrix matrix, Rect localRect, ControllerSvgViewport viewport)
    {
        var tb = TransformRect(matrix, localRect);
        return new Rect(tb.X - viewport.X, tb.Y - viewport.Y, tb.Width, tb.Height);
    }

    public static Rect TransformRect(Matrix matrix, Rect rect)
    {
        var p0 = matrix.Transform(rect.TopLeft);
        var p1 = matrix.Transform(rect.TopRight);
        var p2 = matrix.Transform(rect.BottomRight);
        var p3 = matrix.Transform(rect.BottomLeft);
        
        var minX = Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X));
        var maxX = Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X));
        var minY = Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y));
        var maxY = Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y));
        
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public static Point GetExactPathAnchor(XElement el, ControllerSvgViewport viewport, bool isLeftWing)
    {
        var d = ControllerSvgXml.AttributeIgnoreCase(el, "d")?.Value;
        if (string.IsNullOrWhiteSpace(d)) return default;

        try
        {
            var geometry = Geometry.Parse(d);
            var transform = ControllerSvgAccumulatedTransform.GetMatrix(el);
            var flattened = geometry.GetFlattenedPathGeometry();

            var bestX = isLeftWing ? double.MaxValue : double.MinValue;
            var bestPoint = default(Point);
            var found = false;

            bool IsBetter(double viewportX) =>
                isLeftWing
                    ? viewportX < bestX - EdgeComparisonEpsilon
                    : viewportX > bestX + EdgeComparisonEpsilon;

            void Consider(Point p)
            {
                var tp = transform.Transform(p);
                var vp = new Point(tp.X - viewport.X, tp.Y - viewport.Y);
                if (!IsBetter(vp.X)) return;
                bestX = vp.X;
                bestPoint = vp;
                found = true;
            }

            foreach (var figure in flattened.Figures)
            {
                Consider(figure.StartPoint);
                foreach (var segment in figure.Segments)
                {
                    if (segment is PolyLineSegment poly)
                    {
                        foreach (var p in poly.Points) Consider(p);
                    }
                    else if (segment is LineSegment line)
                    {
                        Consider(line.Point);
                    }
                }
            }

            return found ? bestPoint : default;
        }
        catch
        {
            return default;
        }
    }
}
