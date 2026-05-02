using System.Collections.Generic;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationTopologyAnalyzer
{
    AutomationTopologyAnalysis Analyze(AutomationGraphDocument document);

    ConnectionValidationResult ValidateConnection(
        AutomationGraphDocument document,
        Guid sourceNodeId,
        string sourcePortId,
        Guid targetNodeId,
        string targetPortId);

    ConnectionValidationResult ValidateConnection(
        AutomationGraphDocument document,
        Guid sourceNodeId,
        string sourcePortId,
        Guid targetNodeId,
        string targetPortId,
        IReadOnlySet<Guid>? ignoredEdgeIds);
}
