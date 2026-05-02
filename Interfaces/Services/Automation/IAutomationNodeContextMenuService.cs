#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationNodeContextMenuService
{
    IReadOnlyList<AutomationNodeContextMenuAction> BuildNodeActions(
        Guid targetNodeId,
        string targetNodeTypeId,
        Guid? selectedNodeId,
        string? selectedNodeTypeId);

    bool TryCopyNodeIdToClipboard(Guid nodeId);

    bool TryCopyNodeTypeIdToClipboard(string nodeTypeId);
}
