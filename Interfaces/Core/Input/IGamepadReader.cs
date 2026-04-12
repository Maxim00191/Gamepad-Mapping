using System;
using System.Numerics;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Core;

public interface IGamepadReader : IDisposable
{
    event Action<InputFrame>? OnInputFrame;
    void Start();
    void Stop();

    ThumbstickDeadzoneShape ThumbstickDeadzoneShape { get; set; }

    float LeftThumbstickDeadzone { get; set; }
    float RightThumbstickDeadzone { get; set; }
    float LeftTriggerInnerDeadzone { get; set; }
    float LeftTriggerOuterDeadzone { get; set; }
    float RightTriggerInnerDeadzone { get; set; }
    float RightTriggerOuterDeadzone { get; set; }
}

