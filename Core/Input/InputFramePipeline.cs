using System;
using System.Collections.Generic;

namespace GamepadMapperGUI.Core;

internal sealed class InputFramePipeline
{
    private readonly IReadOnlyList<IInputFrameMiddleware> _middlewares;
    private readonly Action<InputFrameContext> _terminal;

    public InputFramePipeline(
        IReadOnlyList<IInputFrameMiddleware> middlewares,
        Action<InputFrameContext> terminal)
    {
        _middlewares = middlewares;
        _terminal = terminal;
    }

    public void Invoke(InputFrameContext context)
    {
        var index = -1;
        void Next(InputFrameContext ctx)
        {
            index++;
            if (index < _middlewares.Count)
                _middlewares[index].Invoke(ctx, Next);
            else
                _terminal(ctx);
        }

        Next(context);
    }
}
