using System.Numerics;

namespace GamepadMapperGUI.Models;

public readonly record struct PlayStationTouchPoint(
    bool IsActive,
    int TrackingId,
    float XNormalized,
    float YNormalized);

public readonly record struct PlayStationInputState(
    GamepadButtons Buttons,
    Vector2 LeftThumbstick,
    Vector2 RightThumbstick,
    float LeftTrigger,
    float RightTrigger,
    Vector3 Gyroscope,
    bool IsTouchpadPressed,
    PlayStationTouchPoint PrimaryTouch,
    PlayStationTouchPoint SecondaryTouch,
    long TimestampMs);
