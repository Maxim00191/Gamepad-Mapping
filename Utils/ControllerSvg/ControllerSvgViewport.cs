using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace Gamepad_Mapping.Utils.ControllerSvg;

public readonly record struct ControllerSvgViewport(double Width, double Height)
{
    public static bool TryReadSvgRoot(string svgPath, out ControllerSvgViewport viewport) =>
        TryReadSvgRoot(svgPath, out _, out viewport);

    public static bool TryReadSvgRoot(
        string svgPath,
        [NotNullWhen(true)] out XElement? svgRoot,
        out ControllerSvgViewport viewport)
    {
        svgRoot = null;
        viewport = default;
        if (!File.Exists(svgPath)) return false;

        XDocument doc;
        try
        {
            doc = XDocument.Load(svgPath);
        }
        catch
        {
            return false;
        }

        var root = doc.Root;
        if (root is null || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!TryReadViewportFromSvgElement(root, out viewport))
            return false;

        svgRoot = root;
        return true;
    }

    public static bool TryReadViewportFromSvgElement(XElement svgRoot, out ControllerSvgViewport viewport)
    {
        viewport = default;

        var viewBox = ControllerSvgXml.AttributeIgnoreCase(svgRoot, "viewBox")?.Value;
        if (!string.IsNullOrWhiteSpace(viewBox))
        {
            var parts = viewBox.Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4 &&
                TryParseDouble(parts[2], out var w) && TryParseDouble(parts[3], out var h) &&
                w > 0 && h > 0)
            {
                viewport = new ControllerSvgViewport(w, h);
                return true;
            }
        }

        var wAttr = ControllerSvgXml.AttributeIgnoreCase(svgRoot, "width")?.Value;
        var hAttr = ControllerSvgXml.AttributeIgnoreCase(svgRoot, "height")?.Value;
        if (TryParseLength(wAttr, out var w2) && TryParseLength(hAttr, out var h2) && w2 > 0 && h2 > 0)
        {
            viewport = new ControllerSvgViewport(w2, h2);
            return true;
        }

        return false;
    }

    private static bool TryParseDouble(string s, out double value) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static bool TryParseLength(string? s, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        if (s.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            s = s[..^2].Trim();
        return TryParseDouble(s, out value);
    }
}
