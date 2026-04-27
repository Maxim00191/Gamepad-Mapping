#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationExecutionGraphIndex
{
    AutomationNodeState? GetNode(Guid nodeId);

    Guid? GetExecutionTarget(Guid sourceNodeId, string sourcePortId);

    Guid? GetDataSource(Guid targetNodeId, string targetPortId);

    (Guid SourceNodeId, string SourcePortId)? GetDataSourceLink(Guid targetNodeId, string targetPortId);

    IReadOnlyList<Guid> FindExecutionRoots(INodeTypeRegistry registry);
}
