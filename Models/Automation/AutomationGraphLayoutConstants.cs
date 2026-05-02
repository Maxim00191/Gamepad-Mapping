#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public static class AutomationGraphLayoutConstants
{
    public const double InsertionDownstreamPushGutterLogical = 32d;

    /// <summary>Screen-space hit tolerance for snap-to-edge during palette drag and node drag.</summary>
    public const double EdgeDropSnapToleranceScreenPixels = 28d;

    public const int EdgeBezierDistanceInitialSamples = 72;

    public const int EdgeBezierDistanceRefineSamples = 48;

    public static double DefaultDownstreamPushDeltaX(double nodeVisualWidthLogical) =>
        nodeVisualWidthLogical + InsertionDownstreamPushGutterLogical;

    public static double EdgeDropSnapToleranceSquaredLogical(double zoom)
    {
        var z = zoom < 0.01d ? 0.01d : zoom;
        var r = EdgeDropSnapToleranceScreenPixels / z;
        return r * r;
    }
}
