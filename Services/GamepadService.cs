using System;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Core;

namespace GamepadMapperGUI.Services;

/// <summary>
/// Implementation of IGamepadService that wraps IGamepadReader.
/// </summary>
public class GamepadService : IGamepadService
{
    private readonly IGamepadReader _reader;
    private bool _isRunning;

    public GamepadService(IGamepadReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _reader.OnInputFrame += (frame) => OnInputFrame?.Invoke(frame);
    }

    public bool IsRunning => _isRunning;

    public event Action<InputFrame>? OnInputFrame;

    public void Start()
    {
        if (_isRunning) return;
        _reader.Start();
        _isRunning = true;
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _reader.Stop();
        _isRunning = false;
    }

    public void SetThumbstickDeadzones(float left, float right)
    {
        if (_reader is GamepadReader gr)
        {
            gr.LeftThumbstickDeadzone = left;
            gr.RightThumbstickDeadzone = right;
        }
    }

    public void SetTriggerDeadzones(float leftInner, float leftOuter, float rightInner, float rightOuter)
    {
        if (_reader is GamepadReader gr)
        {
            gr.LeftTriggerInnerDeadzone = leftInner;
            gr.LeftTriggerOuterDeadzone = leftOuter;
            gr.RightTriggerInnerDeadzone = rightInner;
            gr.RightTriggerOuterDeadzone = rightOuter;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
