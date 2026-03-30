using System;
using System.Collections.Generic;

namespace GamepadMapperGUI.Core;

internal sealed class ButtonEventPipeline
{
    private readonly IReadOnlyList<IButtonEventMiddleware> _middlewares;
    private readonly Action<ButtonEventContext> _terminal;

    public ButtonEventPipeline(
        IReadOnlyList<IButtonEventMiddleware> middlewares,
        Action<ButtonEventContext> terminal)
    {
        _middlewares = middlewares;
        _terminal = terminal;
    }

    public void Invoke(ButtonEventContext context)
    {
        var index = -1;
        void Next(ButtonEventContext ctx)
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
