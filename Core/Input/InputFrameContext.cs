using System;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

internal sealed class InputFrameContext
{
    public required InputFrame Frame { get; init; }

    public bool IsFirstFrame { get; set; }

    public GamepadButtons PreviousButtonsMask { get; set; } = GamepadButtons.None;

    public GamepadButtons[] PressedButtons { get; set; } = Array.Empty<GamepadButtons>();

    public GamepadButtons[] ReleasedButtons { get; set; } = Array.Empty<GamepadButtons>();
}
