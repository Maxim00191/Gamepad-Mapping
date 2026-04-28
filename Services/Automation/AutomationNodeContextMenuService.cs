#nullable enable

using System.Windows;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationNodeContextMenuService : IAutomationNodeContextMenuService
{
    private const string CaptureNodeTypeId = "perception.capture_screen";

    public IReadOnlyList<AutomationNodeContextMenuAction> BuildNodeActions(
        Guid targetNodeId,
        string targetNodeTypeId,
        Guid? selectedNodeId,
        string? selectedNodeTypeId)
    {
        var actions = new List<AutomationNodeContextMenuAction>
        {
            new()
            {
                Kind = AutomationNodeContextMenuActionKind.CopyNodeId,
                LabelResourceKey = "AutomationNodeContextMenu_CopyNodeId"
            },
            new()
            {
                Kind = AutomationNodeContextMenuActionKind.CopyNodeTypeId,
                LabelResourceKey = "AutomationNodeContextMenu_CopyNodeTypeId"
            }
        };

        var selectedIsCapture = string.Equals(selectedNodeTypeId, CaptureNodeTypeId, StringComparison.OrdinalIgnoreCase);
        var targetIsCapture = string.Equals(targetNodeTypeId, CaptureNodeTypeId, StringComparison.OrdinalIgnoreCase);
        var canUseAsCaptureCacheSource = selectedIsCapture &&
                                         targetIsCapture &&
                                         selectedNodeId.HasValue &&
                                         selectedNodeId.Value != targetNodeId;
        if (selectedIsCapture)
        {
            actions.Add(new AutomationNodeContextMenuAction
            {
                Kind = AutomationNodeContextMenuActionKind.UseAsCaptureCacheSource,
                LabelResourceKey = "AutomationNodeContextMenu_UseAsCaptureCacheSource",
                IsEnabled = canUseAsCaptureCacheSource
            });
        }

        return actions;
    }

    public bool TryCopyNodeIdToClipboard(Guid nodeId) =>
        TrySetClipboardText(nodeId.ToString("D"));

    public bool TryCopyNodeTypeIdToClipboard(string nodeTypeId)
    {
        var normalized = nodeTypeId.Trim();
        return normalized.Length > 0 && TrySetClipboardText(normalized);
    }

    private static bool TrySetClipboardText(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
