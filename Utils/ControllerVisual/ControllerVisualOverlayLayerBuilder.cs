using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using GamepadMapperGUI.Models.ControllerVisual;

namespace Gamepad_Mapping.Utils.ControllerVisual;

public static class ControllerVisualOverlayLayerBuilder
{
    public static IReadOnlyDictionary<string, Point> ComputeOverlayAnchors(
        XElement svgRoot,
        ControllerVisualLayoutDescriptor layout)
    {
        var idIndex = ControllerVisualInteractiveLayerBuilder.BuildIdElementIndex(svgRoot);
        var positions = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);

        ControllerSvgViewport.TryReadViewportFromSvgElement(svgRoot, out var viewport);
        var spineX = viewport.Width > 0 ? viewport.Width * 0.5 : 0;

        foreach (var region in layout.Regions)
        {
            if (!idIndex.TryGetValue(region.SvgElementId, out var el)) continue;

            if (!TryGetElementOverlayBounds(el, viewport, out var ob))
                continue;

            var midY = ob.Top + ob.Height * 0.5;
            var cx = ob.Left + ob.Width * 0.5;
            var isLeft = cx < spineX;
            var exact = ControllerVisualOverlayGeometryEngine.GetExactPathAnchor(el, viewport, isLeft);
            positions[region.LogicalId] = exact != default
                ? exact
                : (isLeft ? new Point(ob.Left, midY) : new Point(ob.Right, midY));
        }

        return positions;
    }

    private static bool TryGetElementOverlayBounds(XElement el, ControllerSvgViewport viewport, out Rect overlayBounds)
    {
        overlayBounds = default;
        var transform = ControllerSvgAccumulatedTransform.GetMatrix(el);

        var local = el.Name.LocalName;
        if (local.Equals("path", StringComparison.OrdinalIgnoreCase))
        {
            var d = ControllerSvgXml.AttributeIgnoreCase(el, "d")?.Value;
            if (string.IsNullOrWhiteSpace(d)) return false;
            try
            {
                var geometry = Geometry.Parse(d);
                overlayBounds = ControllerVisualOverlayGeometryEngine.TransformToViewport(transform, geometry.Bounds, viewport);
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
                var localRect = new Rect(x, y, w, h);
                overlayBounds = ControllerVisualOverlayGeometryEngine.TransformToViewport(transform, localRect, viewport);
                return true;
            }
        }

        return false;
    }
}
