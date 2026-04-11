using System;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Input;

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
    void SetThumbstickDeadzoneShape(ThumbstickDeadzoneShape shape);
    void SetTriggerDeadzones(float leftInner, float leftOuter, float rightInner, float rightOuter);

    /// <summary>
    /// Replaces the underlying reader.
    /// </summary>
    void ReplaceReader(IGamepadReader reader);
}

