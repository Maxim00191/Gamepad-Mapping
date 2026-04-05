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
        return dispatchLegacy(keyboardKey, trigger, out errorStatus);
    }

    public bool TryExecuteDeferredSoloRelease(string sourceToken, out string? errorStatus)
    {
        errorStatus = null;
        return false;
    }

    public bool RequiresDeferralOnPress => false;
}
