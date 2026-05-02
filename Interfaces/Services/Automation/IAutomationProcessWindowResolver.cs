#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationProcessWindowResolver
{
    bool TryResolveWindowHandle(
        string? processName,
        out IntPtr windowHandle,
        out AutomationPhysicalRect windowBounds);

    bool TryResolveWindowHandle(
        AutomationProcessWindowTarget processTarget,
        out IntPtr windowHandle,
        out AutomationPhysicalRect windowBounds,
        out AutomationProcessWindowTarget resolvedTarget);
}
