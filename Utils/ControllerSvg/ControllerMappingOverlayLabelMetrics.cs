#nullable enable

using System.Windows;

namespace Gamepad_Mapping.Utils.ControllerSvg;

public static class ControllerMappingOverlayLabelMetrics
{
    public const string FontFamilyName = "Segoe UI";

    public const double PrimaryFontSize = 10d;

    public const double SecondaryFontSize = 9d;

    public const double MaxTextBlockWidth = 120d;

    public const double SecondaryTextMarginLeft = 3d;

    public const double BorderPaddingLeft = 3d;

    public const double BorderPaddingTop = 1d;

    public const double BorderThickness = 1d;

    public static Thickness BorderPaddingThickness =>
        new(BorderPaddingLeft, BorderPaddingTop, BorderPaddingLeft, BorderPaddingTop);
}
