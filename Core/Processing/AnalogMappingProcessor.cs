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
using GamepadMapperGUI.Core.Emulation.Noise;

namespace GamepadMapperGUI.Core;

internal sealed class AnalogMappingProcessor
{
    private struct MouseLookPipeline
    {
        public float FilterX;
        public float FilterY;
        public float LastRawX;
        public float LastRawY;
        public bool WasAboveSettle;
        public sbyte PreReleaseSignX;
        public sbyte PreReleaseSignY;
        public int ReboundFramesRemaining;
    }

    private MouseLookPipeline _mouseLookLeft;
    private MouseLookPipeline _mouseLookRight;

    private readonly AnalogProcessor _analogProcessor;
    private readonly IKeyboardEmulator _keyboardEmulator;
    private readonly IMouseEmulator _mouseEmulator;
    private readonly Func<bool> _canDispatchOutput;
    private readonly Action<string> _setMappedOutput;
    private readonly Action<string> _setMappingStatus;
    private readonly Func<float> _getMouseLookSensitivity;
    private readonly Func<float> _getMouseLookSmoothing;
    private readonly Func<float> _getMouseLookSettleMagnitude;
    private readonly Func<float> _getMouseLookReboundSuppression;
    private readonly Func<int> _getGamepadPollingIntervalMs;
    private readonly Func<float> _getAnalogChangeEpsilon;
    private readonly Func<int> _getKeyboardTapHoldDurationMs;

    public AnalogMappingProcessor(
        AnalogProcessor analogProcessor,
        IKeyboardEmulator keyboardEmulator,
        IMouseEmulator mouseEmulator,
        Func<bool> canDispatchOutput,
        Action<string> setMappedOutput,
        Action<string> setMappingStatus,
        Func<float>? getMouseLookSensitivity = null,
        Func<float>? getMouseLookSmoothing = null,
        Func<float>? getMouseLookSettleMagnitude = null,
        Func<float>? getMouseLookReboundSuppression = null,
        Func<int>? getGamepadPollingIntervalMs = null,
        Func<float>? getAnalogChangeEpsilon = null,
        Func<int>? getKeyboardTapHoldDurationMs = null)
    {
        _analogProcessor = analogProcessor;
        _keyboardEmulator = keyboardEmulator;
        _mouseEmulator = mouseEmulator;
        _canDispatchOutput = canDispatchOutput;
        _setMappedOutput = setMappedOutput;
        _setMappingStatus = setMappingStatus;
        _getMouseLookSensitivity = getMouseLookSensitivity ?? (() => AnalogProcessor.LegacyDefaultMouseLookSensitivity);
        _getMouseLookSmoothing = getMouseLookSmoothing ?? (() => 0f);
        _getMouseLookSettleMagnitude = getMouseLookSettleMagnitude ?? (() => 0.02f);
        _getMouseLookReboundSuppression = getMouseLookReboundSuppression ?? (() => 0f);
        _getGamepadPollingIntervalMs = getGamepadPollingIntervalMs ?? (() => 10);
        _getAnalogChangeEpsilon = getAnalogChangeEpsilon ?? (() => 0.01f);
        _getKeyboardTapHoldDurationMs = getKeyboardTapHoldDurationMs ?? (() => 70);
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
        var settleMag = MathF.Max(_getAnalogChangeEpsilon(), _getMouseLookSettleMagnitude());
        var inSettle = stickMagnitude <= settleMag;

        ref var pipe = ref GetMouseLookPipelineRef(sourceType);

        if (inSettle)
        {
            if (pipe.WasAboveSettle)
            {
                var rebound = Math.Clamp(_getMouseLookReboundSuppression(), 0f, 1f);
                if (rebound > 0f)
                {
                    pipe.PreReleaseSignX = ToSign(pipe.LastRawX);
                    pipe.PreReleaseSignY = ToSign(pipe.LastRawY);
                    var pollMs = GamepadInputStreamConstraints.ClampPollingIntervalMs(_getGamepadPollingIntervalMs());
                    var frames = (int)MathF.Ceiling((float)MouseLookMotionConstraints.ReboundSuppressionCalibrationWindowMs / pollMs);
                    pipe.ReboundFramesRemaining = Math.Max(2, frames);
                }
            }

            pipe.FilterX = 0f;
            pipe.FilterY = 0f;
            pipe.LastRawX = 0f;
            pipe.LastRawY = 0f;
            pipe.WasAboveSettle = false;
            _analogProcessor.ClearMouseLookResidual(sourceType);
            ResetMouseSubdivisionIfApplicable(sourceType);
        }

        var mouseDeltaX = 0f;
        var mouseDeltaY = 0f;
        var epsilon = _getAnalogChangeEpsilon();
        var sensitivity = _getMouseLookSensitivity();
        // Tremor-only MoveBy uses stick magnitude only when this thumbstick maps mouse look (each stick scored separately).
        var noiseMagnitude = ThumbstickHasMouseLookMapping(sourceType, mappingsSnapshot) ? stickMagnitude : 0f;

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
                if (MathF.Abs(axisValue) < epsilon)
                    continue;

                var delta = axisValue * sensitivity;
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

        if (!inSettle)
        {
            pipe.WasAboveSettle = true;
            pipe.LastRawX = mouseDeltaX;
            pipe.LastRawY = mouseDeltaY;

            var rebound = Math.Clamp(_getMouseLookReboundSuppression(), 0f, 1f);
            if (rebound > 0f && pipe.ReboundFramesRemaining > 0)
            {
                ApplyReboundAttenuation(ref mouseDeltaX, ref mouseDeltaY, pipe.PreReleaseSignX, pipe.PreReleaseSignY, rebound);
                pipe.ReboundFramesRemaining--;
            }

            var smoothing = Math.Clamp(_getMouseLookSmoothing(), 0f, 1f);
            float sendX;
            float sendY;
            if (smoothing <= 0f)
            {
                sendX = mouseDeltaX;
                sendY = mouseDeltaY;
            }
            else
            {
                var pollMs = GamepadInputStreamConstraints.ClampPollingIntervalMs(_getGamepadPollingIntervalMs());
                var dtSec = pollMs / 1000f;
                var sm = smoothing * smoothing;
                var tauSec = 0.004f + (0.10f - 0.004f) * sm;
                var alpha = tauSec > 1e-6f ? 1f - MathF.Exp(-dtSec / tauSec) : 1f;
                pipe.FilterX += alpha * (mouseDeltaX - pipe.FilterX);
                pipe.FilterY += alpha * (mouseDeltaY - pipe.FilterY);
                sendX = pipe.FilterX;
                sendY = pipe.FilterY;
            }

            if (MathF.Abs(sendX) > 0f || MathF.Abs(sendY) > 0f)
                SendMouseLookDelta(sourceType, sendX, sendY, stickMagnitude);
            else if (noiseMagnitude > epsilon)
                _mouseEmulator.MoveBy(0, 0, noiseMagnitude);
        }
        else if (noiseMagnitude > epsilon)
        {
            _mouseEmulator.MoveBy(0, 0, noiseMagnitude);
        }
    }

    private static bool ThumbstickHasMouseLookMapping(GamepadBindingType sourceType, IReadOnlyList<MappingEntry> mappingsSnapshot)
    {
        foreach (var mapping in mappingsSnapshot)
        {
            if (mapping?.From is null || mapping.From.Type != sourceType)
                continue;

            if (!AnalogProcessor.TryParseAnalogSource(mapping.From.Value, out _))
                continue;

            var outputToken = InputTokenResolver.NormalizeKeyboardKeyToken(mapping.KeyboardKey ?? string.Empty);
            if (string.IsNullOrWhiteSpace(outputToken))
                continue;

            if (AnalogProcessor.TryResolveMouseLookOutput(outputToken, out _))
                return true;
        }

        return false;
    }

    private static sbyte ToSign(float v)
    {
        if (v > 1e-6f) return 1;
        if (v < -1e-6f) return -1;
        return 0;
    }

    private static void ApplyReboundAttenuation(ref float dx, ref float dy, sbyte preX, sbyte preY, float rebound)
    {
        var sx = ToSign(dx);
        var sy = ToSign(dy);
        var factor = 1f - rebound;
        if (preX != 0 && sx != 0 && sx != preX)
            dx *= factor;
        if (preY != 0 && sy != 0 && sy != preY)
            dy *= factor;
    }

    private ref MouseLookPipeline GetMouseLookPipelineRef(GamepadBindingType sourceType) =>
        ref sourceType == GamepadBindingType.LeftThumbstick ? ref _mouseLookLeft : ref _mouseLookRight;

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
                                _keyboardEmulator.TapKey(command.Key, keyHoldMs: _getKeyboardTapHoldDurationMs());
                            else if (mapping.Trigger != TriggerMoment.Released)
                                _keyboardEmulator.KeyDown(command.Key);
                        }
                        else
                        {
                            if (mapping.Trigger == TriggerMoment.Released)
                                _keyboardEmulator.TapKey(command.Key, keyHoldMs: _getKeyboardTapHoldDurationMs());
                            else if (mapping.Trigger != TriggerMoment.Tap)
                                _keyboardEmulator.KeyUp(command.Key);
                        }
                    }
                    else
                    {
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
        _mouseLookLeft = default;
        _mouseLookRight = default;
        ResetMouseSubdivisionIfApplicable(null);
    }

    private void ReleaseAnalogOutputsForSourceThumbstick(GamepadBindingType sourceType)
    {
        foreach (var (key, _) in _analogProcessor.GetActiveNonTapOutputsForBinding(sourceType))
            _keyboardEmulator.KeyUp(key);

        _analogProcessor.RemoveAnalogKeyboardStateForBinding(sourceType);
        ref var pipe = ref GetMouseLookPipelineRef(sourceType);
        pipe = default;
        ResetMouseSubdivisionIfApplicable(sourceType);
    }

    private void ResetMouseSubdivisionIfApplicable(GamepadBindingType? thumbstickScope)
    {
        if (_mouseEmulator is IPendingMouseSubdivisionState s)
            s.ClearPendingSubdivision(thumbstickScope);
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
                    _keyboardEmulator.TapKey(command.Key, keyHoldMs: _getKeyboardTapHoldDurationMs());
                else if (mapping.Trigger != TriggerMoment.Released)
                    _keyboardEmulator.KeyDown(command.Key);
            }
            else
            {
                if (mapping.Trigger == TriggerMoment.Released)
                    _keyboardEmulator.TapKey(command.Key, keyHoldMs: _getKeyboardTapHoldDurationMs());
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
        _mouseEmulator.MoveBy(delta.PixelDx, delta.PixelDy, stickMagnitude, thumbstickSource);
    }
}
