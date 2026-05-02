#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

internal static class AutomationOutputDispatchGuard
{
    public static bool CanDispatch(AutomationRuntimeContext context, string nodeTag, IList<string> log)
    {
        if (context.OutputGuard is null)
            return true;
        if (context.OutputGuard.CanDispatchOutput(context, out var reason))
            return true;

        log.Add($"[{nodeTag}] suppressed reason={reason}");
        return false;
    }
}
