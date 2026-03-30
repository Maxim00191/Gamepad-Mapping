using Vortice.XInput;

namespace GamepadMapperGUI.Models;

public readonly record struct InputFrameProcessingResult(
    GamepadButtons[] PressedButtons,
    GamepadButtons[] ReleasedButtons);

