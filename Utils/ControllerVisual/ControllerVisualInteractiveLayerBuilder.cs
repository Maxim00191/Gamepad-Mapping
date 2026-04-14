using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using GamepadMapperGUI.Models.ControllerVisual;
using WpfPath = System.Windows.Shapes.Path;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace Gamepad_Mapping.Utils.ControllerVisual;

public static class ControllerVisualInteractiveLayerBuilder
{
    public static void Populate(
        Canvas target,
        XElement svgRoot,
        ControllerVisualLayoutDescriptor layout,
        Style interactivePathStyle,
        Style interactiveRectangleStyle,
        MouseButtonEventHandler mouseDown,
        MouseEventHandler mouseEnter,
        MouseEventHandler mouseLeave,
        Func<string, string>? getAccessibleNameForLogicalId = null)
    {
        target.Children.Clear();

        if (ControllerSvgViewport.TryReadViewportFromSvgElement(svgRoot, out var vp))
        {
            target.Width = vp.Width;
            target.Height = vp.Height;
        }

        var idIndex = BuildIdElementIndex(svgRoot);

        foreach (var region in layout.Regions)
        {
            if (!idIndex.TryGetValue(region.SvgElementId, out var el))
            {
                Debug.WriteLine(
                    $"Controller visual: no SVG element with id '{region.SvgElementId}' for logical '{region.LogicalId}'.");
                continue;
            }

            var local = el.Name.LocalName;
            var kind = region.ElementKind;
            if (kind == ControllerVisualElementKind.Auto)
            {
                if (local.Equals("path", StringComparison.OrdinalIgnoreCase))
                    kind = ControllerVisualElementKind.Path;
                else if (local.Equals("rect", StringComparison.OrdinalIgnoreCase))
                    kind = ControllerVisualElementKind.Rect;
            }

            switch (kind)
            {
                case ControllerVisualElementKind.Path when local.Equals("path", StringComparison.OrdinalIgnoreCase):
                    if (CreatePathShape(el, region.LogicalId, interactivePathStyle, mouseDown, mouseEnter, mouseLeave, getAccessibleNameForLogicalId) is { } path)
                        target.Children.Add(path);
                    break;
                case ControllerVisualElementKind.Rect when local.Equals("rect", StringComparison.OrdinalIgnoreCase):
                    if (CreateRectangleShape(el, region.LogicalId, interactiveRectangleStyle, mouseDown, mouseEnter, mouseLeave, getAccessibleNameForLogicalId) is { } rect)
                        target.Children.Add(rect);
                    break;
                case ControllerVisualElementKind.Auto:
                    Debug.WriteLine(
                        $"Controller visual: unsupported or ambiguous element '{region.SvgElementId}' (localName={local}) for logical '{region.LogicalId}'.");
                    break;
                default:
                    Debug.WriteLine(
                        $"Controller visual: element kind mismatch for '{region.SvgElementId}' (expected {kind}, got {local}) for logical '{region.LogicalId}'.");
                    break;
            }
        }
    }

    public static Dictionary<string, XElement> BuildIdElementIndex(XElement root)
    {
        var d = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in root.Descendants())
        {
            var a = ControllerSvgXml.AttributeIgnoreCase(e, "id");
            if (a is null || string.IsNullOrWhiteSpace(a.Value))
                continue;
            d.TryAdd(a.Value, e);
        }

        return d;
    }

    private static WpfPath? CreatePathShape(
        XElement pathEl,
        string logicalId,
        Style style,
        MouseButtonEventHandler mouseDown,
        MouseEventHandler mouseEnter,
        MouseEventHandler mouseLeave,
        Func<string, string>? getAccessibleNameForLogicalId)
    {
        var d = ControllerSvgXml.AttributeIgnoreCase(pathEl, "d")?.Value;
        if (string.IsNullOrWhiteSpace(d)) return null;

        Geometry geometry;
        try
        {
            geometry = Geometry.Parse(d);
        }
        catch (FormatException ex)
        {
            Debug.WriteLine($"Invalid path geometry for '{logicalId}': {ex.Message}");
            return null;
        }

        var fillRuleAttr = ControllerSvgXml.AttributeIgnoreCase(pathEl, "fill-rule")?.Value;
        if (string.Equals(fillRuleAttr, "evenodd", StringComparison.OrdinalIgnoreCase) && geometry is PathGeometry pathGeometry)
            pathGeometry.FillRule = FillRule.EvenOdd;

        geometry.Freeze();

        var path = new WpfPath
        {
            Style = style,
            Data = geometry,
            Tag = logicalId,
            RenderTransform = FreezeMatrixTransform(ControllerSvgAccumulatedTransform.GetMatrix(pathEl))
        };

        path.MouseLeftButtonDown += mouseDown;
        path.MouseEnter += mouseEnter;
        path.MouseLeave += mouseLeave;
        var name = getAccessibleNameForLogicalId?.Invoke(logicalId) ?? logicalId;
        AutomationProperties.SetName(path, name);
        return path;
    }

    private static WpfRectangle? CreateRectangleShape(
        XElement rectEl,
        string logicalId,
        Style style,
        MouseButtonEventHandler mouseDown,
        MouseEventHandler mouseEnter,
        MouseEventHandler mouseLeave,
        Func<string, string>? getAccessibleNameForLogicalId)
    {
        if (!TryParseDouble(ControllerSvgXml.AttributeIgnoreCase(rectEl, "x")?.Value, out var x)) return null;
        if (!TryParseDouble(ControllerSvgXml.AttributeIgnoreCase(rectEl, "y")?.Value, out var y)) return null;
        if (!TryParseDouble(ControllerSvgXml.AttributeIgnoreCase(rectEl, "width")?.Value, out var w)) return null;
        if (!TryParseDouble(ControllerSvgXml.AttributeIgnoreCase(rectEl, "height")?.Value, out var h)) return null;

        TryParseDouble(ControllerSvgXml.AttributeIgnoreCase(rectEl, "rx")?.Value, out var rx);
        TryParseDouble(ControllerSvgXml.AttributeIgnoreCase(rectEl, "ry")?.Value, out var ry);

        var rect = new WpfRectangle
        {
            Style = style,
            Width = w,
            Height = h,
            RadiusX = rx,
            RadiusY = ry > 0 ? ry : rx,
            Tag = logicalId,
            RenderTransform = FreezeMatrixTransform(ControllerSvgAccumulatedTransform.GetMatrix(rectEl))
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);

        rect.MouseLeftButtonDown += mouseDown;
        rect.MouseEnter += mouseEnter;
        rect.MouseLeave += mouseLeave;
        var name = getAccessibleNameForLogicalId?.Invoke(logicalId) ?? logicalId;
        AutomationProperties.SetName(rect, name);
        return rect;
    }

    private static bool TryParseDouble(string? s, out double value)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            value = 0;
            return false;
        }
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static MatrixTransform FreezeMatrixTransform(Matrix matrix)
    {
        var t = new MatrixTransform(matrix);
        t.Freeze();
        return t;
    }
}
