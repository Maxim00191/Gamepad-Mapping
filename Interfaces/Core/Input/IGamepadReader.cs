using System;
using System.Numerics;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Core;

public interface IGamepadReader : IDisposable
{
    event Action<InputFrame>? OnInputFrame;
    void Start();
    void Stop();

    /// <summary>Delay between gamepad polls (ms). Applied via <c>GamepadInputStreamConstraints</c> clamping.</summary>
    int PollingIntervalMs { get; set; }

    /// <summary>Minimum analog delta before a new input frame is emitted (excluding first frame). Clamped when applied.</summary>
    float AnalogChangeEpsilon { get; set; }

    ThumbstickDeadzoneShape ThumbstickDeadzoneShape { get; set; }

    float LeftThumbstickDeadzone { get; set; }
    float RightThumbstickDeadzone { get; set; }
    float LeftTriggerInnerDeadzone { get; set; }
    float LeftTriggerOuterDeadzone { get; set; }
    float RightTriggerInnerDeadzone { get; set; }
    float RightTriggerOuterDeadzone { get; set; }
}

