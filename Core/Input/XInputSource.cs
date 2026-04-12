using System;
using System.Numerics;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using Vortice.XInput;
using GamepadButtons = GamepadMapperGUI.Models.GamepadButtons;

namespace GamepadMapperGUI.Core.Input;

/// <summary>
/// XInput implementation of IGamepadSource.
/// </summary>
public class XInputSource(IXInput xinput, uint userIndex = 0) : IGamepadSource
{
    private readonly IXInput _xinput = xinput ?? throw new ArgumentNullException(nameof(xinput));
    private readonly uint _userIndex = userIndex;

    public bool TryGetFrame(out InputFrame frame)
    {
        if (_xinput.GetState(_userIndex, out State xState))
        {
            frame = new InputFrame(
                Buttons: (GamepadButtons)xState.Gamepad.Buttons,
                LeftThumbstick: new Vector2(NormalizeAxis(xState.Gamepad.LeftThumbX), NormalizeAxis(xState.Gamepad.LeftThumbY)),
                RightThumbstick: new Vector2(NormalizeAxis(xState.Gamepad.RightThumbX), NormalizeAxis(xState.Gamepad.RightThumbY)),
                LeftTrigger: xState.Gamepad.LeftTrigger / 255f,
                RightTrigger: xState.Gamepad.RightTrigger / 255f,
                IsConnected: true,
                TimestampMs: Environment.TickCount64);
            return true;
        }

        frame = InputFrame.Disconnected(Environment.TickCount64);
        return false;
    }

    private static float NormalizeAxis(short value)
    {
        var normalized = value < 0 ? value / 32768f : value / 32767f;
        return Math.Clamp(normalized, -1f, 1f);
    }
}
