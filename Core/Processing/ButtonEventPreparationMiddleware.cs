using System;
using System.Collections.Generic;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

internal sealed class ButtonEventPreparationMiddleware : IButtonEventMiddleware
{
    private readonly Action<IReadOnlyCollection<GamepadButtons>, float, float> _setLatestInputState;
    private readonly Action<GamepadButtons> _registerButtonPressed;
    private readonly Action<GamepadButtons> _registerButtonReleased;
    private readonly Action<GamepadButtons, IReadOnlyCollection<GamepadButtons>, IReadOnlyList<MappingEntry>, float, float> _cancelSupersededHoldSessions;
    private readonly Action<GamepadButtons, IReadOnlyCollection<GamepadButtons>, float, float, long?> _handleHoldRelease;
    private readonly Func<GamepadButtons, long?> _getReleasedButtonHeldMs;
    private readonly Action<GamepadButtons, IReadOnlySet<DispatchedOutput>?> _forceReleaseHeldOutputsForButton;
    private readonly Func<GamepadButtons, IReadOnlyCollection<GamepadButtons>, IReadOnlyCollection<MappingEntry>, float, float, long?, HashSet<DispatchedOutput>> _collectReleasedOutputsHandledByMappings;
    private readonly Action<IReadOnlyCollection<GamepadButtons>> _setLatestActiveButtons;
    private readonly Func<bool> _canDispatchOutput;
    private readonly Action<string> _setMappingStatus;

    public ButtonEventPreparationMiddleware(
        Action<IReadOnlyCollection<GamepadButtons>, float, float> setLatestInputState,
        Action<GamepadButtons> registerButtonPressed,
        Action<GamepadButtons> registerButtonReleased,
        Action<GamepadButtons, IReadOnlyCollection<GamepadButtons>, IReadOnlyList<MappingEntry>, float, float> cancelSupersededHoldSessions,
        Action<GamepadButtons, IReadOnlyCollection<GamepadButtons>, float, float, long?> handleHoldRelease,
        Func<GamepadButtons, long?> getReleasedButtonHeldMs,
        Action<GamepadButtons, IReadOnlySet<DispatchedOutput>?> forceReleaseHeldOutputsForButton,
        Func<GamepadButtons, IReadOnlyCollection<GamepadButtons>, IReadOnlyCollection<MappingEntry>, float, float, long?, HashSet<DispatchedOutput>> collectReleasedOutputsHandledByMappings,
        Action<IReadOnlyCollection<GamepadButtons>> setLatestActiveButtons,
        Func<bool> canDispatchOutput,
        Action<string> setMappingStatus)
    {
        _setLatestInputState = setLatestInputState;
        _registerButtonPressed = registerButtonPressed;
        _registerButtonReleased = registerButtonReleased;
        _cancelSupersededHoldSessions = cancelSupersededHoldSessions;
        _handleHoldRelease = handleHoldRelease;
        _getReleasedButtonHeldMs = getReleasedButtonHeldMs;
        _forceReleaseHeldOutputsForButton = forceReleaseHeldOutputsForButton;
        _collectReleasedOutputsHandledByMappings = collectReleasedOutputsHandledByMappings;
        _setLatestActiveButtons = setLatestActiveButtons;
        _canDispatchOutput = canDispatchOutput;
        _setMappingStatus = setMappingStatus;
    }

    public void Invoke(ButtonEventContext context, Action<ButtonEventContext> next)
    {
        IReadOnlyCollection<GamepadButtons> effectiveActiveButtons;
        if (context.Trigger == TriggerMoment.Released)
        {
            var postRelease = new HashSet<GamepadButtons>(context.ActiveButtons);
            postRelease.Remove(context.Button);
            _setLatestActiveButtons(postRelease);
            effectiveActiveButtons = postRelease;
            context.ReleasedButtonHeldMs = _getReleasedButtonHeldMs(context.Button);
            context.ReleasedOutputsHandledByMappings = _collectReleasedOutputsHandledByMappings(
                context.Button,
                context.ActiveButtons,
                context.MappingsSnapshot,
                context.LeftTriggerValue,
                context.RightTriggerValue,
                context.ReleasedButtonHeldMs);
            _handleHoldRelease(context.Button, context.ActiveButtons, context.LeftTriggerValue, context.RightTriggerValue, context.ReleasedButtonHeldMs);
            _registerButtonReleased(context.Button);
            _forceReleaseHeldOutputsForButton(context.Button, context.ReleasedOutputsHandledByMappings);
        }
        else
        {
            _setLatestActiveButtons(context.ActiveButtons);
            effectiveActiveButtons = context.ActiveButtons;
        }

        _setLatestInputState(effectiveActiveButtons, context.LeftTriggerValue, context.RightTriggerValue);

        if (context.Trigger == TriggerMoment.Pressed)
        {
            _cancelSupersededHoldSessions(
                context.Button,
                context.ActiveButtons,
                context.MappingsSnapshot,
                context.LeftTriggerValue,
                context.RightTriggerValue);
            _registerButtonPressed(context.Button);
        }

        if (context.Trigger != TriggerMoment.Released && !_canDispatchOutput())
        {
            context.IsSuppressed = true;
            _setMappingStatus($"Suppressed ({context.Button}, {context.Trigger}) - target is not foreground");
            return;
        }

        next(context);
    }
}
