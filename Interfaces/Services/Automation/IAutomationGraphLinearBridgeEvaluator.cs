#nullable enable

using System.Diagnostics.CodeAnalysis;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationGraphLinearBridgeEvaluator
{
    bool TryBuildBridgeAcrossNode(
        AutomationGraphDocument document,
        Guid nodeId,
        [NotNullWhen(true)] out AutomationGraphLinearBridgePlan? plan);
}
