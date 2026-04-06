using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Core;

internal interface IExecutableAction
{
    bool Execute(TriggerMoment trigger, string sourceToken, out string? errorStatus);
    
    bool TryExecuteDeferredSoloRelease(string sourceToken, out string? errorStatus);

    bool RequiresDeferralOnPress { get; }
}
