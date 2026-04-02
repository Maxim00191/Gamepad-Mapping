using System;
using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

internal static class ChordResolver
{
    public static bool TryParseButtonChord(string? sourceToken, out List<GamepadButtons> chordButtons, out string normalizedSourceToken) =>
        TryParseButtonChord(sourceToken, out chordButtons, out _, out _, out normalizedSourceToken);

    public static bool TryParseButtonChord(
        string? sourceToken,
        out List<GamepadButtons> chordButtons,
        out bool requiresRightTrigger,
        out bool requiresLeftTrigger,
        out string normalizedSourceToken)
    {
        chordButtons = [];
        requiresRightTrigger = false;
        requiresLeftTrigger = false;
        normalizedSourceToken = string.Empty;
        if (string.IsNullOrWhiteSpace(sourceToken))
            return false;

        var segments = sourceToken.Split(['+', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return false;

        foreach (var segment in segments)
        {
            if (segment.Equals("RightTrigger", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("RT", StringComparison.OrdinalIgnoreCase))
            {
                requiresRightTrigger = true;
                continue;
            }

            if (segment.Equals("LeftTrigger", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("LT", StringComparison.OrdinalIgnoreCase))
            {
                requiresLeftTrigger = true;
                continue;
            }

            if (!Enum.TryParse<GamepadButtons>(segment, true, out var button) || button == GamepadButtons.None)
            {
                chordButtons = [];
                requiresRightTrigger = false;
                requiresLeftTrigger = false;
                normalizedSourceToken = string.Empty;
                return false;
            }
            if (!chordButtons.Contains(button))
                chordButtons.Add(button);
        }

        if (chordButtons.Count == 0)
            return false;

        // Semantic validation: physically impossible combinations
        if (HasImpossibleCombination(chordButtons))
        {
            chordButtons = [];
            requiresRightTrigger = false;
            requiresLeftTrigger = false;
            normalizedSourceToken = string.Empty;
            return false;
        }

        var parts = new List<string>();
        if (requiresLeftTrigger)
            parts.Add(nameof(GamepadBindingType.LeftTrigger));
        if (requiresRightTrigger)
            parts.Add(nameof(GamepadBindingType.RightTrigger));
        parts.AddRange(chordButtons.OrderBy(b => b.ToString()).Select(b => b.ToString()));
        normalizedSourceToken = string.Join(" + ", parts);
        return true;
    }

    public static bool DoesChordMatchEvent(
        IReadOnlyCollection<GamepadButtons> chordButtons,
        bool requiresRightTrigger,
        bool requiresLeftTrigger,
        float leftTriggerValue,
        float rightTriggerValue,
        float triggerMatchThreshold,
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

        if (requiresRightTrigger && rightTriggerValue < triggerMatchThreshold)
            return false;
        if (requiresLeftTrigger && leftTriggerValue < triggerMatchThreshold)
            return false;

        return true;
    }

    public static int ChordSpecificity(IReadOnlyCollection<GamepadButtons> chordButtons, bool requiresRightTrigger, bool requiresLeftTrigger) =>
        chordButtons.Count + (requiresRightTrigger ? 1 : 0) + (requiresLeftTrigger ? 1 : 0);

    /// <summary>ABXY — always treated as action keys for combo-lead deferral / long-release suppress (not modifier leads).</summary>
    public static bool IsFaceActionButton(GamepadButtons b) =>
        b is GamepadButtons.A or GamepadButtons.B or GamepadButtons.X or GamepadButtons.Y;

    /// <summary>
    /// True when <paramref name="other"/> is strictly more specific than <paramref name="candidate"/>
    /// and both refer to the same logical chord (candidate's requirements are implied by other's).
    /// </summary>
    public static bool IsOtherChordStrictlyMoreSpecific(
        IReadOnlyCollection<GamepadButtons> candidateChord,
        bool candidateReqRt,
        bool candidateReqLt,
        IReadOnlyCollection<GamepadButtons> otherChord,
        bool otherReqRt,
        bool otherReqLt)
    {
        if (!candidateChord.All(otherChord.Contains))
            return false;
        if (candidateReqRt && !otherReqRt)
            return false;
        if (candidateReqLt && !otherReqLt)
            return false;
        return ChordSpecificity(otherChord, otherReqRt, otherReqLt) >
               ChordSpecificity(candidateChord, candidateReqRt, candidateReqLt);
    }

    private static bool HasImpossibleCombination(List<GamepadButtons> buttons)
    {
        bool Has(GamepadButtons b) => buttons.Contains(b);

        // D-Pad opposites
        if (Has(GamepadButtons.DPadUp) && Has(GamepadButtons.DPadDown)) return true;
        if (Has(GamepadButtons.DPadLeft) && Has(GamepadButtons.DPadRight)) return true;

        // Thumbstick opposites (if defined in GamepadButtons, which they are in some XInput wrappers)
        // Vortice.XInput.GamepadButtons typically only includes digital buttons.
        // Let's check if there are any other digital buttons that are mutually exclusive.
        
        return false;
    }
}
