using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

internal static class AutomationOverlayCoordinateMapping
{
    internal static (double Left, double Top, double Width, double Height) NormalizeVirtualDesktopLogicalBounds(
        double virtualScreenLeft,
        double virtualScreenTop,
        double virtualScreenWidth,
        double virtualScreenHeight)
    {
        return (
            virtualScreenLeft,
            virtualScreenTop,
            Math.Max(1.0, virtualScreenWidth),
            Math.Max(1.0, virtualScreenHeight));
    }

    public static void PhysicalRectToOverlay(
        AutomationPhysicalRect rect,
        AutomationVirtualScreenMetrics vs,
        double overlayWidth,
        double overlayHeight,
        out double x,
        out double y,
        out double w,
        out double h)
    {
        if (vs.WidthPx <= 0 || vs.HeightPx <= 0 || overlayWidth <= 0 || overlayHeight <= 0 || rect.IsEmpty)
        {
            x = y = w = h = 0;
            return;
        }

        var sx = overlayWidth / vs.WidthPx;
        var sy = overlayHeight / vs.HeightPx;
        x = (rect.X - vs.PhysicalOriginX) * sx;
        y = (rect.Y - vs.PhysicalOriginY) * sy;
        w = rect.Width * sx;
        h = rect.Height * sy;
    }

    public static void PhysicalRectToOverlayForClientSurface(
        AutomationPhysicalRect rect,
        int clientOriginScreenX,
        int clientOriginScreenY,
        int clientPhysicalWidthPx,
        int clientPhysicalHeightPx,
        double overlayWidth,
        double overlayHeight,
        out double x,
        out double y,
        out double w,
        out double h)
    {
        if (clientPhysicalWidthPx <= 0 || clientPhysicalHeightPx <= 0 || overlayWidth <= 0 || overlayHeight <= 0 ||
            rect.IsEmpty)
        {
            x = y = w = h = 0;
            return;
        }

        var sx = overlayWidth / clientPhysicalWidthPx;
        var sy = overlayHeight / clientPhysicalHeightPx;
        x = (rect.X - clientOriginScreenX) * sx;
        y = (rect.Y - clientOriginScreenY) * sy;
        w = rect.Width * sx;
        h = rect.Height * sy;
    }
}
