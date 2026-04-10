using System;
using System.Globalization;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

/// <summary>
/// Public helpers for normalizing button+trigger chord expressions (shared by UI and validation).
/// </summary>
public static class GamepadChordInput
{
    /// <summary>Separator used in normalized chord strings from <see cref="ChordResolver"/>.</summary>
    public const string NormalizedTokenSeparator = " + ";

    /// <summary>
    /// When the user picks only LT/RT (same as JSON <c>from.type: LeftTrigger / RightTrigger</c>), not a <see cref="GamepadBindingType.Button"/> chord.
    /// </summary>
    public static bool TryCreateNativeTriggerOnlyBinding(string? singleToken, out GamepadBinding binding)
    {
        binding = new GamepadBinding();
        var t = (singleToken ?? string.Empty).Trim();
        if (t.Length == 0)
            return false;

        if (t.Equals(nameof(GamepadBindingType.LeftTrigger), StringComparison.OrdinalIgnoreCase) ||
            t.Equals("LT", StringComparison.OrdinalIgnoreCase))
        {
            binding.Type = GamepadBindingType.LeftTrigger;
            binding.Value = nameof(GamepadBindingType.LeftTrigger);
            return true;
        }

        if (t.Equals(nameof(GamepadBindingType.RightTrigger), StringComparison.OrdinalIgnoreCase) ||
            t.Equals("RT", StringComparison.OrdinalIgnoreCase))
        {
            binding.Type = GamepadBindingType.RightTrigger;
            binding.Value = nameof(GamepadBindingType.RightTrigger);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true when the expression parses as a valid chord; <paramref name="normalized"/> is the canonical stored form.
    /// </summary>
    public static bool TryNormalizeButtonExpression(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return ChordResolver.TryParseButtonChord(raw.Trim(), out _, out _, out _, out normalized);
    }

    /// <summary>
    /// True when <paramref name="raw"/> parses as a chord that requires LT and/or RT to be considered held (same rules as <see cref="ChordResolver"/>).
    /// </summary>
    public static bool ExpressionInvolvesTrigger(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return ChordResolver.TryParseButtonChord(raw.Trim(), out _, out var requiresRightTrigger, out var requiresLeftTrigger, out _) &&
               (requiresRightTrigger || requiresLeftTrigger);
    }

    /// <summary>
    /// Whether the mapping editor should show the trigger match threshold field: valid LT/RT chord, or any segment that names LT/RT
    /// (e.g. user picked only RightTrigger before completing the chord — <see cref="ExpressionInvolvesTrigger"/> would still be false).
    /// </summary>
    public static bool ShouldShowTriggerMatchThresholdEditor(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var trimmed = raw.Trim();
        if (ExpressionInvolvesTrigger(trimmed))
            return true;

        foreach (var segment in trimmed.Split(['+', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (SegmentMeansTriggerToken(segment))
                return true;
        }

        return false;
    }

    private static bool SegmentMeansTriggerToken(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return false;

        if (segment.Equals("RightTrigger", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("RT", StringComparison.OrdinalIgnoreCase))
            return true;

        return segment.Equals("LeftTrigger", StringComparison.OrdinalIgnoreCase) ||
               segment.Equals("LT", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a user-entered trigger match threshold (must be &gt; 0 and ≤ 1; matches mapping engine).
    /// </summary>
    public static bool TryParseTriggerMatchThreshold(string? text, out float value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            return false;

        if (!float.IsFinite(f) || f <= 0f || f > 1f)
            return false;

        value = f;
        return true;
    }

    /// <summary>Splits a normalized chord string into segments (from <see cref="TryNormalizeButtonExpression"/>).</summary>
    public static string[] SplitNormalizedParts(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
            return [];

        return normalized.Split(
            [NormalizedTokenSeparator],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
