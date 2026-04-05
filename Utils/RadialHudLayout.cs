using System;
using System.Windows;

namespace Gamepad_Mapping.Utils;

/// <summary>
/// Geometry for radial menu HUD: item centers on a circle concentric with the disc,
/// with outer edges of square slots aligned to the disc boundary (strict layout for UI work).
/// Convention: index 0 at top (12 o'clock), increasing clockwise — matches mapping engine sector math.
/// </summary>
public static class RadialHudLayout
{
    public const double DiscDiameter = 340;
    public const double ItemSize = 80;

    public static double DiscRadius => DiscDiameter * 0.5;
    public static double ItemHalf => ItemSize * 0.5;

    /// <summary>Distance from disc center to center of each item; outer corner of item lies on the disc circle.</summary>
    public static double ItemCenterRadius => DiscRadius - ItemHalf;

    /// <summary>Center of item <paramref name="segmentIndex"/> in canvas coordinates with origin at disc center (Y down).</summary>
    public static Point ItemCenterOffset(int segmentIndex, int segmentCount)
    {
        if (segmentCount <= 0)
            return new Point(0, 0);

        var idx = ((segmentIndex % segmentCount) + segmentCount) % segmentCount;
        var angleRad = -Math.PI * 0.5 + 2 * Math.PI * idx / segmentCount;
        var r = ItemCenterRadius;
        return new Point(r * Math.Cos(angleRad), r * Math.Sin(angleRad));
    }

    /// <summary>Top-left of item slot for Canvas.Left / Canvas.Top; canvas is DiscDiameter square, (0,0) top-left of disc.</summary>
    public static Point ItemSlotTopLeft(int segmentIndex, int segmentCount)
    {
        var c = ItemCenterOffset(segmentIndex, segmentCount);
        return new Point(DiscRadius + c.X - ItemHalf, DiscRadius + c.Y - ItemHalf);
    }
}
