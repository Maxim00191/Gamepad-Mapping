#nullable enable

using System.Windows;

namespace Gamepad_Mapping.Utils.ControllerVisual;

public static class ControllerMappingOverlayLabelMetrics
{
    public const string FontFamilyName = "Segoe UI";

    public const double PrimaryFontSize = 10d;

    public const double SecondaryFontSize = 9d;

    public const double MaxTextBlockWidth = 150d;

    public const double MaxLabelBoxWidth = 200d;

    /// <summary>Uniform width for overlay label borders and layout (fixed column width).</summary>
    public const double OverlayLabelColumnWidth = 200d;

    public const double SecondaryTextMarginLeft = 3d;

    public const double StackedSecondaryMarginTop = 2d;

    public const double BorderPaddingLeft = 3d;

    public const double BorderPaddingTop = 1d;

    public const double BorderThickness = 1d;

    public const double EstimatedLayoutBleed = 2d;

    public static Thickness BorderPaddingThickness =>
        new(BorderPaddingLeft, BorderPaddingTop, BorderPaddingLeft, BorderPaddingTop);
}
