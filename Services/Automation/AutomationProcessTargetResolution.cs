#nullable enable

using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public static class AutomationProcessTargetResolution
{
    public static AutomationProcessWindowTarget ResolveLiveTarget(
        IProcessTargetService? processTargetService,
        string? processName,
        int processId)
    {
        var configured = AutomationProcessWindowTarget.From(processName, processId);
        if (processTargetService is null || string.IsNullOrWhiteSpace(configured.ProcessName))
            return configured;

        var live = processTargetService.CreateTargetFromDeclaredProcessName(configured.ProcessName);
        if (live.ProcessId <= 0)
            return configured;

        var liveName = string.IsNullOrWhiteSpace(live.ProcessName)
            ? configured.ProcessName
            : live.ProcessName;
        return AutomationProcessWindowTarget.From(liveName, live.ProcessId);
    }
}
