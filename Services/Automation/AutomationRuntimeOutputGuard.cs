#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationRuntimeOutputGuard : IAutomationRuntimeOutputGuard
{
    public AutomationRuntimeOutputGuard(IProcessTargetService processTargetService)
    {
        ArgumentNullException.ThrowIfNull(processTargetService);
    }

    public bool CanDispatchOutput(AutomationRuntimeContext context, out string? suppressReason)
    {
        suppressReason = null;
        return true;
    }
}
