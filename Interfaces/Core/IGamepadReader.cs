using System;
using System.Numerics;
using Vortice.XInput;

namespace GamepadMapperGUI.Interfaces.Core;

public interface IGamepadReader
{
    event Action<GamepadButtons>? OnButtonPressed;
    event Action<GamepadButtons>? OnButtonReleased;
    event Action<Vector2>? OnLeftThumbstickChanged;
    event Action<Vector2>? OnRightThumbstickChanged;
    event Action<float>? OnLeftTriggerChanged;
    event Action<float>? OnRightTriggerChanged;
    void Start();
    void Stop();
}
