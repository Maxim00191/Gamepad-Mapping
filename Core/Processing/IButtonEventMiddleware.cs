using System;

namespace GamepadMapperGUI.Core;

internal interface IButtonEventMiddleware
{
    void Invoke(ButtonEventContext context, Action<ButtonEventContext> next);
}
