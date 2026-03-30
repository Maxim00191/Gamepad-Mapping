using System;
using System.Collections.Generic;
using System.Linq;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

internal static class ChordResolver
{
    public static bool TryParseButtonChord(string? sourceToken, out List<GamepadButtons> chordButtons, out string normalizedSourceToken)
    {
        chordButtons = [];
        normalizedSourceToken = string.Empty;
        if (string.IsNullOrWhiteSpace(sourceToken))
            return false;

        var segments = sourceToken.Split(['+', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return false;

        foreach (var segment in segments)
        {
            if (!Enum.TryParse<GamepadButtons>(segment, true, out var button) || button == GamepadButtons.None)
                return false;
            if (!chordButtons.Contains(button))
                chordButtons.Add(button);
        }

        if (chordButtons.Count == 0)
            return false;

        normalizedSourceToken = string.Join(" + ", chordButtons.Select(b => b.ToString()));
        return true;
    }

    public static bool DoesChordMatchEvent(
        IReadOnlyCollection<GamepadButtons> chordButtons,
        GamepadButtons changedButton,
        IReadOnlyCollection<GamepadButtons> activeButtons)
    {
        if (!chordButtons.Contains(changedButton))
            return false;

        foreach (var button in chordButtons)
        {
            if (!activeButtons.Contains(button))
                return false;
        }

        return true;
    }
}
