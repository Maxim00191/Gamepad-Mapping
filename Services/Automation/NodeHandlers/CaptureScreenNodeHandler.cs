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
            return context.GetExecutionTarget(node.Id, "flow.out");
        }

        var metrics = AutomationVirtualScreenNative.GetPhysicalVirtualScreen();
        var fullBitmap = context.Capture.CaptureVirtualScreenPhysical();
        context.StoreCapture(node.Id, fullBitmap, metrics.PhysicalOriginX, metrics.PhysicalOriginY);
        return context.GetExecutionTarget(node.Id, "flow.out");
    }
}
