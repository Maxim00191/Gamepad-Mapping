using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Windows.Input;
using System.Windows.Threading;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

public sealed class MappingEngine : IMappingEngine
{
    private readonly IKeyboardEmulator _keyboardEmulator;
    private readonly IMouseEmulator _mouseEmulator;
    private readonly Func<bool> _canDispatchOutput;
    private readonly Action<string> _setMappedOutput;
    private readonly Action<string> _setMappingStatus;
    private readonly OutputStateTracker _outputStateTracker = new();
    private readonly AnalogProcessor _analogProcessor = new();
    private readonly InputDispatcher _inputDispatcher;
    private readonly Action<ComboHudContent?>? _setComboHud;
    private readonly HoldSessionManager _holdSessionManager;
    private readonly InputFramePipeline _inputFramePipeline;
    private readonly ButtonEventPipeline _buttonEventPipeline;

    private readonly int _comboHudDelayMs;

    private readonly TriggerMoment _buttonPressedTrigger = TriggerMoment.Pressed;
    private readonly TriggerMoment _buttonTapTrigger = TriggerMoment.Tap;

    private IReadOnlyCollection<GamepadButtons> _latestActiveButtons = Array.Empty<GamepadButtons>();
    private float _latestLeftTrigger;
    private float _latestRightTrigger;
    private IReadOnlyList<MappingEntry> _lastButtonMappingsSnapshot = Array.Empty<MappingEntry>();
    private DispatcherTimer? _comboHudDelayTimer;
    private bool _comboHudDelayConfirmed;
    private string? _pendingComboHudSignature;

    public MappingEngine(
        IKeyboardEmulator keyboardEmulator,
        IMouseEmulator mouseEmulator,
        Func<bool> canDispatchOutput,
        Action<Action> runOnUi,
        Action<string> setMappedOutput,
        Action<string> setMappingStatus,
        Action<ComboHudContent?>? setComboHud = null,
        int modifierGraceMs = HoldSessionManager.DefaultModifierGraceMs)
    {
        _comboHudDelayMs = modifierGraceMs;
        _keyboardEmulator = keyboardEmulator;
        _mouseEmulator = mouseEmulator;
        _canDispatchOutput = canDispatchOutput;
        _setMappedOutput = setMappedOutput;
        _setMappingStatus = setMappingStatus;
        _setComboHud = setComboHud;
        _inputDispatcher = new InputDispatcher(DispatchMappedOutput, runOnUi, setMappedOutput, setMappingStatus);
        _holdSessionManager = new HoldSessionManager(
            _canDispatchOutput,
            _setMappedOutput,
            _setMappingStatus,
            QueueOutputDispatch,
            SyncComboHud,
            modifierGraceMs);

        _inputFramePipeline = new InputFramePipeline(
            middlewares: new IInputFrameMiddleware[] { new InputFrameTransitionMiddleware() },
            terminal: ProcessInputFrameTerminal);

        _buttonEventPipeline = new ButtonEventPipeline(
            middlewares:
            [
                new ButtonEventPreparationMiddleware(
                    setLatestInputState: (activeButtons, leftTrigger, rightTrigger) =>
                    {
                        _latestLeftTrigger = leftTrigger;
                        _latestRightTrigger = rightTrigger;
                        _holdSessionManager.UpdateLatestInputState(activeButtons, leftTrigger, rightTrigger);
                    },
                    registerButtonPressed: _holdSessionManager.RegisterButtonPressed,
                    registerButtonReleased: _holdSessionManager.RegisterButtonReleased,
                    cancelSupersededHoldSessions: _holdSessionManager.CancelHoldSessionsSupersededByMoreSpecificChord,
                    handleHoldRelease: _holdSessionManager.HandleHoldBindingRelease,
                    forceReleaseHeldOutputsForButton: ForceReleaseHeldOutputsForButton,
                    collectReleasedOutputsHandledByMappings: CollectReleasedOutputsHandledByMappings,
                    setLatestActiveButtons: activeButtons => _latestActiveButtons = activeButtons,
                    canDispatchOutput: _canDispatchOutput,
                    setMappingStatus: _setMappingStatus)
            ],
            terminal: ProcessButtonEventTerminal);
    }

    public InputFrameProcessingResult ProcessInputFrame(InputFrame frame, IReadOnlyList<MappingEntry> mappingsSnapshot)
    {
        _lastButtonMappingsSnapshot = mappingsSnapshot;

        var context = new InputFrameContext
        {
            Frame = frame
        };

        _inputFramePipeline.Invoke(context);
        return new InputFrameProcessingResult(context.PressedButtons, context.ReleasedButtons);
    }

    private void ProcessInputFrameTerminal(InputFrameContext context)
    {
        var frame = context.Frame;

        _latestLeftTrigger = frame.LeftTrigger;
        _latestRightTrigger = frame.RightTrigger;

        var activeButtonsNow = ToActiveButtonsSet(frame.Buttons);
        _latestActiveButtons = activeButtonsNow;
        _holdSessionManager.UpdateLatestInputState(_latestActiveButtons, frame.LeftTrigger, frame.RightTrigger);

        // The first frame is treated as an initialization frame; button transitions are ignored.
        if (!context.IsFirstFrame)
        {
            var workingActiveButtons = ToActiveButtonsSet(context.PreviousButtonsMask);

            foreach (var pressedButton in context.PressedButtons)
            {
                workingActiveButtons.Add(pressedButton);

                // Keep the legacy ordering: Pressed mappings first, then Tap mappings.
                HandleButtonMappingsInternal(
                    pressedButton,
                    _buttonPressedTrigger,
                    workingActiveButtons,
                    _lastButtonMappingsSnapshot,
                    frame.LeftTrigger,
                    frame.RightTrigger);

                HandleButtonMappingsInternal(
                    pressedButton,
                    _buttonTapTrigger,
                    workingActiveButtons,
                    _lastButtonMappingsSnapshot,
                    frame.LeftTrigger,
                    frame.RightTrigger);
            }

            foreach (var releasedButton in context.ReleasedButtons)
            {
                HandleButtonMappingsInternal(
                    releasedButton,
                    TriggerMoment.Released,
                    workingActiveButtons,
                    _lastButtonMappingsSnapshot,
                    frame.LeftTrigger,
                    frame.RightTrigger);

                workingActiveButtons.Remove(releasedButton);
            }
        }

        // Continuous analog evaluation (for output transitions + relative mouse look).
        HandleThumbstickMappingsInternal(GamepadBindingType.LeftThumbstick, frame.LeftThumbstick, _lastButtonMappingsSnapshot);
        HandleThumbstickMappingsInternal(GamepadBindingType.RightThumbstick, frame.RightThumbstick, _lastButtonMappingsSnapshot);
        HandleTriggerMappingsInternal(GamepadBindingType.LeftTrigger, frame.LeftTrigger, _lastButtonMappingsSnapshot);
        HandleTriggerMappingsInternal(GamepadBindingType.RightTrigger, frame.RightTrigger, _lastButtonMappingsSnapshot);

        SyncComboHud();
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

    private void HandleButtonMappingsInternal(
        GamepadButtons buttons,
        TriggerMoment trigger,
        IReadOnlyCollection<GamepadButtons> activeButtons,
        IReadOnlyList<MappingEntry> mappingsSnapshot,
        float leftTriggerValue,
        float rightTriggerValue)
    {
        var context = new ButtonEventContext
        {
            Button = buttons,
            Trigger = trigger,
            ActiveButtons = activeButtons,
            MappingsSnapshot = mappingsSnapshot,
            LeftTriggerValue = leftTriggerValue,
            RightTriggerValue = rightTriggerValue
        };

        _buttonEventPipeline.Invoke(context);
    }

    private void ProcessButtonEventTerminal(ButtonEventContext context)
    {
        if (context.IsSuppressed)
            return;

        var matched = false;
        var suppressedByHoldDual = false;
        var candidates = new List<(MappingEntry Mapping, List<GamepadButtons> ChordButtons, bool RequiresRightTrigger, bool RequiresLeftTrigger, string SourceToken)>();

        foreach (var mapping in context.MappingsSnapshot)
        {
            if (mapping?.From is null) continue;
            if (mapping.From.Type != GamepadBindingType.Button) continue;
            if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var chordButtons, out var reqRt, out var reqLt, out var sourceToken))
                continue;
            var triggerThreshold = GetTriggerMatchThreshold(mapping);
            if (!ChordResolver.DoesChordMatchEvent(
                    chordButtons,
                    reqRt,
                    reqLt,
                    context.LeftTriggerValue,
                    context.RightTriggerValue,
                    triggerThreshold,
                    context.Button,
                    context.ActiveButtons))
                continue;
            if (mapping.Trigger != context.Trigger) continue;
            if (context.Trigger == TriggerMoment.Tap && _holdSessionManager.HasHoldSessionForSourceToken(sourceToken))
                continue;
            if (HoldSessionManager.IsHoldDualMapping(mapping))
            {
                suppressedByHoldDual = true;
                continue;
            }

            candidates.Add((mapping, chordButtons, reqRt, reqLt, sourceToken));
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var hasMoreSpecificMatch = candidates.Any(other =>
                !ReferenceEquals(other.Mapping, candidate.Mapping) &&
                ChordResolver.IsOtherChordStrictlyMoreSpecific(
                    candidate.ChordButtons,
                    candidate.RequiresRightTrigger,
                    candidate.RequiresLeftTrigger,
                    other.ChordButtons,
                    other.RequiresRightTrigger,
                    other.RequiresLeftTrigger));
            if (hasMoreSpecificMatch)
                continue;

            matched = true;

            try
            {
                if (!InputTokenResolver.TryResolveMappedOutput(candidate.Mapping.KeyboardKey, out var output, out var baseLabel))
                    continue;

                _outputStateTracker.TrackOutputHoldState(candidate.SourceToken, candidate.ChordButtons, output, context.Trigger);
                var outputLabel = $"{baseLabel} ({context.Trigger})";
                _setMappedOutput(outputLabel);
                _setMappingStatus($"Queued: {candidate.SourceToken} ({context.Trigger}) -> {outputLabel}");
                QueueOutputDispatch(candidate.SourceToken, context.Trigger, output, outputLabel, candidate.Mapping.KeyboardKey ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to send key mapping. key={candidate.Mapping.KeyboardKey}, ex={ex.Message}");
                _setMappingStatus($"Error sending '{candidate.Mapping.KeyboardKey}': {ex.Message}");
            }
        }

        var holdArmed = false;
        if (context.Trigger == TriggerMoment.Pressed)
            holdArmed = _holdSessionManager.TryArmHoldBinding(
                context.Button,
                context.ActiveButtons,
                context.MappingsSnapshot,
                context.LeftTriggerValue,
                context.RightTriggerValue);

        if (!matched && !suppressedByHoldDual && !holdArmed)
            _setMappingStatus($"No mapping for {context.ButtonName} ({context.Trigger})");
    }

    private void HandleThumbstickMappingsInternal(GamepadBindingType sourceType, Vector2 stickValue, IReadOnlyList<MappingEntry> mappingsSnapshot)
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

    private void HandleTriggerMappingsInternal(GamepadBindingType triggerBindingType, float triggerValue, IReadOnlyList<MappingEntry> mappingsSnapshot)
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

            var stateKey =
                $"Trigger|{mapping.From.Type}|{mapping.From.Value}|{InputTokenResolver.NormalizeKeyboardKeyToken(mapping.KeyboardKey ?? string.Empty)}|{mapping.Trigger}";
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
                            SendPointerAction(pointerAction, TriggerMoment.Tap);
                        else if (mapping.Trigger != TriggerMoment.Released)
                            SendPointerAction(pointerAction, TriggerMoment.Pressed);
                    }
                    else
                    {
                        if (mapping.Trigger == TriggerMoment.Released)
                            SendPointerAction(pointerAction, TriggerMoment.Tap);
                        else if (mapping.Trigger != TriggerMoment.Tap)
                            SendPointerAction(pointerAction, TriggerMoment.Released);
                    }
                }

                var moment = transition.IsActive ? TriggerMoment.Pressed : TriggerMoment.Released;
                _setMappedOutput($"{baseLabel} ({moment})");
                _setMappingStatus($"Trigger {mapping.From.Type}: {baseLabel} ({moment})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed trigger mapping. key={mapping.KeyboardKey}, ex={ex.Message}");
                _setMappingStatus($"Error sending '{mapping.KeyboardKey}': {ex.Message}");
            }
        }
    }

    public void ForceReleaseAllOutputs()
    {
        _latestActiveButtons = Array.Empty<GamepadButtons>();
        _holdSessionManager.ClearButtonDownTicks();
        _holdSessionManager.ClearAllHoldSessions();
        _outputStateTracker.ForceReleaseAllOutputs(ForceReleaseOutput);
    }

    public void ForceReleaseAnalogOutputs()
    {
        foreach (var active in _analogProcessor.GetActiveNonTapOutputs())
        {
            _keyboardEmulator.KeyUp(active.Key);
        }
        _analogProcessor.Reset();
    }

    public void Dispose()
    {
        try
        {
            if (_comboHudDelayTimer is not null)
            {
                _comboHudDelayTimer.Stop();
                _comboHudDelayTimer.Tick -= OnComboHudDelayTimerTick;
                _comboHudDelayTimer = null;
            }
            ForceReleaseAllOutputs();
            ForceReleaseAnalogOutputs();
            _inputDispatcher.Dispose();
        }
        catch
        {
            // Best-effort shutdown.
        }
    }

    public static Key ParseKey(string? keyboardKey) => InputTokenResolver.ParseKey(keyboardKey);

    public static string NormalizeKeyboardKeyToken(string keyboardKey) => InputTokenResolver.NormalizeKeyboardKeyToken(keyboardKey);

    public static bool IsMouseLookOutput(string token) => AnalogProcessor.IsMouseLookOutput(token);

    private void SyncComboHud()
    {
        if (_setComboHud is null)
            return;

        if (!TryGetComboHudSignature(out var signature))
        {
            CancelComboHudDelayTimer();
            _comboHudDelayConfirmed = false;
            _pendingComboHudSignature = null;
            _setComboHud(null);
            return;
        }

        if (!string.Equals(signature, _pendingComboHudSignature, StringComparison.Ordinal))
        {
            _pendingComboHudSignature = signature;
            _comboHudDelayConfirmed = false;
            CancelComboHudDelayTimer();
        }

        if (!_comboHudDelayConfirmed)
        {
            ScheduleComboHudDelay();
            return;
        }

        CancelComboHudDelayTimer();
        PresentComboHudForCurrentSignature(signature);
    }

    private bool TryGetComboHudSignature(out string signature)
    {
        if (_holdSessionManager.TryGetFirstHoldSession(out var holdSession) && holdSession is not null)
        {
            signature = $"hold|{holdSession.SourceToken}";
            return true;
        }

        var prefix = ComboHudBuilder.BuildModifierPrefixHud(_canDispatchOutput, _latestActiveButtons, _lastButtonMappingsSnapshot);
        if (prefix is null)
        {
            signature = string.Empty;
            return false;
        }

        var thumbprint = string.Join(
            ',',
            _latestActiveButtons.OrderBy(b => b.ToString(), StringComparer.OrdinalIgnoreCase));
        signature = $"prefix|{thumbprint}";
        return true;
    }

    private void PresentComboHudForCurrentSignature(string signature)
    {
        if (_setComboHud is null)
            return;

        if (signature.StartsWith("hold|", StringComparison.Ordinal) &&
            _holdSessionManager.TryGetFirstHoldSession(out var holdSession) &&
            holdSession is not null)
        {
            _setComboHud(ComboHudBuilder.BuildComboHud(holdSession, _lastButtonMappingsSnapshot));
            return;
        }

        var prefix = ComboHudBuilder.BuildModifierPrefixHud(_canDispatchOutput, _latestActiveButtons, _lastButtonMappingsSnapshot);
        if (prefix is not null)
            _setComboHud(prefix);
        else
            _setComboHud(null);
    }

    private void EnsureComboHudDelayTimer()
    {
        if (_comboHudDelayTimer is not null)
            return;

        _comboHudDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_comboHudDelayMs) };
        _comboHudDelayTimer.Tick += OnComboHudDelayTimerTick;
    }

    private void OnComboHudDelayTimerTick(object? sender, EventArgs e)
    {
        _comboHudDelayTimer?.Stop();
        if (_setComboHud is null)
            return;

        if (!TryGetComboHudSignature(out var signature))
        {
            _comboHudDelayConfirmed = false;
            _pendingComboHudSignature = null;
            _setComboHud(null);
            return;
        }

        _comboHudDelayConfirmed = true;
        PresentComboHudForCurrentSignature(signature);
    }

    private void ScheduleComboHudDelay()
    {
        EnsureComboHudDelayTimer();
        _comboHudDelayTimer!.Stop();
        _comboHudDelayTimer.Start();
    }

    private void CancelComboHudDelayTimer()
    {
        _comboHudDelayTimer?.Stop();
    }

    private HashSet<DispatchedOutput> CollectReleasedOutputsHandledByMappings(
        GamepadButtons changedButton,
        IReadOnlyCollection<GamepadButtons> activeButtons,
        IReadOnlyCollection<MappingEntry> snapshot,
        float leftTriggerValue,
        float rightTriggerValue)
    {
        var handledOutputs = new HashSet<DispatchedOutput>();
        foreach (var mapping in snapshot)
        {
            if (mapping?.From is null || mapping.From.Type != GamepadBindingType.Button)
                continue;
            if (mapping.Trigger != TriggerMoment.Released)
                continue;
            if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var chordButtons, out var reqRt, out var reqLt, out _))
                continue;
            var triggerThreshold = GetTriggerMatchThreshold(mapping);
            if (!ChordResolver.DoesChordMatchEvent(chordButtons, reqRt, reqLt, leftTriggerValue, rightTriggerValue, triggerThreshold, changedButton, activeButtons))
                continue;
            if (!InputTokenResolver.TryResolveMappedOutput(mapping.KeyboardKey, out var output, out _))
                continue;

            handledOutputs.Add(output);
        }

        return handledOutputs;
    }

    private static float GetTriggerMatchThreshold(MappingEntry mapping) =>
        mapping.AnalogThreshold is > 0 and <= 1 ? mapping.AnalogThreshold.Value : 0.35f;

    private void DispatchMappedOutput(DispatchedOutput output, TriggerMoment trigger)
    {
        if (output.KeyboardKey is Key key && key != Key.None)
        {
            if (trigger == TriggerMoment.Pressed)
                _keyboardEmulator.KeyDown(key);
            else if (trigger == TriggerMoment.Released)
                _keyboardEmulator.KeyUp(key);
            else
                _keyboardEmulator.TapKey(key);
            return;
        }

        if (output.PointerAction is PointerAction pointerAction)
            SendPointerAction(pointerAction, trigger);
    }

    private void ForceReleaseHeldOutputsForButton(GamepadButtons button, IReadOnlySet<DispatchedOutput>? outputsHandledByReleasedMappings = null)
    {
        _outputStateTracker.ForceReleaseHeldOutputsForButton(button, ForceReleaseOutput, outputsHandledByReleasedMappings);
    }

    private void ForceReleaseOutput(DispatchedOutput output)
    {
        QueueOutputDispatch("ForceRelease", TriggerMoment.Released, output, "Forced release", "forced-release");
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

    private void SendPointerAction(PointerAction action, TriggerMoment trigger)
    {
        switch (action)
        {
            case PointerAction.LeftClick:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.LeftDown();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.LeftUp();
                else _mouseEmulator.LeftClick();
                break;
            case PointerAction.RightClick:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.RightDown();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.RightUp();
                else _mouseEmulator.RightClick();
                break;
            case PointerAction.MiddleClick:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.MiddleDown();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.MiddleUp();
                else _mouseEmulator.MiddleClick();
                break;
            case PointerAction.X1Click:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.X1Down();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.X1Up();
                else _mouseEmulator.X1Click();
                break;
            case PointerAction.X2Click:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.X2Down();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.X2Up();
                else _mouseEmulator.X2Click();
                break;
            case PointerAction.WheelUp:
                if (trigger != TriggerMoment.Released) _mouseEmulator.WheelUp();
                break;
            case PointerAction.WheelDown:
                if (trigger != TriggerMoment.Released) _mouseEmulator.WheelDown();
                break;
        }
    }

    private void QueueOutputDispatch(
        string buttonName,
        TriggerMoment trigger,
        DispatchedOutput output,
        string outputLabel,
        string sourceToken)
    {
        _inputDispatcher.Enqueue(buttonName, trigger, output, outputLabel, sourceToken);
    }
}
