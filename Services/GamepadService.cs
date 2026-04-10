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
    private IGamepadReader _reader;
    private bool _isRunning;

    public GamepadService(IGamepadReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _reader.OnInputFrame += HandleInputFrame;
    }

    private void HandleInputFrame(InputFrame frame) => OnInputFrame?.Invoke(frame);

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
        _reader.LeftThumbstickDeadzone = left;
        _reader.RightThumbstickDeadzone = right;
    }

    public void SetTriggerDeadzones(float leftInner, float leftOuter, float rightInner, float rightOuter)
    {
        _reader.LeftTriggerInnerDeadzone = leftInner;
        _reader.LeftTriggerOuterDeadzone = leftOuter;
        _reader.RightTriggerInnerDeadzone = rightInner;
        _reader.RightTriggerOuterDeadzone = rightOuter;
    }

    public void ReplaceReader(IGamepadReader reader)
    {
        var wasRunning = _isRunning;
        if (wasRunning) Stop();

        _reader.OnInputFrame -= HandleInputFrame;
        _reader.Dispose();

        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _reader.OnInputFrame += HandleInputFrame;

        if (wasRunning) Start();
    }

    public void Dispose()
    {
        Stop();
        _reader.Dispose();
    }
}
