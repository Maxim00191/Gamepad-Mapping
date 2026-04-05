#nullable enable
using System;
using System.Windows;
using System.Windows.Media;

namespace Gamepad_Mapping.Utils;

/// <summary>
/// Geometry for radial menu HUD: item centers on a circle concentric with the disc,
/// with outer edges of square slots aligned to the disc boundary (strict layout for UI work).
/// Convention: index 0 at top (12 o'clock), increasing clockwise — matches mapping engine sector math.
/// All pixel sizes derive from <see cref="HudScale"/> and private base constants (scale 1.0 = reference design).
/// </summary>
public static class RadialHudLayout
{
    /// <summary>Single factor for overall HUD size. At 1.0, disc diameter is <c>400</c>; default <c>1.5</c> yields the previous 600px disc.</summary>
    public static double HudScale { get; set; } = 1.5;

    public const double MinHudScale = 0.75;
    public const double MaxHudScale = 2.0;

    public static double ClampHudScale(double value) =>
        Math.Clamp(value, MinHudScale, MaxHudScale);

    private const double BaseDiscDiameter = 400;
    private const double BaseItemSize = 96;
    private const double BaseInnerHoleDiameter = 200;
    private const double BaseTitlePlateDiameter = 112;

    private const double BasePrimaryItemFontSize = 12;
    private const double BaseSecondaryItemFontSize = 12;
    private const double BaseTitleFontSize = 20;

    private const double BaseRootOuterMargin = 40;
    private const double BaseTitleAreaHorizontalMargin = 40.0 / 3.0;

    private const double BaseItemBorderThickness = 4.0 / 3.0;
    private const double BaseSecondaryCaptionTopMargin = 4.0 / 3.0;

    private const double BaseDiscDropShadowBlur = 40.0 / 3.0;
    private const double BaseTitlePlateDropShadowBlur = 16.0 / 3.0;
    private const double BaseTitleTextDropShadowBlur = 2.0;
    private const double BaseSelectedItemGlowBlur = 20.0 / 3.0;

    private const double BaseHudStrokeThickness = 2.0 / 3.0;

    /// <summary>Target arc length between sectors at the outer edge (scales with <see cref="HudScale"/>).</summary>
    private const double BaseSectorGapLength = 6.0;

    /// <summary>Base fillet radius at sector tips (outer/inner arc meeting radial edges); scales with <see cref="HudScale"/>.</summary>
    private const double BaseSectorCornerRadius = 6.0;

    public static double HudStrokeThickness => BaseHudStrokeThickness * HudScale;

    /// <summary>Radial-menu sector spacing at the outer ring (arc length ≈ this value when not limited by wedge count).</summary>
    public static double SectorGapLength => BaseSectorGapLength * HudScale;

    /// <summary>Fillet radius where each sector’s outer/inner arcs meet the radial edges (clamped to wedge geometry).</summary>
    public static double SectorCornerRadius => BaseSectorCornerRadius * HudScale;

    public static double DiscDiameter => BaseDiscDiameter * HudScale;
    public static double ItemSize => BaseItemSize * HudScale;
    public static double InnerHoleDiameter => BaseInnerHoleDiameter * HudScale;
    public static double TitlePlateDiameter => BaseTitlePlateDiameter * HudScale;

    public static double PrimaryItemFontSize => BasePrimaryItemFontSize * HudScale;
    public static double SecondaryItemFontSize => BaseSecondaryItemFontSize * HudScale;
    public static double TitleFontSize => BaseTitleFontSize * HudScale;

    /// <summary>Uniform RootGrid margin (avoids x:Static to Thickness in XAML — can crash at load).</summary>
    public static double RootOuterMarginLength => BaseRootOuterMargin * HudScale;

    public static double TitleAreaHorizontalPadding => BaseTitleAreaHorizontalMargin * HudScale;

    public static double SecondaryCaptionTopSpacing => BaseSecondaryCaptionTopMargin * HudScale;

    public static double ItemBorderThickness => BaseItemBorderThickness * HudScale;

    public static double DiscDropShadowBlur => BaseDiscDropShadowBlur * HudScale;
    public static double TitlePlateDropShadowBlur => BaseTitlePlateDropShadowBlur * HudScale;
    public static double TitleTextDropShadowBlur => BaseTitleTextDropShadowBlur * HudScale;
    public static double SelectedItemGlowBlur => BaseSelectedItemGlowBlur * HudScale;

    public static double DiscRadius => DiscDiameter * 0.5;
    public static double InnerHoleRadius => InnerHoleDiameter * 0.5;
    public static double TitlePlateRadius => TitlePlateDiameter * 0.5;
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

    /// <summary>
    /// Annulus sector built only from <see cref="PathFigure"/> + <see cref="ArcSegment"/> (no <see cref="EllipseGeometry"/>).
    /// Adjacent sectors are separated by a constant angular gap so arc length at the outer edge ≈ <see cref="SectorGapLength"/>.
    /// Wedge bisectors use the same angles as <see cref="ItemCenterOffset"/> (matches stick sector math in <c>MappingEngine</c>).
    /// </summary>
    public static Geometry CreateAnnulusSectorGeometry(int segmentIndex, int segmentCount)
    {
        if (segmentCount <= 0)
            return Geometry.Empty;

        if (segmentCount == 1)
            return CreateFullAnnulusPathGeometry();

        var idx = ((segmentIndex % segmentCount) + segmentCount) % segmentCount;
        var stepRad = 2 * Math.PI / segmentCount;
        var gapRad = SectorGapLength / DiscRadius;
        if (gapRad >= stepRad)
            gapRad = stepRad * 0.45;
        var sweepRad = stepRad - gapRad;

        var centerRad = -Math.PI * 0.5 + stepRad * idx;
        var startRad = centerRad - sweepRad * 0.5;
        var endRad = centerRad + sweepRad * 0.5;

        return CreateAnnulusSectorPathGeometry(startRad, endRad);
    }

    /// <summary>Single annulus ring as one closed <see cref="PathGeometry"/> (two 180° outer arcs + two 180° inner arcs).</summary>
    private static PathGeometry CreateFullAnnulusPathGeometry()
    {
        var cx = DiscRadius;
        var cy = DiscRadius;
        var ro = DiscRadius;
        var ri = InnerHoleRadius;

        var outerTop = new Point(cx, cy - ro);
        var outerBottom = new Point(cx, cy + ro);
        var innerTop = new Point(cx, cy - ri);
        var innerBottom = new Point(cx, cy + ri);

        var fig = new PathFigure { StartPoint = outerTop, IsClosed = true };
        fig.Segments.Add(new ArcSegment(outerBottom, new Size(ro, ro), 0, true, SweepDirection.Clockwise, true));
        fig.Segments.Add(new ArcSegment(outerTop, new Size(ro, ro), 0, true, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(innerTop, true));
        fig.Segments.Add(new ArcSegment(innerBottom, new Size(ri, ri), 0, true, SweepDirection.Counterclockwise, true));
        fig.Segments.Add(new ArcSegment(innerTop, new Size(ri, ri), 0, true, SweepDirection.Counterclockwise, true));

        var pg = new PathGeometry();
        pg.Figures.Add(fig);
        pg.Freeze();
        return pg;
    }

    private static PathGeometry CreateAnnulusSectorPathGeometry(double startRad, double endRad)
    {
        var cx = DiscRadius;
        var cy = DiscRadius;
        var ro = DiscRadius;
        var ri = InnerHoleRadius;

        var sweepRad = endRad - startRad;
        var cr = Math.Min(
            SectorCornerRadius,
            Math.Min(sweepRad * ro * 0.12, (ro - ri) * 0.35));

        var trimOuter = cr / ro;
        var trimInner = cr / ri;
        var trim = Math.Min(trimOuter, Math.Min(trimInner, sweepRad * 0.225));
        var s0 = startRad + trim;
        var e0 = endRad - trim;
        if (e0 <= s0 || cr < 0.5)
            return CreateAnnulusSectorPathGeometrySharp(startRad, endRad);

        var oS = RingPoint(cx, cy, ro, s0);
        var oE = RingPoint(cx, cy, ro, e0);
        var iE = RingPoint(cx, cy, ri, e0);
        var iS = RingPoint(cx, cy, ri, s0);

        var oCornerEnd = RingPoint(cx, cy, ro, endRad);
        var iCornerEnd = RingPoint(cx, cy, ri, endRad);
        var iCornerStart = RingPoint(cx, cy, ri, startRad);
        var oCornerStart = RingPoint(cx, cy, ro, startRad);

        var b = Point.Add(oCornerEnd, RadialInVector(endRad) * cr);
        var c = Point.Add(iCornerEnd, RadialOutVector(endRad) * cr);
        var d = Point.Add(iCornerStart, RadialOutVector(startRad) * cr);
        var g = Point.Add(oCornerStart, RadialInVector(startRad) * cr);

        var outerLarge = e0 - s0 > Math.PI;
        var innerLarge = e0 - s0 > Math.PI;

        var fig = new PathFigure { StartPoint = oS, IsClosed = true };
        fig.Segments.Add(new ArcSegment(oE, new Size(ro, ro), 0, outerLarge, SweepDirection.Clockwise, true));
        fig.Segments.Add(new ArcSegment(b, new Size(cr, cr), 0, false, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(c, true));
        fig.Segments.Add(new ArcSegment(iE, new Size(cr, cr), 0, false, SweepDirection.Clockwise, true));
        fig.Segments.Add(new ArcSegment(iS, new Size(ri, ri), 0, innerLarge, SweepDirection.Counterclockwise, true));
        fig.Segments.Add(new ArcSegment(d, new Size(cr, cr), 0, false, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(g, true));
        fig.Segments.Add(new ArcSegment(oS, new Size(cr, cr), 0, false, SweepDirection.Clockwise, true));

        var pg = new PathGeometry();
        pg.Figures.Add(fig);
        pg.Freeze();
        return pg;
    }

    private static Point RingPoint(double cx, double cy, double radius, double angleRad) =>
        new(cx + radius * Math.Cos(angleRad), cy + radius * Math.Sin(angleRad));

    private static Vector RadialOutVector(double angleRad) => new(Math.Cos(angleRad), Math.Sin(angleRad));

    private static Vector RadialInVector(double angleRad) => new(-Math.Cos(angleRad), -Math.Sin(angleRad));

    private static PathGeometry CreateAnnulusSectorPathGeometrySharp(double startRad, double endRad)
    {
        var cx = DiscRadius;
        var cy = DiscRadius;
        var ro = DiscRadius;
        var ri = InnerHoleRadius;

        var outerStart = new Point(cx + ro * Math.Cos(startRad), cy + ro * Math.Sin(startRad));
        var outerEnd = new Point(cx + ro * Math.Cos(endRad), cy + ro * Math.Sin(endRad));
        var innerEnd = new Point(cx + ri * Math.Cos(endRad), cy + ri * Math.Sin(endRad));
        var innerStart = new Point(cx + ri * Math.Cos(startRad), cy + ri * Math.Sin(startRad));

        var sweepRad = endRad - startRad;
        var largeArc = sweepRad > Math.PI;
        var fig = new PathFigure { StartPoint = outerStart, IsClosed = true };
        fig.Segments.Add(new ArcSegment(outerEnd, new Size(ro, ro), 0, largeArc, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(innerEnd, true));
        fig.Segments.Add(new ArcSegment(innerStart, new Size(ri, ri), 0, largeArc, SweepDirection.Counterclockwise, true));

        var pg = new PathGeometry();
        pg.Figures.Add(fig);
        pg.Freeze();
        return pg;
    }
}
