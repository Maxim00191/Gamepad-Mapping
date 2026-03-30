using System.Numerics;
using Vortice.XInput;

namespace GamepadMapperGUI.Models;

public readonly record struct InputFrame(
    GamepadButtons Buttons,
    Vector2 LeftThumbstick,
    Vector2 RightThumbstick,
    float LeftTrigger,
    float RightTrigger,
    bool IsConnected,
    long TimestampMs)
{
    public static InputFrame Disconnected(long timestampMs) =>
        new(
            Buttons: GamepadButtons.None,
            LeftThumbstick: Vector2.Zero,
            RightThumbstick: Vector2.Zero,
            LeftTrigger: 0f,
            RightTrigger: 0f,
            IsConnected: false,
            TimestampMs: timestampMs);
}

