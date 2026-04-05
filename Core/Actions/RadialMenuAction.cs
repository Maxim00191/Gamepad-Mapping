using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Actions;

internal sealed class RadialMenuAction(
    MappingEntry mapping,
    IRadialMenuController controller) : IExecutableAction
{
    public bool Execute(TriggerMoment trigger, string sourceToken, out string? errorStatus)
    {
        if (trigger == TriggerMoment.Pressed)
        {
            return controller.TryOpen(mapping, sourceToken, out errorStatus);
        }

        if (trigger == TriggerMoment.Released)
        {
            errorStatus = null;
            if (mapping.RadialMenu is not { } rm)
                return false;

            // Note: The confirm mode is handled by the controller via the Func provided in MappingEngine
            // Here we just trigger the close.
            return controller.TryClose(rm.RadialMenuId, sourceToken, true);
        }

        errorStatus = null;
        return false;
    }

    public bool TryExecuteDeferredSoloRelease(string sourceToken, out string? errorStatus)
    {
        return Execute(TriggerMoment.Pressed, sourceToken, out errorStatus);
    }

    public bool RequiresDeferralOnPress => true;
}
