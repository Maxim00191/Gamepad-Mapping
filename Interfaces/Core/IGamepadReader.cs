using System;
using System.Numerics;
using Vortice.XInput;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Core;

public interface IGamepadReader
{
    event Action<InputFrame>? OnInputFrame;
    void Start();
    void Stop();
}
