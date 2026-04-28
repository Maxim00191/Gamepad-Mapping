#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class CaptureScreenNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "perception.capture_screen";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cacheRef = AutomationNodePropertyReader.ReadString(node.Properties, "captureCacheRefNodeId");
        if (Guid.TryParse(cacheRef, out var cachedNodeId) &&
            context.TryGetCapture(cachedNodeId, out var cachedBitmap, out var cachedOriginX, out var cachedOriginY))
        {
            context.StoreCapture(node.Id, cachedBitmap, cachedOriginX, cachedOriginY);
            log.Add($"[capture_screen] reused_cache source={AutomationLogFormatter.NodeId(cachedNodeId)} origin=({cachedOriginX},{cachedOriginY}) size={cachedBitmap.PixelWidth}x{cachedBitmap.PixelHeight}");
            return context.GetExecutionTarget(node.Id, "flow.out");
        }

        var mode = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.CaptureMode);
        if (string.IsNullOrWhiteSpace(mode))
            mode = "full";

        if (string.Equals(mode, "roi", StringComparison.OrdinalIgnoreCase))
        {
            if (!AutomationNodePropertyReader.TryReadRoiCapture(node.Properties, out var roi) || roi.IsEmpty)
                throw new InvalidOperationException("roi_invalid");

            var bitmap = context.Capture.CaptureRectanglePhysical(roi.X, roi.Y, roi.Width, roi.Height);
            context.StoreCapture(node.Id, bitmap, roi.X, roi.Y);
            log.Add($"[capture_screen] mode=roi rect=({roi.X},{roi.Y},{roi.Width},{roi.Height}) size={bitmap.PixelWidth}x{bitmap.PixelHeight}");
            return context.GetExecutionTarget(node.Id, "flow.out");
        }

        var full = context.Capture.CaptureVirtualScreenPhysical();
        context.StoreCapture(node.Id, full.Bitmap, full.Metrics.PhysicalOriginX, full.Metrics.PhysicalOriginY);
        log.Add($"[capture_screen] mode=full origin=({full.Metrics.PhysicalOriginX},{full.Metrics.PhysicalOriginY}) size={full.Bitmap.PixelWidth}x{full.Bitmap.PixelHeight}");
        return context.GetExecutionTarget(node.Id, "flow.out");
    }
}
