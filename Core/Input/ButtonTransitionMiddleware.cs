using System;
using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

internal sealed class ButtonTransitionMiddleware : IInputFrameMiddleware
{
    private readonly Action<GamepadButtons, TriggerMoment, IReadOnlyCollection<GamepadButtons>, IReadOnlyList<MappingEntry>, float, float> _handleButtonEvent;
    private readonly Func<IReadOnlyList<MappingEntry>> _getMappingsSnapshot;
    private readonly Action<GamepadButtons> _onButtonReleased;

    public ButtonTransitionMiddleware(
        Action<GamepadButtons, TriggerMoment, IReadOnlyCollection<GamepadButtons>, IReadOnlyList<MappingEntry>, float, float> handleButtonEvent,
        Func<IReadOnlyList<MappingEntry>> getMappingsSnapshot,
        Action<GamepadButtons> onButtonReleased)
    {
        _handleButtonEvent = handleButtonEvent;
        _getMappingsSnapshot = getMappingsSnapshot;
        _onButtonReleased = onButtonReleased;
    }

    public void Invoke(InputFrameContext context, Action<InputFrameContext> next)
    {
        if (context.IsFirstFrame)
        {
            next(context);
            return;
        }

        var mappings = _getMappingsSnapshot();
        var workingActiveButtons = ToActiveButtonsSet(context.PreviousButtonsMask);

        foreach (var pressedButton in context.PressedButtons)
        {
            workingActiveButtons.Add(pressedButton);

            // Pressed mappings
            _handleButtonEvent(
                pressedButton,
                TriggerMoment.Pressed,
                workingActiveButtons,
                mappings,
                context.Frame.LeftTrigger,
                context.Frame.RightTrigger);

            // Tap mappings
            _handleButtonEvent(
                pressedButton,
                TriggerMoment.Tap,
                workingActiveButtons,
                mappings,
                context.Frame.LeftTrigger,
                context.Frame.RightTrigger);
        }

        foreach (var releasedButton in context.ReleasedButtons)
        {
            _handleButtonEvent(
                releasedButton,
                TriggerMoment.Released,
                workingActiveButtons,
                mappings,
                context.Frame.LeftTrigger,
                context.Frame.RightTrigger);

            _onButtonReleased(releasedButton);
            workingActiveButtons.Remove(releasedButton);
        }

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
