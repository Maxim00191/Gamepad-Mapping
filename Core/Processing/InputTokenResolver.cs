using System;
using System.Collections.Generic;
using System.Windows.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

internal static class InputTokenResolver
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
        if (token.Length == 0 && keyboardKey.Length > 0 && keyboardKey.AsSpan().Contains(' '))
            return nameof(Key.Space);

        return KeyAliases.TryGetValue(token, out var alias) ? alias : token;
    }

    /// <summary>Parses modifier names for <see cref="ItemCycleBinding.WithKeys"/>; empty or null list is valid.</summary>
    public static bool TryParseItemCycleModifierKeys(IReadOnlyList<string>? tokens, out Key[] keys)
    {
        if (tokens is null || tokens.Count == 0)
        {
            keys = Array.Empty<Key>();
            return true;
        }

        var list = new List<Key>();
        foreach (var t in tokens)
        {
            if (string.IsNullOrWhiteSpace(t))
                continue;
            var k = ParseKey(t.Trim());
            if (k == Key.None)
            {
                keys = Array.Empty<Key>();
                return false;
            }

            list.Add(k);
        }

        keys = list.ToArray();
        return true;
    }

    /// <summary>
    /// Parses a chord string like "Ctrl+C" or "Alt+Shift+S" into modifiers and a main key.
    /// Returns true if the string is a valid chord.
    /// </summary>
    public static bool TryParseChord(string? chord, out Key[] modifiers, out Key mainKey)
    {
        modifiers = Array.Empty<Key>();
        mainKey = Key.None;

        if (string.IsNullOrWhiteSpace(chord) || !chord.Contains('+'))
            return false;

        var parts = chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return false;

        var modifierList = new List<Key>();
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var mod = ParseKey(parts[i]);
            if (mod == Key.None) return false;
            modifierList.Add(mod);
        }

        var main = ParseKey(parts[^1]);
        if (main == Key.None) return false;

        modifiers = modifierList.ToArray();
        mainKey = main;
        return true;
    }

    internal static bool TryResolveMappedOutput(string? outputToken, out DispatchedOutput output, out string outputLabel)
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

    internal static string DescribePointerAction(PointerAction action)
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

    internal static bool TryParsePointerAction(string token, out PointerAction action)
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
}
