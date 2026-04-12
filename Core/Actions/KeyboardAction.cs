using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Actions;

internal delegate bool LegacyDispatchDelegate(string keyboardKey, TriggerMoment trigger, out string? errorStatus);

internal sealed class KeyboardAction(
    string keyboardKey,
    LegacyDispatchDelegate dispatchLegacy) : IExecutableAction
{
    public bool Execute(TriggerMoment trigger, string sourceToken, out string? errorStatus)
    {
        if (trigger == TriggerMoment.Tap)
        {
            return dispatchLegacy(keyboardKey, TriggerMoment.Tap, out errorStatus);
        }
        return dispatchLegacy(keyboardKey, trigger, out errorStatus);
    }

    public bool TryExecuteDeferredSoloRelease(string sourceToken, out string? errorStatus)
    {
        // For standard keyboard actions, a deferred solo release (short release) 
        // should be treated as a Tap to ensure the key is registered.
        return dispatchLegacy(keyboardKey, TriggerMoment.Tap, out errorStatus);
    }

    public bool RequiresDeferralOnPress => false;
}
