#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationHumanNoiseTargetResolver
{
    AutomationHumanNoiseTarget Resolve(IAutomationExecutionGraphIndex index, AutomationNodeState node);
}
