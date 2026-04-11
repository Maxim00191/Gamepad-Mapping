using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Core.Input;

namespace GamepadMapperGUI.Core;

internal sealed class AnalogMappingProcessor
{
    private readonly AnalogProcessor _analogProcessor;
    private readonly IKeyboardEmulator _keyboardEmulator;
    private readonly IMouseEmulator _mouseEmulator;
    private readonly Func<bool> _canDispatchOutput;
    private readonly Action<string> _setMappedOutput;
    private readonly Action<string> _setMappingStatus;

    public AnalogMappingProcessor(
        AnalogProcessor analogProcessor,
        IKeyboardEmulator keyboardEmulator,
        IMouseEmulator mouseEmulator,
        Func<bool> canDispatchOutput,
        Action<string> setMappedOutput,
        Action<string> setMappingStatus)
    {
        _analogProcessor = analogProcessor;
        _keyboardEmulator = keyboardEmulator;
        _mouseEmulator = mouseEmulator;
        _canDispatchOutput = canDispatchOutput;
        _setMappedOutput = setMappedOutput;
        _setMappingStatus = setMappingStatus;
    }

    public void ProcessThumbstick(GamepadBindingType sourceType, Vector2 stickValue, IReadOnlyList<MappingEntry> mappingsSnapshot, bool isConsumed = false)
    {
        if (!_canDispatchOutput())
        {
            ForceReleaseAnalogOutputs();
            return;
        }

        if (isConsumed)
        {
            ReleaseAnalogOutputsForSourceThumbstick(sourceType);
            return;
        }

        var stickMagnitude = stickValue.Length();
        var mouseDeltaX = 0f;
        var mouseDeltaY = 0f;

        foreach (var mapping in mappingsSnapshot)
        {
            if (mapping?.From is null || mapping.From.Type != sourceType)
                continue;

            if (!AnalogProcessor.TryParseAnalogSource(mapping.From.Value, out var source))
                continue;

            var outputToken = InputTokenResolver.NormalizeKeyboardKeyToken(mapping.KeyboardKey ?? string.Empty);
            if (string.IsNullOrWhiteSpace(outputToken))
                continue;

            if (AnalogProcessor.TryResolveMouseLookOutput(outputToken, out var isVerticalLook))
            {
                var axisValue = AnalogProcessor.ResolveStickAxisValue(stickValue, source);
                if (MathF.Abs(axisValue) < 0.01f)
                    continue;

                var delta = axisValue * AnalogProcessor.DefaultLookSensitivity;
                if (isVerticalLook)
                    mouseDeltaY += -delta;
                else
                    mouseDeltaX += delta;
                continue;
            }

            if (!InputTokenResolver.TryResolveMappedOutput(outputToken, out var output, out var baseLabel))
                continue;

            HandleAnalogOutput(mapping, source, stickValue, output, baseLabel);
        }

        bool hasMouseMovement = MathF.Abs(mouseDeltaX) > 0f || MathF.Abs(mouseDeltaY) > 0f;

        if (hasMouseMovement)
        {
            SendMouseLookDelta(sourceType, mouseDeltaX, mouseDeltaY, stickMagnitude); 
        }
        else if (stickMagnitude > 0.01f)
        {
            _mouseEmulator.MoveBy(0, 0, stickMagnitude);
        }
    }

    public void ProcessTrigger(GamepadBindingType triggerBindingType, float triggerValue, IReadOnlyList<MappingEntry> mappingsSnapshot, Action<string, TriggerMoment, DispatchedOutput, string, string> queueOutputDispatch)
    {
        if (!_canDispatchOutput())
        {
            ForceReleaseAnalogOutputs();
            return;
        }

        foreach (var mapping in mappingsSnapshot)
        {
            if (mapping?.From is null || mapping.From.Type != triggerBindingType)
                continue;

            if (!InputTokenResolver.TryResolveMappedOutput(mapping.KeyboardKey, out var output, out var baseLabel))
                continue;

            var transition = _analogProcessor.EvaluateTriggerTransition(mapping, triggerValue);
            if (!transition.HasChanged)
                continue;

            try
            {
                var command = TranslateToCommand(output, transition.IsActive ? TriggerMoment.Pressed : TriggerMoment.Released);
                if (command.Type != OutputCommandType.None)
                {
                    if (command.Type is OutputCommandType.KeyPress or OutputCommandType.KeyRelease or OutputCommandType.KeyTap)
                    {
                        if (transition.IsActive)
                        {
                            if (mapping.Trigger == TriggerMoment.Tap)
                                _keyboardEmulator.TapKey(command.Key);
                            else if (mapping.Trigger != TriggerMoment.Released)
                                _keyboardEmulator.KeyDown(command.Key);
                        }
                        else
                        {
                            if (mapping.Trigger == TriggerMoment.Released)
                                _keyboardEmulator.TapKey(command.Key);
                            else if (mapping.Trigger != TriggerMoment.Tap)
                                _keyboardEmulator.KeyUp(command.Key);
                        }
                    }
                    else
                    {
                        // For pointer actions on triggers, we still use the dispatcher callback
                        // to ensure they are queued and don't block the input loop.
                        var moment = transition.IsActive ? TriggerMoment.Pressed : TriggerMoment.Released;
                        if (mapping.Trigger == TriggerMoment.Tap) moment = TriggerMoment.Tap;
                        else if (mapping.Trigger == TriggerMoment.Released && !transition.IsActive) moment = TriggerMoment.Tap;

                        queueOutputDispatch(
                            triggerBindingType.ToString(),
                            moment,
                            output,
                            $"{baseLabel} ({moment})",
                            mapping.KeyboardKey ?? "trigger-input");
                    }
                }

                var finalMoment = transition.IsActive ? TriggerMoment.Pressed : TriggerMoment.Released;
                _setMappedOutput($"{baseLabel} ({finalMoment})");
                _setMappingStatus($"Trigger {mapping.From.Type}: {baseLabel} ({finalMoment})");
            }
            catch (Exception ex)
            {
                _setMappingStatus($"Error sending '{mapping.KeyboardKey}': {ex.Message}");
            }
        }
    }

    public void ForceReleaseAnalogOutputs()
    {
        foreach (var active in _analogProcessor.GetActiveNonTapOutputs())
        {
            _keyboardEmulator.KeyUp(active.Key);
        }
        _analogProcessor.Reset();
    }

    private void ReleaseAnalogOutputsForSourceThumbstick(GamepadBindingType sourceType)
    {
        foreach (var (key, _) in _analogProcessor.GetActiveNonTapOutputsForBinding(sourceType))
            _keyboardEmulator.KeyUp(key);

        _analogProcessor.RemoveAnalogKeyboardStateForBinding(sourceType);
    }

    private void HandleAnalogOutput(MappingEntry mapping, AnalogSourceDefinition source, Vector2 stickValue, DispatchedOutput output, string baseLabel)
    {
        var transition = _analogProcessor.EvaluateKeyboardTransition(mapping, source, stickValue, output.KeyboardKey ?? Key.None);
        if (!transition.HasChanged)
            return;

        var moment = transition.IsActive ? TriggerMoment.Pressed : TriggerMoment.Released;
        var command = TranslateToCommand(output, moment);
        if (command.Type == OutputCommandType.None) return;

        if (command.Type is OutputCommandType.KeyPress or OutputCommandType.KeyRelease or OutputCommandType.KeyTap)
        {
            if (transition.IsActive)
            {
                if (mapping.Trigger == TriggerMoment.Tap)
                    _keyboardEmulator.TapKey(command.Key);
                else if (mapping.Trigger != TriggerMoment.Released)
                    _keyboardEmulator.KeyDown(command.Key);
            }
            else
            {
                if (mapping.Trigger == TriggerMoment.Released)
                    _keyboardEmulator.TapKey(command.Key);
                else if (mapping.Trigger != TriggerMoment.Tap)
                    _keyboardEmulator.KeyUp(command.Key);
            }
        }
    }

    private static OutputCommand TranslateToCommand(DispatchedOutput output, TriggerMoment trigger)
    {
        if (output.KeyboardKey is { } key && key != Key.None)
        {
            var type = trigger switch
            {
                TriggerMoment.Pressed => OutputCommandType.KeyPress,
                TriggerMoment.Released => OutputCommandType.KeyRelease,
                _ => OutputCommandType.KeyTap
            };
            return new OutputCommand(type, Key: key);
        }

        if (output.PointerAction is { } action && action != PointerAction.None)
        {
            var type = action switch
            {
                PointerAction.WheelUp or PointerAction.WheelDown => OutputCommandType.PointerWheel,
                _ => trigger switch
                {
                    TriggerMoment.Pressed => OutputCommandType.PointerDown,
                    TriggerMoment.Released => OutputCommandType.PointerUp,
                    _ => OutputCommandType.PointerClick
                }
            };
            return new OutputCommand(type, PointerAction: action);
        }

        return default;
    }

    private void SendMouseLookDelta(GamepadBindingType thumbstickSource, float deltaX, float deltaY, float stickMagnitude)
    {
        var delta = _analogProcessor.AccumulateMouseLookDelta(thumbstickSource, deltaX, deltaY);
        _mouseEmulator.MoveBy(delta.PixelDx, delta.PixelDy, stickMagnitude);
    }
}

