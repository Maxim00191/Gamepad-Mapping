#nullable enable

using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public static class AutomationDirectScreenCapture
{
    public static bool TryDirectCapture(
        IAutomationScreenCaptureServiceResolver captureResolver,
        JsonObject? properties,
        out AutomationVirtualScreenCaptureResult result)
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

        if (string.Equals(mode, AutomationCaptureMode.Roi, StringComparison.OrdinalIgnoreCase))
        {
            if (!AutomationNodePropertyReader.TryReadRoiCapture(properties, out var roi) || roi.IsEmpty)
                return false;
            try
            {
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

        var sourceMode = AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureSourceMode);
        if (string.IsNullOrWhiteSpace(sourceMode))
            sourceMode = AutomationCaptureSourceMode.Screen;

        try
        {
            if (string.Equals(sourceMode, AutomationCaptureSourceMode.ProcessWindow, StringComparison.OrdinalIgnoreCase))
            {
                var processName = AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureProcessName);
                result = capture.CaptureProcessWindowPhysical(processName);
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
}
