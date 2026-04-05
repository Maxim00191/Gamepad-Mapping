#nullable enable
using System;
using System.Windows;

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

    private const double BaseDiscDiameter = 400;
    private const double BaseItemSize = 96;
    private const double BaseInnerHoleDiameter = 200;
    private const double BaseTitlePlateDiameter = 112;

    private const double BasePrimaryItemFontSize = 15;
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

    public static double HudStrokeThickness => BaseHudStrokeThickness * HudScale;

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
}
