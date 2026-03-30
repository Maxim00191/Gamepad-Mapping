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
    private readonly Dictionary<string, bool> _analogOutputStates = new();
    private float _mouseLookResidualX;
    private float _mouseLookResidualY;

    private const float DefaultAnalogThreshold = 0.35f;
    private const float DefaultMouseLookSensitivity = 18f;

    public static bool TryParseAnalogSource(string token, out AnalogSourceDefinition source)
    {
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
            _ => default
        };

        return normalized is
            "RIGHT" or "POSX" or "XPOS" or "XP" or "XPLUS" or
            "LEFT" or "NEGX" or "XNEG" or "XM" or "XMINUS" or
            "UP" or "FORWARD" or "POSY" or "YPOS" or "YP" or "YPLUS" or
            "DOWN" or "BACK" or "BACKWARD" or "NEGY" or "YNEG" or "YM" or "YMINUS" or
            "X" or "HORIZONTAL" or
            "Y" or "VERTICAL";
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
        var axisRaw = source.IsVerticalAxis ? value.Y : value.X;
        if (source.IsSignedAxis)
            return axisRaw;
        if (!source.IsDirectional)
            return 0f;

        return source.DirectionSign >= 0
            ? MathF.Max(0f, axisRaw)
            : MathF.Max(0f, -axisRaw);
    }

    public AnalogOutputTransition EvaluateKeyboardTransition(MappingEntry mapping, AnalogSourceDefinition source, Vector2 stickValue, Key key)
    {
        var threshold = mapping.AnalogThreshold is > 0 and <= 1 ? mapping.AnalogThreshold.Value : DefaultAnalogThreshold;
        var axisValue = ResolveStickAxisValue(stickValue, source);
        var isActive = axisValue >= threshold;
        var stateKey = BuildAnalogStateKey(mapping, key);
        _analogOutputStates.TryGetValue(stateKey, out var currentState);
        if (currentState == isActive)
            return new AnalogOutputTransition(false, isActive);

        _analogOutputStates[stateKey] = isActive;
        return new AnalogOutputTransition(true, isActive);
    }

    public AnalogOutputTransition EvaluateTriggerTransition(MappingEntry mapping, float triggerValue, string stateKey)
    {
        var threshold = mapping.AnalogThreshold is > 0 and <= 1 ? mapping.AnalogThreshold.Value : DefaultAnalogThreshold;
        var isActive = triggerValue >= threshold;
        _analogOutputStates.TryGetValue(stateKey, out var currentState);
        if (currentState == isActive)
            return new AnalogOutputTransition(false, isActive);

        _analogOutputStates[stateKey] = isActive;
        return new AnalogOutputTransition(true, isActive);
    }

    public MouseLookDelta AccumulateMouseLookDelta(float deltaX, float deltaY)
    {
        _mouseLookResidualX += deltaX;
        _mouseLookResidualY += deltaY;
        var pixelDx = (int)MathF.Truncate(_mouseLookResidualX);
        var pixelDy = (int)MathF.Truncate(_mouseLookResidualY);
        _mouseLookResidualX -= pixelDx;
        _mouseLookResidualY -= pixelDy;
        return new MouseLookDelta(pixelDx, pixelDy);
    }

    public IEnumerable<(Key Key, TriggerMoment Trigger)> GetActiveNonTapOutputs()
    {
        foreach (var kvp in _analogOutputStates.Where(x => x.Value))
        {
            var parts = kvp.Key.Split('|');
            if (parts.Length < 4)
                continue;

            if (!Enum.TryParse(parts[2], true, out Key key) || key == Key.None)
                continue;
            if (!Enum.TryParse(parts[3], true, out TriggerMoment trigger) || trigger == TriggerMoment.Tap)
                continue;

            yield return (key, trigger);
        }
    }

    public void Reset()
    {
        _analogOutputStates.Clear();
        _mouseLookResidualX = 0f;
        _mouseLookResidualY = 0f;
    }

    private static string BuildAnalogStateKey(MappingEntry mapping, Key key)
    {
        var sourceType = mapping.From.Type.ToString();
        var sourceValue = mapping.From.Value ?? string.Empty;
        var trigger = mapping.Trigger.ToString();
        return $"{sourceType}|{sourceValue}|{key}|{trigger}";
    }

    public static float DefaultLookSensitivity => DefaultMouseLookSensitivity;
}
