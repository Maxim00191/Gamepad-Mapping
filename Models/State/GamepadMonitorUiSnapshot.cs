namespace GamepadMapperGUI.Models;

/// <summary>Latest gamepad state for the monitor panel; written from the input thread and sampled by the UI timer.</summary>
public readonly record struct GamepadMonitorUiSnapshot(
    float LeftThumbX,
    float LeftThumbY,
    float RightThumbX,
    float RightThumbY,
    float LeftTrigger,
    float RightTrigger,
    string LastButtonPressed,
    string LastButtonReleased);
