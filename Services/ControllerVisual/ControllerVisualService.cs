using System;
using System.Collections.Generic;
using Gamepad_Mapping.Interfaces.Services.ControllerVisual;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Services.ControllerVisual;

public class ControllerVisualService : IControllerVisualService
{
    private static readonly Dictionary<string, (GamepadBindingType Type, string Value, string DisplayName)> _idMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "btn_A", (GamepadBindingType.Button, "A", "Button A") },
        { "btn_B", (GamepadBindingType.Button, "B", "Button B") },
        { "btn_X", (GamepadBindingType.Button, "X", "Button X") },
        { "btn_Y", (GamepadBindingType.Button, "Y", "Button Y") },
        { "shoulder_L", (GamepadBindingType.Button, "LeftShoulder", "Left Shoulder") },
        { "shoulder_R", (GamepadBindingType.Button, "RightShoulder", "Right Shoulder") },
        { "trigger_L", (GamepadBindingType.LeftTrigger, "LeftTrigger", "Left Trigger") },
        { "trigger_R", (GamepadBindingType.RightTrigger, "RightTrigger", "Right Trigger") },
        { "btn_back", (GamepadBindingType.Button, "Back", "Back") },
        { "btn_start", (GamepadBindingType.Button, "Start", "Start") },
        { "btn_share", (GamepadBindingType.Button, "Share", "Share") },
        { "btn_home", (GamepadBindingType.Button, "Home", "Home") },
        { "dpad_U", (GamepadBindingType.Button, "DPadUp", "D-Pad Up") },
        { "dpad_D", (GamepadBindingType.Button, "DPadDown", "D-Pad Down") },
        { "dpad_L", (GamepadBindingType.Button, "DPadLeft", "D-Pad Left") },
        { "dpad_R", (GamepadBindingType.Button, "DPadRight", "D-Pad Right") },
        { "thumbStick_L", (GamepadBindingType.LeftThumbstick, "LeftThumbstick", "Left Thumbstick") },
        { "thumbStick_R", (GamepadBindingType.RightThumbstick, "RightThumbstick", "Right Thumbstick") },
        { "thumb_L", (GamepadBindingType.Button, "LeftThumb", "Left Stick") },
        { "thumb_R", (GamepadBindingType.Button, "RightThumb", "Right Stick") }
    };

    private const string ThumbStickLeftId = "thumbStick_L";
    private const string ThumbStickRightId = "thumbStick_R";

    public string? MapBindingToId(string value, GamepadBindingType type)
    {
        if (type == GamepadBindingType.LeftThumbstick && IsRecognizedThumbstickFromValue(GamepadBindingType.LeftThumbstick, value))
            return ThumbStickLeftId;

        if (type == GamepadBindingType.RightThumbstick && IsRecognizedThumbstickFromValue(GamepadBindingType.RightThumbstick, value))
            return ThumbStickRightId;

        foreach (var kvp in _idMap)
        {
            if (kvp.Value.Type == type && string.Equals(kvp.Value.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Key;
            }
        }
        return null;
    }

    public string? MapChordSegmentToLogicalControlId(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return null;

        var t = segment.Trim();
        if (GamepadChordInput.TryCreateNativeTriggerOnlyBinding(t, out var triggerBinding))
            return MapBindingToId(triggerBinding.Value, triggerBinding.Type);

        foreach (var kvp in _idMap)
        {
            if (string.Equals(kvp.Value.Value, t, StringComparison.OrdinalIgnoreCase))
                return kvp.Key;
        }

        return null;
    }

    public IEnumerable<string> EnumerateMappedLogicalControlIds() => _idMap.Keys;

    public GamepadBinding? MapIdToBinding(string elementId)
    {
        if (_idMap.TryGetValue(elementId, out var info))
        {
            return new GamepadBinding { Type = info.Type, Value = info.Value };
        }
        return null;
    }

    public string GetDisplayName(string elementId)
    {
        if (_idMap.TryGetValue(elementId, out var info))
        {
            return info.DisplayName;
        }
        return elementId;
    }

    public IEnumerable<MappingEntry> GetMappingsForElement(string elementId, IEnumerable<MappingEntry> mappings)
    {
        if (MapIdToBinding(elementId) is null)
            return [];

        return mappings.Where(m => IsMappingOnLogicalControl(m, elementId));
    }

    public bool IsMappingOnLogicalControl(MappingEntry mapping, string elementId)
    {
        if (string.IsNullOrEmpty(elementId) || mapping.From is null)
            return false;

        if (MapIdToBinding(elementId) is null)
            return false;

        var value = mapping.From.Value ?? string.Empty;
        if (value.IndexOf('+', StringComparison.Ordinal) < 0)
        {
            if (IsThumbstickSurfaceId(elementId))
                return MatchesThumbstickAnalogMapping(mapping.From, elementId);

            var binding = MapIdToBinding(elementId)!;
            if (mapping.From.Type != binding.Type)
                return false;

            return string.Equals(value, binding.Value, StringComparison.OrdinalIgnoreCase);
        }

        var parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            var id = MapChordSegmentToLogicalControlId(p);
            if (id is not null && string.Equals(id, elementId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (mapping.From.Type is GamepadBindingType.LeftThumbstick or GamepadBindingType.RightThumbstick)
        {
            foreach (var p in parts)
            {
                var id = MapBindingToId(p, mapping.From.Type);
                if (id is not null && string.Equals(id, elementId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static bool IsThumbstickSurfaceId(string elementId) =>
        string.Equals(elementId, ThumbStickLeftId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(elementId, ThumbStickRightId, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesThumbstickAnalogMapping(GamepadBinding from, string elementId)
    {
        if (string.Equals(elementId, ThumbStickLeftId, StringComparison.OrdinalIgnoreCase))
        {
            return from.Type == GamepadBindingType.LeftThumbstick &&
                   IsRecognizedThumbstickFromValue(GamepadBindingType.LeftThumbstick, from.Value);
        }

        if (string.Equals(elementId, ThumbStickRightId, StringComparison.OrdinalIgnoreCase))
        {
            return from.Type == GamepadBindingType.RightThumbstick &&
                   IsRecognizedThumbstickFromValue(GamepadBindingType.RightThumbstick, from.Value);
        }

        return false;
    }

    private static bool IsRecognizedThumbstickFromValue(GamepadBindingType stickType, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim();
        if (stickType == GamepadBindingType.LeftThumbstick &&
            string.Equals(v, "LeftThumbstick", StringComparison.OrdinalIgnoreCase))
            return true;

        if (stickType == GamepadBindingType.RightThumbstick &&
            string.Equals(v, "RightThumbstick", StringComparison.OrdinalIgnoreCase))
            return true;

        return AnalogProcessor.TryParseAnalogSource(v, out _);
    }
}
