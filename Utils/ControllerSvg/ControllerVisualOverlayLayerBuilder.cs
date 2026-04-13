using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using GamepadMapperGUI.Models.ControllerVisual;

namespace Gamepad_Mapping.Utils.ControllerSvg;

public static class ControllerVisualOverlayLayerBuilder
{
    public static IReadOnlyDictionary<string, Point> ComputeOverlayAnchors(
        XElement svgRoot,
        ControllerVisualLayoutDescriptor layout)
    {
        var idIndex = ControllerVisualInteractiveLayerBuilder.BuildIdElementIndex(svgRoot);
        var positions = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);

        ControllerSvgViewport.TryReadViewportFromSvgElement(svgRoot, out var viewport);

        foreach (var region in layout.Regions)
        {
            if (!idIndex.TryGetValue(region.SvgElementId, out var el)) continue;

            if (TryGetElementCenter(el, viewport, out var center))
                positions[region.LogicalId] = center;
        }

        return positions;
    }

    private static bool TryGetElementCenter(XElement el, ControllerSvgViewport viewport, out Point center)
    {
        center = new Point();

        var transform = ControllerSvgAccumulatedTransform.GetMatrix(el);

        var local = el.Name.LocalName;
        if (local.Equals("path", StringComparison.OrdinalIgnoreCase))
        {
            var d = ControllerSvgXml.AttributeIgnoreCase(el, "d")?.Value;
            if (string.IsNullOrWhiteSpace(d)) return false;
            try
            {
                var geometry = Geometry.Parse(d);
                var bounds = geometry.Bounds;
                var rawCenter = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                var t = transform.Transform(rawCenter);
                center = new Point(t.X - viewport.X, t.Y - viewport.Y);
                return true;
            }
            catch
            {
                return false;
            }
        }

        if (local.Equals("rect", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(ControllerSvgXml.AttributeIgnoreCase(el, "x")?.Value, out var x) &&
                double.TryParse(ControllerSvgXml.AttributeIgnoreCase(el, "y")?.Value, out var y) &&
                double.TryParse(ControllerSvgXml.AttributeIgnoreCase(el, "width")?.Value, out var w) &&
                double.TryParse(ControllerSvgXml.AttributeIgnoreCase(el, "height")?.Value, out var h))
            {
                var rawCenter = new Point(x + w / 2, y + h / 2);
                var t = transform.Transform(rawCenter);
                center = new Point(t.X - viewport.X, t.Y - viewport.Y);
                return true;
            }
        }

        return false;
    }
}
