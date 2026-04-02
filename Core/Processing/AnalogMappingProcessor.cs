using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;

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

    public void ProcessThumbstick(GamepadBindingType sourceType, Vector2 stickValue, IReadOnlyList<MappingEntry> mappingsSnapshot)
    {
        if (!_canDispatchOutput())
        {
            ForceReleaseAnalogOutputs();
            return;
        }

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

            var key = InputTokenResolver.ParseKey(outputToken);
            if (key == Key.None)
                continue;

            HandleAnalogKeyboardOutput(mapping, source, stickValue, key);
        }

        if (MathF.Abs(mouseDeltaX) > 0f || MathF.Abs(mouseDeltaY) > 0f)
            SendMouseLookDelta(mouseDeltaX, mouseDeltaY);
    }

    public void ProcessTrigger(GamepadBindingType triggerBindingType, float triggerValue, IReadOnlyList<MappingEntry> mappingsSnapshot, Action<PointerAction, TriggerMoment> sendPointerAction)
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

            var stateKey = $"Trigger|{mapping.From.Type}|{mapping.From.Value}|{InputTokenResolver.NormalizeKeyboardKeyToken(mapping.KeyboardKey ?? string.Empty)}|{mapping.Trigger}";
            var transition = _analogProcessor.EvaluateTriggerTransition(mapping, triggerValue, stateKey);
            if (!transition.HasChanged)
                continue;

            try
            {
                if (output.KeyboardKey is Key key && key != Key.None)
                {
                    if (transition.IsActive)
                    {
                        if (mapping.Trigger == TriggerMoment.Tap)
                            _keyboardEmulator.TapKey(key);
                        else if (mapping.Trigger != TriggerMoment.Released)
                            _keyboardEmulator.KeyDown(key);
                    }
                    else
                    {
                        if (mapping.Trigger == TriggerMoment.Released)
                            _keyboardEmulator.TapKey(key);
                        else if (mapping.Trigger != TriggerMoment.Tap)
                            _keyboardEmulator.KeyUp(key);
                    }
                }
                else if (output.PointerAction is PointerAction pointerAction)
                {
                    if (transition.IsActive)
                    {
                        if (mapping.Trigger == TriggerMoment.Tap)
                            sendPointerAction(pointerAction, TriggerMoment.Tap);
                        else if (mapping.Trigger != TriggerMoment.Released)
                            sendPointerAction(pointerAction, TriggerMoment.Pressed);
                    }
                    else
                    {
                        if (mapping.Trigger == TriggerMoment.Released)
                            sendPointerAction(pointerAction, TriggerMoment.Tap);
                        else if (mapping.Trigger != TriggerMoment.Tap)
                            sendPointerAction(pointerAction, TriggerMoment.Released);
                    }
                }

                var moment = transition.IsActive ? TriggerMoment.Pressed : TriggerMoment.Released;
                _setMappedOutput($"{baseLabel} ({moment})");
                _setMappingStatus($"Trigger {mapping.From.Type}: {baseLabel} ({moment})");
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

    private void HandleAnalogKeyboardOutput(MappingEntry mapping, AnalogSourceDefinition source, Vector2 stickValue, Key key)
    {
        var transition = _analogProcessor.EvaluateKeyboardTransition(mapping, source, stickValue, key);
        if (!transition.HasChanged)
            return;

        if (transition.IsActive)
        {
            if (mapping.Trigger == TriggerMoment.Tap)
                _keyboardEmulator.TapKey(key);
            else if (mapping.Trigger != TriggerMoment.Released)
                _keyboardEmulator.KeyDown(key);
        }
        else
        {
            if (mapping.Trigger == TriggerMoment.Released)
                _keyboardEmulator.TapKey(key);
            else if (mapping.Trigger != TriggerMoment.Tap)
                _keyboardEmulator.KeyUp(key);
        }
    }

    private void SendMouseLookDelta(float deltaX, float deltaY)
    {
        var delta = _analogProcessor.AccumulateMouseLookDelta(deltaX, deltaY);
        if (delta.PixelDx != 0 || delta.PixelDy != 0)
            _mouseEmulator.MoveBy(delta.PixelDx, delta.PixelDy);
    }
}
