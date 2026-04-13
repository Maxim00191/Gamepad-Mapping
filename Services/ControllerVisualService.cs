using System;
using System.Collections.Generic;
using Gamepad_Mapping.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.Services;

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
        { "thumb_L", (GamepadBindingType.Button, "LeftThumb", "Left Stick Click") },
        { "thumb_R", (GamepadBindingType.Button, "RightThumb", "Right Stick Click") }
    };

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
}
