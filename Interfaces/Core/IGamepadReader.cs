using System;
using System.Numerics;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Core;

public interface IGamepadReader
{
    event Action<InputFrame>? OnInputFrame;
    void Start();
    void Stop();
}

