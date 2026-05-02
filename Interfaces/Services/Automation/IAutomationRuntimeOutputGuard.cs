using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationRuntimeOutputGuard
{
    bool CanDispatchOutput(AutomationRuntimeContext context, out string? suppressReason);
}
