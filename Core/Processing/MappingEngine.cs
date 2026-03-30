using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

public sealed class MappingEngine : IMappingEngine
{
    private static readonly Dictionary<string, string> KeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Spacebar"] = nameof(Key.Space),
        ["Return"] = nameof(Key.Enter),
        ["Esc"] = nameof(Key.Escape),
        ["LeftControl"] = nameof(Key.LeftCtrl),
        ["RightControl"] = nameof(Key.RightCtrl),
        ["Control"] = nameof(Key.LeftCtrl),
        ["Ctrl"] = nameof(Key.LeftCtrl),
        ["Alt"] = nameof(Key.LeftAlt),
        // WPF Key uses D0–D9; bare digits do not round-trip via KeyConverter / Enum.Parse.
        ["0"] = nameof(Key.D0),
        ["1"] = nameof(Key.D1),
        ["2"] = nameof(Key.D2),
        ["3"] = nameof(Key.D3),
        ["4"] = nameof(Key.D4),
        ["5"] = nameof(Key.D5),
        ["6"] = nameof(Key.D6),
        ["7"] = nameof(Key.D7),
        ["8"] = nameof(Key.D8),
        ["9"] = nameof(Key.D9)
    };

    private readonly IKeyboardEmulator _keyboardEmulator;
    private readonly IMouseEmulator _mouseEmulator;
    private readonly Func<bool> _canDispatchOutput;
    private readonly Action<string> _setMappedOutput;
    private readonly Action<string> _setMappingStatus;
    private readonly OutputStateTracker _outputStateTracker = new();
    private readonly AnalogProcessor _analogProcessor = new();
    private readonly InputDispatcher _inputDispatcher;

    private readonly TriggerMoment _buttonPressedTrigger = TriggerMoment.Pressed;
    private readonly TriggerMoment _buttonTapTrigger = TriggerMoment.Tap;

    public MappingEngine(
        IKeyboardEmulator keyboardEmulator,
        IMouseEmulator mouseEmulator,
        Func<bool> canDispatchOutput,
        Action<Action> runOnUi,
        Action<string> setMappedOutput,
        Action<string> setMappingStatus)
    {
        _keyboardEmulator = keyboardEmulator;
        _mouseEmulator = mouseEmulator;
        _canDispatchOutput = canDispatchOutput;
        _setMappedOutput = setMappedOutput;
        _setMappingStatus = setMappingStatus;
        _inputDispatcher = new InputDispatcher(DispatchMappedOutput, runOnUi, setMappedOutput, setMappingStatus);
    }

    public TriggerMoment ButtonPressedTrigger => _buttonPressedTrigger;
    public TriggerMoment ButtonTapTrigger => _buttonTapTrigger;

    public void HandleButtonMappings(
        GamepadButtons buttons,
        TriggerMoment trigger,
        IReadOnlyCollection<GamepadButtons> activeButtons,
        IReadOnlyCollection<MappingEntry> mappings,
        float leftTriggerValue,
        float rightTriggerValue)
    {
        var buttonName = buttons.ToString();
        var snapshot = mappings.ToList();
        var releasedOutputsHandledByMappings = trigger == TriggerMoment.Released
            ? CollectReleasedOutputsHandledByMappings(buttons, activeButtons, snapshot, leftTriggerValue, rightTriggerValue)
            : null;

        if (trigger == TriggerMoment.Released)
        {
            ForceReleaseHeldOutputsForButton(buttons, releasedOutputsHandledByMappings);
        }
        else if (!_canDispatchOutput())
        {
            _setMappingStatus($"Suppressed ({buttons}, {trigger}) - target is not foreground");
            return;
        }

        var matched = false;
        var candidates = new List<(MappingEntry Mapping, List<GamepadButtons> ChordButtons, bool RequiresRightTrigger, bool RequiresLeftTrigger, string SourceToken)>();

        foreach (var mapping in snapshot)
        {
            if (mapping?.From is null) continue;
            if (mapping.From.Type != GamepadBindingType.Button) continue;
            if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var chordButtons, out var reqRt, out var reqLt, out var sourceToken))
                continue;
            var triggerThreshold = GetTriggerMatchThreshold(mapping);
            if (!ChordResolver.DoesChordMatchEvent(chordButtons, reqRt, reqLt, leftTriggerValue, rightTriggerValue, triggerThreshold, buttons, activeButtons))
                continue;
            if (mapping.Trigger != trigger) continue;
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
                if (!TryResolveMappedOutput(candidate.Mapping.KeyboardKey, out var output, out var baseLabel))
                    continue;

                _outputStateTracker.TrackOutputHoldState(candidate.SourceToken, candidate.ChordButtons, output, trigger);
                var outputLabel = $"{baseLabel} ({trigger})";
                _setMappedOutput(outputLabel);
                _setMappingStatus($"Queued: {candidate.SourceToken} ({trigger}) -> {outputLabel}");
                QueueOutputDispatch(candidate.SourceToken, trigger, output, outputLabel, candidate.Mapping.KeyboardKey ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to send key mapping. key={candidate.Mapping.KeyboardKey}, ex={ex.Message}");
                _setMappingStatus($"Error sending '{candidate.Mapping.KeyboardKey}': {ex.Message}");
            }
        }

        if (!matched)
            _setMappingStatus($"No mapping for {buttonName} ({trigger})");
    }

    public void HandleThumbstickMappings(GamepadBindingType sourceType, Vector2 stickValue, IReadOnlyCollection<MappingEntry> mappings)
    {
        if (!_canDispatchOutput())
        {
            ForceReleaseAnalogOutputs();
            return;
        }

        var snapshot = mappings.ToList();
        var mouseDeltaX = 0f;
        var mouseDeltaY = 0f;

        foreach (var mapping in snapshot)
        {
            if (mapping?.From is null || mapping.From.Type != sourceType)
                continue;

            if (!AnalogProcessor.TryParseAnalogSource(mapping.From.Value, out var source))
                continue;

            var outputToken = NormalizeKeyboardKeyToken(mapping.KeyboardKey ?? string.Empty);
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

            var key = ParseKey(outputToken);
            if (key == Key.None)
                continue;

            HandleAnalogKeyboardOutput(mapping, source, stickValue, key);
        }

        if (MathF.Abs(mouseDeltaX) > 0f || MathF.Abs(mouseDeltaY) > 0f)
            SendMouseLookDelta(mouseDeltaX, mouseDeltaY);
    }

    public void HandleTriggerMappings(GamepadBindingType triggerBindingType, float triggerValue, IReadOnlyCollection<MappingEntry> mappings)
    {
        if (!_canDispatchOutput())
        {
            ForceReleaseAnalogOutputs();
            return;
        }

        var snapshot = mappings.ToList();
        foreach (var mapping in snapshot)
        {
            if (mapping?.From is null || mapping.From.Type != triggerBindingType)
                continue;

            if (!TryResolveMappedOutput(mapping.KeyboardKey, out var output, out var baseLabel))
                continue;

            var stateKey =
                $"Trigger|{mapping.From.Type}|{mapping.From.Value}|{NormalizeKeyboardKeyToken(mapping.KeyboardKey ?? string.Empty)}|{mapping.Trigger}";
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
            ForceReleaseAllOutputs();
            ForceReleaseAnalogOutputs();
            _inputDispatcher.Dispose();
        }
        catch
        {
            // Best-effort shutdown.
        }
    }

    public static Key ParseKey(string? keyboardKey)
    {
        if (string.IsNullOrWhiteSpace(keyboardKey))
            return Key.None;

        var normalized = NormalizeKeyboardKeyToken(keyboardKey);

        if (Enum.TryParse<Key>(normalized, true, out var key))
            return key;

        try
        {
            var converter = new KeyConverter();
            var converted = converter.ConvertFromString(normalized);
            return converted is Key k ? k : Key.None;
        }
        catch
        {
            return Key.None;
        }
    }

    public static string NormalizeKeyboardKeyToken(string keyboardKey)
    {
        var token = keyboardKey.Trim();
        return KeyAliases.TryGetValue(token, out var alias) ? alias : token;
    }

    public static bool IsMouseLookOutput(string token) => AnalogProcessor.IsMouseLookOutput(token);

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
            if (!TryResolveMappedOutput(mapping.KeyboardKey, out var output, out _))
                continue;

            handledOutputs.Add(output);
        }

        return handledOutputs;
    }

    private static float GetTriggerMatchThreshold(MappingEntry mapping) =>
        mapping.AnalogThreshold is > 0 and <= 1 ? mapping.AnalogThreshold.Value : 0.35f;

    private bool TryResolveMappedOutput(string? outputToken, out DispatchedOutput output, out string outputLabel)
    {
        output = default;
        outputLabel = string.Empty;
        var normalized = NormalizeKeyboardKeyToken(outputToken ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (TryParsePointerAction(normalized, out var pointerAction))
        {
            output = new DispatchedOutput(null, pointerAction);
            outputLabel = DescribePointerAction(pointerAction);
            return true;
        }

        var key = ParseKey(normalized);
        if (key == Key.None)
            return false;

        output = new DispatchedOutput(key, null);
        outputLabel = $"Keyboard {key}";
        return true;
    }

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

    private static string DescribePointerAction(PointerAction action)
    {
        return action switch
        {
            PointerAction.LeftClick => "Mouse Left",
            PointerAction.RightClick => "Mouse Right",
            PointerAction.MiddleClick => "Mouse Middle",
            PointerAction.X1Click => "Mouse X1",
            PointerAction.X2Click => "Mouse X2",
            PointerAction.WheelUp => "Mouse Wheel Up",
            PointerAction.WheelDown => "Mouse Wheel Down",
            _ => "Mouse"
        };
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

    private static bool TryParsePointerAction(string token, out PointerAction action)
    {
        var normalized = token.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

        action = normalized switch
        {
            "MOUSELEFT" or "LEFTCLICK" or "LCLICK" or "LBUTTON" => PointerAction.LeftClick,
            "MOUSERIGHT" or "RIGHTCLICK" or "RCLICK" or "RBUTTON" => PointerAction.RightClick,
            "MOUSEMIDDLE" or "MIDDLECLICK" or "MCLICK" or "MBUTTON" => PointerAction.MiddleClick,
            "MOUSEX1" or "XBUTTON1" or "XBUTTONONE" => PointerAction.X1Click,
            "MOUSEX2" or "XBUTTON2" or "XBUTTONTWO" => PointerAction.X2Click,
            "WHEELUP" or "MOUSEWHEELUP" or "SCROLLUP" => PointerAction.WheelUp,
            "WHEELDOWN" or "MOUSEWHEELDOWN" or "SCROLLDOWN" => PointerAction.WheelDown,
            _ => default
        };

        return normalized is
            "MOUSELEFT" or "LEFTCLICK" or "LCLICK" or "LBUTTON" or
            "MOUSERIGHT" or "RIGHTCLICK" or "RCLICK" or "RBUTTON" or
            "MOUSEMIDDLE" or "MIDDLECLICK" or "MCLICK" or "MBUTTON" or
            "MOUSEX1" or "XBUTTON1" or "XBUTTONONE" or
            "MOUSEX2" or "XBUTTON2" or "XBUTTONTWO" or
            "WHEELUP" or "MOUSEWHEELUP" or "SCROLLUP" or
            "WHEELDOWN" or "MOUSEWHEELDOWN" or "SCROLLDOWN";
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
