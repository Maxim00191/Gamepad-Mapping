using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace Gamepad_Mapping.Utils.ControllerVisual;

public static class ControllerSvgAccumulatedTransform
{
    public static Matrix GetMatrix(XElement element)
    {
        var matrix = Matrix.Identity;
        var chain = new List<XElement>();
        for (var n = element; n != null; n = n.Parent)
            chain.Add(n);
        chain.Reverse();
        foreach (var node in chain)
            AppendTransformAttribute(node, ref matrix);
        return matrix;
    }

    internal static void AppendTransformAttribute(XElement element, ref Matrix matrix)
    {
        var transformAttr = ControllerSvgXml.AttributeIgnoreCase(element, "transform")?.Value;
        if (string.IsNullOrWhiteSpace(transformAttr))
            return;

        if (transformAttr.StartsWith("matrix(", StringComparison.OrdinalIgnoreCase))
        {
            var parts = transformAttr.Substring(7, transformAttr.Length - 8)
                .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 6 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var m11) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var m12) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var m21) &&
                double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var m22) &&
                double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetX) &&
                double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetY))
            {
                matrix.Append(new Matrix(m11, m12, m21, m22, offsetX, offsetY));
            }
        }
        else if (transformAttr.StartsWith("translate(", StringComparison.OrdinalIgnoreCase))
        {
            var parts = transformAttr.Substring(10, transformAttr.Length - 11)
                .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var tx))
            {
                var ty = 0d;
                if (parts.Length >= 2)
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out ty);
                matrix.Translate(tx, ty);
            }
        }
    }
}
