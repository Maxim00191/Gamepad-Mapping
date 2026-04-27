#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationExecutionSafetyPolicy
{
    AutomationExecutionSafetyLimits GetLimits(AutomationGraphDocument document);
}
