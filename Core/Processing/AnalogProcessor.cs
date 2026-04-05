using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

internal readonly record struct AnalogSourceDefinition(bool IsDirectional, bool IsSignedAxis, bool IsVerticalAxis, int DirectionSign);
internal readonly record struct AnalogOutputTransition(bool HasChanged, bool IsActive);
internal readonly record struct MouseLookDelta(int PixelDx, int PixelDy);

internal sealed class AnalogProcessor
{
    private readonly record struct StateKey(GamepadBindingType Type, string Value, Key Key, TriggerMoment Trigger);
    private readonly Dictionary<StateKey, bool> _analogOutputStates = new();
    private readonly HashSet<StateKey> _activeStates = new();
    private readonly Dictionary<string, Key> _keyEnumCache = new(StringComparer.OrdinalIgnoreCase);
    private float _mouseLookResidualLeftX;
    private float _mouseLookResidualLeftY;
    private float _mouseLookResidualRightX;
    private float _mouseLookResidualRightY;

    private const float DefaultAnalogThreshold = 0.35f;
    private const float DefaultMouseLookSensitivity = 18f;
    private const float JitterHysteresis = 0.01f;

    public static bool TryParseAnalogSource(string token, out AnalogSourceDefinition source)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            source = default;
            return false;
        }

        var normalized = token.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

        source = normalized switch
        {
            "RIGHT" or "POSX" or "XPOS" or "XP" or "XPLUS" => new AnalogSourceDefinition(true, false, false, +1),
            "LEFT" or "NEGX" or "XNEG" or "XM" or "XMINUS" => new AnalogSourceDefinition(true, false, false, -1),
            "UP" or "FORWARD" or "POSY" or "YPOS" or "YP" or "YPLUS" => new AnalogSourceDefinition(true, false, true, +1),
            "DOWN" or "BACK" or "BACKWARD" or "NEGY" or "YNEG" or "YM" or "YMINUS" => new AnalogSourceDefinition(true, false, true, -1),
            "X" or "HORIZONTAL" => new AnalogSourceDefinition(false, true, false, +1),
            "Y" or "VERTICAL" => new AnalogSourceDefinition(false, true, true, +1),
            "MAGNITUDE" or "MAG" or "DISTANCE" or "RADIAL" => new AnalogSourceDefinition(false, false, false, 0),
            _ => default
        };

        return normalized is
            "RIGHT" or "POSX" or "XPOS" or "XP" or "XPLUS" or
            "LEFT" or "NEGX" or "XNEG" or "XM" or "XMINUS" or
            "UP" or "FORWARD" or "POSY" or "YPOS" or "YP" or "YPLUS" or
            "DOWN" or "BACK" or "BACKWARD" or "NEGY" or "YNEG" or "YM" or "YMINUS" or
            "X" or "HORIZONTAL" or
            "Y" or "VERTICAL" or
            "MAGNITUDE" or "MAG" or "DISTANCE" or "RADIAL";
    }

    public static bool TryResolveMouseLookOutput(string token, out bool isVerticalLook)
    {
        var normalized = token.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

        if (normalized is "MOUSEX" or "MOUSELOOKX" or "LOOKX" or "VIEWX")
        {
            isVerticalLook = false;
            return true;
        }

        if (normalized is "MOUSEY" or "MOUSELOOKY" or "LOOKY" or "VIEWY")
        {
            isVerticalLook = true;
            return true;
        }

        isVerticalLook = false;
        return false;
    }

    public static bool IsMouseLookOutput(string token) => TryResolveMouseLookOutput(token, out _);

    public static float ResolveStickAxisValue(Vector2 value, AnalogSourceDefinition source)
    {
        // Handle NaN/Infinity for robustness
        var x = float.IsFinite(value.X) ? value.X : 0f;
        var y = float.IsFinite(value.Y) ? value.Y : 0f;

        // If not directional and not signed axis, it's magnitude (DirectionSign 0)
        if (!source.IsDirectional && !source.IsSignedAxis)
            return Math.Clamp(new Vector2(x, y).Length(), 0f, 1f);

        var axisRaw = source.IsVerticalAxis ? y : x;
        if (source.IsSignedAxis)
            return Math.Clamp(axisRaw, -1f, 1f);

        return source.DirectionSign >= 0
            ? Math.Clamp(axisRaw, 0f, 1f)
            : Math.Clamp(-axisRaw, 0f, 1f);
    }

    public AnalogOutputTransition EvaluateKeyboardTransition(MappingEntry mapping, AnalogSourceDefinition source, Vector2 stickValue, Key key)
    {
        var threshold = mapping.AnalogThreshold is > 0 and <= 1 ? mapping.AnalogThreshold.Value : DefaultAnalogThreshold;
        var axisValue = ResolveStickAxisValue(stickValue, source);
        
        var stateKey = new StateKey(mapping.From.Type, mapping.From.Value ?? string.Empty, key, mapping.Trigger);
        _analogOutputStates.TryGetValue(stateKey, out var currentState);

        // Apply hysteresis to prevent jitter
        var effectiveThreshold = currentState ? (threshold - JitterHysteresis) : threshold;
        var isActive = axisValue >= effectiveThreshold;

        if (currentState == isActive)
            return new AnalogOutputTransition(false, isActive);

        _analogOutputStates[stateKey] = isActive;
        if (isActive) _activeStates.Add(stateKey);
        else _activeStates.Remove(stateKey);

        return new AnalogOutputTransition(true, isActive);
    }

    public AnalogOutputTransition EvaluateTriggerTransition(MappingEntry mapping, float triggerValue, string? unusedStateKey = null)
    {
        var threshold = mapping.AnalogThreshold is > 0 and <= 1 ? mapping.AnalogThreshold.Value : DefaultAnalogThreshold;
        
        // Use pre-parsed key or cache it to avoid repeated Enum.TryParse
        if (mapping.KeyboardKey == null)
        {
            return new AnalogOutputTransition(false, false);
        }

        if (!_keyEnumCache.TryGetValue(mapping.KeyboardKey, out var k))
        {
            k = Enum.TryParse(mapping.KeyboardKey, out Key parsedKey) ? parsedKey : Key.None;
            _keyEnumCache[mapping.KeyboardKey] = k;
        }

        var stateKey = new StateKey(mapping.From.Type, mapping.From.Value ?? string.Empty, k, mapping.Trigger);
        _analogOutputStates.TryGetValue(stateKey, out var currentState);
        
        // Apply hysteresis to prevent jitter
        var effectiveThreshold = currentState ? (threshold - JitterHysteresis) : threshold;
        var isActive = triggerValue >= effectiveThreshold;

        if (currentState == isActive)
            return new AnalogOutputTransition(false, isActive);

        _analogOutputStates[stateKey] = isActive;
        if (isActive) _activeStates.Add(stateKey);
        else _activeStates.Remove(stateKey);

        return new AnalogOutputTransition(true, isActive);
    }

    public MouseLookDelta AccumulateMouseLookDelta(GamepadBindingType thumbstickSource, float deltaX, float deltaY)
    {
        ref var rx = ref GetMouseLookResidualXRef(thumbstickSource);
        ref var ry = ref GetMouseLookResidualYRef(thumbstickSource);
        rx += deltaX;
        ry += deltaY;
        var pixelDx = (int)MathF.Truncate(rx);
        var pixelDy = (int)MathF.Truncate(ry);
        rx -= pixelDx;
        ry -= pixelDy;
        return new MouseLookDelta(pixelDx, pixelDy);
    }

    public void RemoveAnalogKeyboardStateForBinding(GamepadBindingType bindingType)
    {
        foreach (var key in _analogOutputStates.Keys.Where(k => k.Type == bindingType).ToList())
            _analogOutputStates.Remove(key);

        _activeStates.RemoveWhere(s => s.Type == bindingType);
        ClearMouseLookResidualForThumbstick(bindingType);
    }

    public IEnumerable<(Key Key, TriggerMoment Trigger)> GetActiveNonTapOutputsForBinding(GamepadBindingType bindingType)
    {
        foreach (var state in _activeStates)
        {
            if (state.Type != bindingType || state.Trigger == TriggerMoment.Tap)
                continue;

            yield return (state.Key, state.Trigger);
        }
    }

    public IEnumerable<(Key Key, TriggerMoment Trigger)> GetActiveNonTapOutputs()
    {
        foreach (var state in _activeStates)
        {
            if (state.Trigger == TriggerMoment.Tap)
                continue;

            yield return (state.Key, state.Trigger);
        }
    }

    public void Reset()
    {
        _analogOutputStates.Clear();
        _activeStates.Clear();
        _keyEnumCache.Clear();
        _mouseLookResidualLeftX = 0f;
        _mouseLookResidualLeftY = 0f;
        _mouseLookResidualRightX = 0f;
        _mouseLookResidualRightY = 0f;
    }

    public static float DefaultLookSensitivity => DefaultMouseLookSensitivity;

    private ref float GetMouseLookResidualXRef(GamepadBindingType thumbstickSource) =>
        ref thumbstickSource == GamepadBindingType.LeftThumbstick
            ? ref _mouseLookResidualLeftX
            : ref _mouseLookResidualRightX;

    private ref float GetMouseLookResidualYRef(GamepadBindingType thumbstickSource) =>
        ref thumbstickSource == GamepadBindingType.LeftThumbstick
            ? ref _mouseLookResidualLeftY
            : ref _mouseLookResidualRightY;

    private void ClearMouseLookResidualForThumbstick(GamepadBindingType thumbstickSource)
    {
        GetMouseLookResidualXRef(thumbstickSource) = 0f;
        GetMouseLookResidualYRef(thumbstickSource) = 0f;
    }
}
