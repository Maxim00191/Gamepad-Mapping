using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using GamepadMapperGUI.Utils;
using WpfPath = System.Windows.Shapes.Path;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace Gamepad_Mapping.Utils.ControllerSvg;

public static class XboxControllerInteractiveLayerBuilder
{
    private static readonly string[] InteractiveIdOrder =
    [
        "shoulder_R", "trigger_R", "shoulder_L", "trigger_L",
        "btn_share", "btn_back", "btn_home", "btn_start",
        "dpad_U", "dpad_D", "dpad_L", "dpad_R",
        "btn_Y", "btn_A", "btn_X", "btn_B",
        "thumbStick_L", "thumbStick_R"
    ];

    public static void Populate(
        Canvas target,
        Style interactivePathStyle,
        Style interactiveRectangleStyle,
        MouseButtonEventHandler mouseDown,
        MouseEventHandler mouseEnter,
        MouseEventHandler mouseLeave)
    {
        target.Children.Clear();

        var svgPath = AppPaths.GetControllerSvgPath("Xbox.svg");
        if (!File.Exists(svgPath))
        {
            Debug.WriteLine($"Controller SVG not found: {svgPath}");
            return;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Load(svgPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load controller SVG: {ex.Message}");
            return;
        }

        var root = doc.Root;
        if (root is null) return;

        if (ControllerSvgViewport.TryReadViewportFromSvgElement(root, out var vp))
        {
            target.Width = vp.Width;
            target.Height = vp.Height;
        }

        foreach (var id in InteractiveIdOrder)
        {
            var el = FindById(root, id);
            if (el is null) continue;

            var local = el.Name.LocalName;
            if (local.Equals("path", StringComparison.OrdinalIgnoreCase))
            {
                if (CreatePathShape(el, id, interactivePathStyle, mouseDown, mouseEnter, mouseLeave) is { } path)
                    target.Children.Add(path);
            }
            else if (local.Equals("rect", StringComparison.OrdinalIgnoreCase))
            {
                if (CreateRectangleShape(el, id, interactiveRectangleStyle, mouseDown, mouseEnter, mouseLeave) is { } rect)
                    target.Children.Add(rect);
            }
        }
    }

    private static XElement? FindById(XElement root, string id)
    {
        foreach (var e in root.Descendants())
        {
            var a = ControllerSvgXml.AttributeIgnoreCase(e, "id");
            if (a is not null && string.Equals(a.Value, id, StringComparison.Ordinal))
                return e;
        }
        return null;
    }

    private static WpfPath? CreatePathShape(
        XElement pathEl,
        string id,
        Style style,
        MouseButtonEventHandler mouseDown,
        MouseEventHandler mouseEnter,
        MouseEventHandler mouseLeave)
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
            Debug.WriteLine($"Invalid path geometry for '{id}': {ex.Message}");
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
            Tag = id
        };

        path.MouseLeftButtonDown += mouseDown;
        path.MouseEnter += mouseEnter;
        path.MouseLeave += mouseLeave;
        return path;
    }

    private static WpfRectangle? CreateRectangleShape(
        XElement rectEl,
        string id,
        Style style,
        MouseButtonEventHandler mouseDown,
        MouseEventHandler mouseEnter,
        MouseEventHandler mouseLeave)
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
            Tag = id
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);

        rect.MouseLeftButtonDown += mouseDown;
        rect.MouseEnter += mouseEnter;
        rect.MouseLeave += mouseLeave;
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
}
