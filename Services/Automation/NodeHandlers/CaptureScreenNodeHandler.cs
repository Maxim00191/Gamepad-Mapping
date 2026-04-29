#nullable enable

using System.Text.Json.Nodes;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class CaptureScreenNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "perception.capture_screen";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cacheRef = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.CaptureCacheRefNodeId);
        if (Guid.TryParse(cacheRef, out var cachedNodeId) &&
            context.TryGetCapture(cachedNodeId, out var cachedBitmap, out var cachedOriginX, out var cachedOriginY))
        {
            context.StoreCapture(node.Id, cachedBitmap, cachedOriginX, cachedOriginY);
            log.Add($"[capture_screen] reused_cache source={AutomationLogFormatter.NodeId(cachedNodeId)} origin=({cachedOriginX},{cachedOriginY}) size={cachedBitmap.PixelWidth}x{cachedBitmap.PixelHeight}");
            return context.GetExecutionTarget(node.Id, "flow.out");
        }

        if (!AutomationDirectScreenCapture.TryDirectCapture(context.Capture, node.Properties, out var direct))
            throw new InvalidOperationException("capture_unavailable");

        context.StoreCapture(node.Id, direct.Bitmap, direct.Metrics.PhysicalOriginX, direct.Metrics.PhysicalOriginY);
        log.Add(FormatDirectCaptureLogLine(node.Properties, direct));
        return context.GetExecutionTarget(node.Id, "flow.out");
    }

    private static string FormatDirectCaptureLogLine(JsonObject? properties, AutomationVirtualScreenCaptureResult direct)
    {
        var mode = AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureMode);
        if (string.IsNullOrWhiteSpace(mode))
            mode = AutomationCaptureMode.Full;

        if (string.Equals(mode, AutomationCaptureMode.Roi, StringComparison.OrdinalIgnoreCase) &&
            AutomationNodePropertyReader.TryReadRoiCapture(properties, out var roi) && !roi.IsEmpty)
        {
            return
                $"[capture_screen] mode=roi rect=({roi.X},{roi.Y},{roi.Width},{roi.Height}) size={direct.Bitmap.PixelWidth}x{direct.Bitmap.PixelHeight}";
        }

        var sourceMode = AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureSourceMode);
        if (string.IsNullOrWhiteSpace(sourceMode))
            sourceMode = AutomationCaptureSourceMode.Screen;

        if (string.Equals(sourceMode, AutomationCaptureSourceMode.ProcessWindow, StringComparison.OrdinalIgnoreCase))
        {
            var processName = AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureProcessName);
            return
                $"[capture_screen] mode=full source=process_window process={processName} origin=({direct.Metrics.PhysicalOriginX},{direct.Metrics.PhysicalOriginY}) size={direct.Bitmap.PixelWidth}x{direct.Bitmap.PixelHeight}";
        }

        return
            $"[capture_screen] mode=full source=screen origin=({direct.Metrics.PhysicalOriginX},{direct.Metrics.PhysicalOriginY}) size={direct.Bitmap.PixelWidth}x{direct.Bitmap.PixelHeight}";
    }
}
