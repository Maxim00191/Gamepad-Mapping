using System.Text.Json.Nodes;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public static class AutomationRoiPreflight
{
    public static string? FindFirstEmptyRoiDocument(AutomationGraphDocument document)
    {
        foreach (var node in document.Nodes)
        {
            if (!string.Equals(node.NodeTypeId, "perception.capture_screen", StringComparison.Ordinal))
                continue;

            var mode = ReadCaptureMode(node.Properties);
            if (!string.Equals(mode, "roi", StringComparison.OrdinalIgnoreCase))
                continue;

            if (AutomationNodePropertyReader.TryReadRoiCapture(node.Properties, out var roi) && !roi.IsEmpty)
                continue;

            return node.Id.ToString();
        }

        return null;
    }

    private static string ReadCaptureMode(JsonObject? props)
    {
        if (props is null || !props.TryGetPropertyValue(AutomationNodePropertyKeys.CaptureMode, out var n) ||
            n is null)
            return "full";

        return n.ToString().Trim('"');
    }
}
