#nullable enable

using System.Text.Json.Nodes;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class CaptureScreenNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "perception.capture_screen";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, IList<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cacheRef = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.CaptureCacheRefNodeId);
        if (Guid.TryParse(cacheRef, out var cachedNodeId) &&
            context.TryGetCapture(
                cachedNodeId,
                out var cachedBitmap,
                out var cachedOriginX,
                out var cachedOriginY,
                out AutomationProcessWindowTarget cachedTargetProcess))
        {
            context.StoreCapture(node.Id, cachedBitmap, cachedOriginX, cachedOriginY, cachedTargetProcess);
            context.SetCaptureTargetProcess(cachedTargetProcess);
            if (context.VerboseExecutionLog)
                log.Add($"[capture_screen] reused_cache source={AutomationLogFormatter.NodeId(cachedNodeId)} origin=({cachedOriginX},{cachedOriginY}) size={cachedBitmap.PixelWidth}x{cachedBitmap.PixelHeight}");
            return context.GetExecutionTarget(node.Id, "flow.out");
        }

        if (!AutomationDirectScreenCapture.TryDirectCapture(
                context.CaptureResolver,
                node.Properties,
                out var direct,
                context.ProcessTargetService))
            throw new InvalidOperationException("capture_unavailable");

        var requestedTarget = ResolveCaptureTargetProcess(context, node.Properties);
        var captureTarget = direct.ProcessTarget.IsEmpty ? requestedTarget : direct.ProcessTarget;
        context.StoreCapture(
            node.Id,
            direct.Bitmap,
            direct.Metrics.PhysicalOriginX,
            direct.Metrics.PhysicalOriginY,
            captureTarget);
        context.SetCaptureTargetProcess(captureTarget);
        if (context.VerboseExecutionLog)
            log.Add(FormatDirectCaptureLogLine(context, node.Properties, direct));
        return context.GetExecutionTarget(node.Id, "flow.out");
    }

    private static AutomationProcessWindowTarget ResolveCaptureTargetProcess(
        AutomationRuntimeContext context,
        JsonObject? properties)
    {
        var sourceMode = AutomationCaptureSourceMode.Normalize(
            AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureSourceMode));
        if (!AutomationCaptureSourceMode.IsInProcessWindow(sourceMode))
            return default;

        var processName = AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureProcessName);
        var processId = AutomationNodePropertyReader.ReadInt(properties, AutomationNodePropertyKeys.CaptureProcessId, 0);
        return AutomationProcessTargetResolution.ResolveLiveTarget(context.ProcessTargetService, processName, processId);
    }

    private static string FormatDirectCaptureLogLine(
        AutomationRuntimeContext context,
        JsonObject? properties,
        AutomationVirtualScreenCaptureResult direct)
    {
        var mode = AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureMode);
        if (string.IsNullOrWhiteSpace(mode))
            mode = AutomationCaptureMode.Full;

        if (string.Equals(mode, AutomationCaptureMode.Roi, StringComparison.OrdinalIgnoreCase) &&
            AutomationNodePropertyReader.TryReadRoiCapture(properties, out var roi) && !roi.IsEmpty)
        {
            var roiSourceMode = AutomationCaptureSourceMode.Normalize(
                AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureSourceMode));
            if (AutomationCaptureSourceMode.IsInProcessWindow(roiSourceMode))
            {
                var target = direct.ProcessTarget.IsEmpty
                    ? ResolveCaptureTargetProcess(context, properties)
                    : direct.ProcessTarget;
                return
                    $"[capture_screen] mode=roi source=in_process_window process={target.DisplayName} rect=({roi.X},{roi.Y},{roi.Width},{roi.Height}) size={direct.Bitmap.PixelWidth}x{direct.Bitmap.PixelHeight}";
            }

            return
                $"[capture_screen] mode=roi rect=({roi.X},{roi.Y},{roi.Width},{roi.Height}) size={direct.Bitmap.PixelWidth}x{direct.Bitmap.PixelHeight}";
        }

        var sourceMode = AutomationCaptureSourceMode.Normalize(
            AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureSourceMode));

        if (AutomationCaptureSourceMode.IsInProcessWindow(sourceMode))
        {
            var target = direct.ProcessTarget.IsEmpty
                ? ResolveCaptureTargetProcess(context, properties)
                : direct.ProcessTarget;
            return
                $"[capture_screen] mode=full source=in_process_window process={target.DisplayName} origin=({direct.Metrics.PhysicalOriginX},{direct.Metrics.PhysicalOriginY}) size={direct.Bitmap.PixelWidth}x{direct.Bitmap.PixelHeight}";
        }

        return
            $"[capture_screen] mode=full source=screen origin=({direct.Metrics.PhysicalOriginX},{direct.Metrics.PhysicalOriginY}) size={direct.Bitmap.PixelWidth}x{direct.Bitmap.PixelHeight}";
    }
}
