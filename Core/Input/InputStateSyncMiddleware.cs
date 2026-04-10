using System;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

internal sealed class InputStateSyncMiddleware : IInputFrameMiddleware
{
    private readonly Action<IReadOnlyCollection<GamepadButtons>, float, float> _syncState;

    public InputStateSyncMiddleware(Action<IReadOnlyCollection<GamepadButtons>, float, float> syncState)
    {
        _syncState = syncState;
    }

    public void Invoke(InputFrameContext context, Action<InputFrameContext> next)
    {
        var frame = context.Frame;
        var activeButtons = ToActiveButtonsSet(frame.Buttons);
        
        _syncState(activeButtons, frame.LeftTrigger, frame.RightTrigger);
        
        next(context);
    }

    private static HashSet<GamepadButtons> ToActiveButtonsSet(GamepadButtons buttons)
    {
        var result = new HashSet<GamepadButtons>();
        var mask = (uint)buttons;
        if (mask == 0) return result;

        for (var bitIndex = 0; bitIndex < 32; bitIndex++)
        {
            var bit = 1u << bitIndex;
            if ((mask & bit) == 0) continue;

            var flag = (GamepadButtons)bit;
            if (Enum.IsDefined(typeof(GamepadButtons), flag))
                result.Add(flag);
        }

        return result;
    }
}
