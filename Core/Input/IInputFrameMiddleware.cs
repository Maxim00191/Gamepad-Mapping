using System;

namespace GamepadMapperGUI.Core;

internal interface IInputFrameMiddleware
{
    void Invoke(InputFrameContext context, Action<InputFrameContext> next);
}
