using System;
using System.Collections.Generic;
using System.Linq;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

internal sealed class InputFrameTransitionMiddleware : IInputFrameMiddleware
{
    private bool _hasPrevious;
    private GamepadButtons _previousButtons = GamepadButtons.None;

    public void Invoke(InputFrameContext context, Action<InputFrameContext> next)
    {
        var currentButtons = context.Frame.Buttons;
        if (!_hasPrevious)
        {
            context.IsFirstFrame = true;
            context.PreviousButtonsMask = GamepadButtons.None;
            context.PressedButtons = Array.Empty<GamepadButtons>();
            context.ReleasedButtons = Array.Empty<GamepadButtons>();
            _previousButtons = currentButtons;
            _hasPrevious = true;
            next(context);
            return;
        }

        context.IsFirstFrame = false;
        context.PreviousButtonsMask = _previousButtons;

        var pressedMask = currentButtons & ~_previousButtons;
        var releasedMask = _previousButtons & ~currentButtons;

        context.PressedButtons = EnumerateSetFlags(pressedMask).ToArray();
        context.ReleasedButtons = EnumerateSetFlags(releasedMask).ToArray();

        _previousButtons = currentButtons;
        next(context);
    }

    private static IEnumerable<GamepadButtons> EnumerateSetFlags(GamepadButtons buttons)
    {
        var mask = (uint)buttons;
        if (mask == 0)
            yield break;

        for (var bitIndex = 0; bitIndex < 32; bitIndex++)
        {
            var bit = 1u << bitIndex;
            if ((mask & bit) == 0)
                continue;

            var flag = (GamepadButtons)bit;
            if (Enum.IsDefined(typeof(GamepadButtons), flag))
                yield return flag;
        }
    }
}
