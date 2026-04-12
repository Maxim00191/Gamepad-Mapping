using System.Numerics;

namespace GamepadMapperGUI.Models;

[Flags]
public enum GamepadButtons : uint
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

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

