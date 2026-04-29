#nullable enable

using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public static class AutomationDirectScreenCapture
{
    public static bool TryDirectCapture(
        IAutomationScreenCaptureServiceResolver captureResolver,
        JsonObject? properties,
        out AutomationVirtualScreenCaptureResult result,
        IProcessTargetService? processTargetService = null)
    {
        result = default;
        if (properties is null)
            return false;

        var cacheRef = AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureCacheRefNodeId);
        if (Guid.TryParse(cacheRef, out var guid) && guid != Guid.Empty)
            return false;

        var capture = captureResolver.ResolveForNodeProperties(properties);

        var mode = AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureMode);
        if (string.IsNullOrWhiteSpace(mode))
            mode = AutomationCaptureMode.Full;

        var sourceMode = AutomationCaptureSourceMode.Normalize(
            AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureSourceMode));

        if (string.Equals(mode, AutomationCaptureMode.Roi, StringComparison.OrdinalIgnoreCase))
        {
            if (!AutomationNodePropertyReader.TryReadRoiCapture(properties, out var roi) || roi.IsEmpty)
                return false;
            try
            {
                if (AutomationCaptureSourceMode.IsInProcessWindow(sourceMode))
                {
                    var target = ReadProcessTarget(properties, processTargetService);
                    return TryCaptureProcessWindowRoi(capture, target, roi, out result);
                }

                var bitmap = capture.CaptureRectanglePhysical(roi.X, roi.Y, roi.Width, roi.Height);
                var metrics = new AutomationVirtualScreenMetrics(roi.X, roi.Y, bitmap.PixelWidth, bitmap.PixelHeight);
                result = new AutomationVirtualScreenCaptureResult(bitmap, metrics);
                return true;
            }
            catch
            {
                return false;
            }
        }

        try
        {
            if (AutomationCaptureSourceMode.IsInProcessWindow(sourceMode))
            {
                var target = ReadProcessTarget(properties, processTargetService);
                result = capture.CaptureProcessWindowPhysical(target);
                return true;
            }

            result = capture.CaptureVirtualScreenPhysical();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCaptureProcessWindowRoi(
        IAutomationScreenCaptureService capture,
        AutomationProcessWindowTarget target,
        AutomationPhysicalRect roi,
        out AutomationVirtualScreenCaptureResult result)
    {
        result = default;
        var windowCapture = capture.CaptureProcessWindowPhysical(target);
        var localX = roi.X - windowCapture.Metrics.PhysicalOriginX;
        var localY = roi.Y - windowCapture.Metrics.PhysicalOriginY;
        if (localX < 0 ||
            localY < 0 ||
            localX + roi.Width > windowCapture.Bitmap.PixelWidth ||
            localY + roi.Height > windowCapture.Bitmap.PixelHeight)
        {
            return false;
        }

        var crop = new CroppedBitmap(windowCapture.Bitmap, new Int32Rect(localX, localY, roi.Width, roi.Height));
        if (crop.CanFreeze)
            crop.Freeze();

        var metrics = new AutomationVirtualScreenMetrics(roi.X, roi.Y, crop.PixelWidth, crop.PixelHeight);
        result = new AutomationVirtualScreenCaptureResult(crop, metrics, windowCapture.ProcessTarget);
        return true;
    }

    private static AutomationProcessWindowTarget ReadProcessTarget(
        JsonObject properties,
        IProcessTargetService? processTargetService)
    {
        var processName = AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureProcessName);
        var processId = AutomationNodePropertyReader.ReadInt(properties, AutomationNodePropertyKeys.CaptureProcessId, 0);
        return AutomationProcessTargetResolution.ResolveLiveTarget(processTargetService, processName, processId);
    }
}
