using System;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Input;

/// <summary>
/// Native PlayStation source (DualSense HID).
/// </summary>
public sealed class PlayStationNativeSource(
    IPlayStationInputProvider playStationInputProvider) : IGamepadSource
{
    private readonly IPlayStationInputProvider _playStationInputProvider = playStationInputProvider;

    public bool TryGetFrame(out InputFrame frame)
    {
        if (_playStationInputProvider.TryGetState(out var nativeState))
        {
            frame = new InputFrame(
                Buttons: nativeState.Buttons,
                LeftThumbstick: nativeState.LeftThumbstick,
                RightThumbstick: nativeState.RightThumbstick,
                LeftTrigger: nativeState.LeftTrigger,
                RightTrigger: nativeState.RightTrigger,
                IsConnected: true,
                TimestampMs: nativeState.TimestampMs,
                PlayStationState: nativeState);
            return true;
        }

        frame = InputFrame.Disconnected(Environment.TickCount64);
        return false;
    }
}
