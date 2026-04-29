#nullable enable

using System.Text.Json.Nodes;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public static class AutomationCapturePreviewSupport
{
    public static bool TryGetPreviewableProperties(
        string? nodeTypeId,
        JsonObject? properties,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsonObject? captureProps,
        out AutomationCapturePreviewBlockReason blockReason)
    {
        captureProps = null;
        blockReason = AutomationCapturePreviewBlockReason.MissingProperties;

        if (string.IsNullOrEmpty(nodeTypeId) ||
            !string.Equals(nodeTypeId, AutomationNodeTypeIds.CaptureScreen, StringComparison.OrdinalIgnoreCase))
        {
            blockReason = AutomationCapturePreviewBlockReason.NotCaptureNode;
            return false;
        }

        if (properties is null)
        {
            blockReason = AutomationCapturePreviewBlockReason.MissingProperties;
            return false;
        }

        var cacheRef = AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureCacheRefNodeId);
        if (Guid.TryParse(cacheRef, out var guid) && guid != Guid.Empty)
        {
            blockReason = AutomationCapturePreviewBlockReason.CacheReference;
            return false;
        }

        var mode = AutomationNodePropertyReader.ReadString(properties, AutomationNodePropertyKeys.CaptureMode);
        if (string.IsNullOrWhiteSpace(mode))
            mode = AutomationCaptureMode.Full;

        if (string.Equals(mode, AutomationCaptureMode.Roi, StringComparison.OrdinalIgnoreCase))
        {
            if (!AutomationNodePropertyReader.TryReadRoiCapture(properties, out var roi) || roi.IsEmpty)
            {
                blockReason = AutomationCapturePreviewBlockReason.InvalidRoi;
                return false;
            }
        }

        captureProps = properties;
        blockReason = AutomationCapturePreviewBlockReason.None;
        return true;
    }

    /// <summary>
    /// Full-screen (virtual desktop or process window) live refresh can be expensive; ROI-sized captures are cheap enough to stream by default.
    /// </summary>
    public static bool SuggestInspectorLiveByDefault(JsonObject props)
    {
        var mode = AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.CaptureMode);
        if (string.IsNullOrWhiteSpace(mode))
            mode = AutomationCaptureMode.Full;

        return string.Equals(mode, AutomationCaptureMode.Roi, StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatCaptureStatus(JsonObject props, Func<string, string> localizer)
    {
        var mode = AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.CaptureMode);
        if (string.IsNullOrWhiteSpace(mode))
            mode = AutomationCaptureMode.Full;

        if (string.Equals(mode, AutomationCaptureMode.Roi, StringComparison.OrdinalIgnoreCase) &&
            AutomationNodePropertyReader.TryReadRoiCapture(props, out var roi) &&
            !roi.IsEmpty)
        {
            return string.Format(
                localizer("AutomationRoiPreview_StatusRoiFormat"),
                roi.X,
                roi.Y,
                roi.Width,
                roi.Height);
        }

        if (string.Equals(mode, AutomationCaptureMode.Full, StringComparison.OrdinalIgnoreCase))
        {
            var sourceMode = AutomationCaptureSourceMode.Normalize(
                AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.CaptureSourceMode));

            if (AutomationCaptureSourceMode.IsInProcessWindow(sourceMode))
            {
                var target = ReadProcessTarget(props);
                var label = target.IsEmpty
                    ? localizer("AutomationRoiPreview_TargetProcessUnspecifiedLabel")
                    : target.DisplayName;
                return string.Format(localizer("AutomationRoiPreview_StatusProcessWindowFormat"), label);
            }

            return localizer("AutomationRoiPreview_StatusFullScreenShort");
        }

        return localizer("AutomationRoiPreview_StatusFullScreenShort");
    }

    private static AutomationProcessWindowTarget ReadProcessTarget(JsonObject props)
    {
        var processName = AutomationNodePropertyReader.ReadString(props, AutomationNodePropertyKeys.CaptureProcessName);
        var processId = AutomationNodePropertyReader.ReadInt(props, AutomationNodePropertyKeys.CaptureProcessId, 0);
        return AutomationProcessWindowTarget.From(processName, processId);
    }
}
