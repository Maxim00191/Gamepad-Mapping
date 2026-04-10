using System;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services;

/// <summary>
/// Manages gamepad lifecycle, polling, and deadzone configuration.
/// </summary>
public interface IGamepadService : IDisposable
{
    bool IsRunning { get; }
    
    event Action<InputFrame>? OnInputFrame;
    
    void Start();
    void Stop();
    
    void SetThumbstickDeadzones(float left, float right);
    void SetTriggerDeadzones(float leftInner, float leftOuter, float rightInner, float rightOuter);
}
