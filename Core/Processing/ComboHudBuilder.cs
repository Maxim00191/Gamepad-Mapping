using System;
using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

internal static class ComboHudBuilder
{
    /// <summary>Whether a modifier combo HUD would have rows if output dispatch were allowed (ignores the dispatch gate).</summary>
    internal static bool HasModifierPrefixHudContent(
        IReadOnlyCollection<GamepadButtons> activeButtons,
        IReadOnlyList<MappingEntry> mappingsSnapshot,
        IReadOnlySet<GamepadButtons> comboLeads)
    {
        if (activeButtons is null || activeButtons.Count == 0)
            return false;

        var aChord = OrderHeldButtonsForTitle(activeButtons);
        const bool aReqRt = false;
        const bool aReqLt = false;

        var lines = CollectChordExtensionLines(aChord, aReqRt, aReqLt, mappingsSnapshot, comboLeads);
        return lines.Count > 0;
    }

    public static ComboHudContent? BuildModifierPrefixHud(
        Func<bool> canDispatchOutput,
        IReadOnlyCollection<GamepadButtons> activeButtons,
        IReadOnlyList<MappingEntry> mappingsSnapshot,
        IReadOnlySet<GamepadButtons> comboLeads)
    {
        if (activeButtons is null || activeButtons.Count == 0)
            return null;

        var aChord = OrderHeldButtonsForTitle(activeButtons);
        const bool aReqRt = false;
        const bool aReqLt = false;

        var lines = CollectChordExtensionLines(aChord, aReqRt, aReqLt, mappingsSnapshot, comboLeads);
        if (lines.Count == 0)
            return null;

        if (!canDispatchOutput())
            return null;

        var title = string.Join(" + ", aChord.Select(FormatHudButton));
        return new ComboHudContent(title, lines);
    }

    public static ComboHudContent BuildComboHud(
        HoldSessionManager.HoldSession session,
        IReadOnlyList<MappingEntry> mappings,
        IReadOnlySet<GamepadButtons> comboLeads)
    {
        MappingEntry? holdEntry = null;
        foreach (var m in mappings)
        {
            if (m?.From is null || m.From.Type != GamepadBindingType.Button || !HoldSessionManager.IsHoldDualMapping(m))
                continue;
            if (!ChordResolver.TryParseButtonChord(m.From.Value, out _, out _, out _, out var tok))
                continue;
            if (!string.Equals(tok, session.SourceToken, StringComparison.Ordinal))
                continue;
            holdEntry = m;
            break;
        }

        var lines = new List<ComboHudLine>();
        var shortSource = FormatHudChordLabel(session.SourceToken);
        if (!string.IsNullOrWhiteSpace(holdEntry?.Description))
        {
            var content = $"{shortSource} · tap {FormatHudOutput(session.ShortKeyToken)} · hold {session.HoldThresholdMs} ms → {FormatHudOutput(session.HoldKeyToken)}";
            lines.Add(new ComboHudLine(holdEntry.Description.Trim(), content));
        }
        else
        {
            lines.Add(new ComboHudLine($"Short press → {FormatHudOutput(session.ShortKeyToken)}", null));
            lines.Add(new ComboHudLine($"Hold {session.HoldThresholdMs} ms → {FormatHudOutput(session.HoldKeyToken)}", null));
        }

        lines.AddRange(CollectChordExtensionLines(
            OrderHeldButtonsForTitle(session.ChordButtons),
            session.RequiresRightTrigger,
            session.RequiresLeftTrigger,
            mappings,
            comboLeads));

        return new ComboHudContent(shortSource, lines);
    }

    public static List<ComboHudLine> CollectChordExtensionLines(
        List<GamepadButtons> baseChord,
        bool baseReqRt,
        bool baseReqLt,
        IReadOnlyList<MappingEntry> mappings,
        IReadOnlySet<GamepadButtons> comboLeads)
    {
        var acc = new List<(int Spec, int SortTier, int FaceKey, int DpadKey, string NormTok, ComboHudLine Line)>();
        foreach (var mapping in mappings)
        {
            if (mapping?.From is null || mapping.From.Type != GamepadBindingType.Button)
                continue;
            if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var chord, out var reqRt, out var reqLt, out var normTok))
                continue;
            if (!ChordResolver.IsOtherChordStrictlyMoreSpecific(baseChord, baseReqRt, baseReqLt, chord, reqRt, reqLt))
                continue;

            // Solo non–lead buttons: hide extensions that only add leads (e.g. shoulder + face while holding face).
            // Still show extensions that add another non-lead (e.g. A+X while holding A).
            if (baseChord.Count == 1 && !comboLeads.Contains(baseChord[0]) &&
                !chord.Any(b => !baseChord.Contains(b) && !comboLeads.Contains(b)))
                continue;

            if (mapping.Trigger == TriggerMoment.Released)
                continue;
            if (HoldSessionManager.IsHoldDualMapping(mapping))
                continue;

            var spec = ChordResolver.ChordSpecificity(chord, reqRt, reqLt);
            var comboLabel = FormatHudChordLabel(mapping.From.Value ?? normTok);
            var keyPart = FormatHudMappingOutput(mapping);
            var descPart = mapping.Description?.Trim();

            string? contentLine = null;
            if (!string.IsNullOrEmpty(keyPart))
                contentLine = string.IsNullOrEmpty(comboLabel) ? keyPart : $"{comboLabel} → {keyPart}";
            else if (!string.IsNullOrEmpty(comboLabel))
                contentLine = comboLabel;

            string primary;
            string? detail;
            if (!string.IsNullOrEmpty(descPart))
            {
                primary = descPart;
                detail = string.IsNullOrWhiteSpace(contentLine) ? null : contentLine;
            }
            else if (!string.IsNullOrEmpty(keyPart))
            {
                primary = keyPart;
                detail = string.IsNullOrEmpty(comboLabel) ? null : comboLabel;
            }
            else
            {
                primary = comboLabel;
                detail = null;
            }

            var added = chord.Where(b => !baseChord.Contains(b)).ToList();
            var (sortTier, faceKey, dpadKey) = ExtensionLineSortKeys(added);
            acc.Add((spec, sortTier, faceKey, dpadKey, normTok, new ComboHudLine(primary, detail)));
        }

        return acc
            .OrderBy(e => e.Spec)
            .ThenBy(e => e.SortTier)
            .ThenBy(e => e.FaceKey)
            .ThenBy(e => e.DpadKey)
            .ThenBy(e => e.NormTok, StringComparer.OrdinalIgnoreCase)
            .Select(e => e.Line)
            .ToList();
    }

    /// <summary>Face buttons in top-right-bottom-left (Y, B, A, X) order for HUD labels.</summary>
    private static int FaceYbaxIndex(GamepadButtons b) => b switch
    {
        GamepadButtons.Y => 0,
        GamepadButtons.B => 1,
        GamepadButtons.A => 2,
        GamepadButtons.X => 3,
        _ => -1
    };

    /// <summary>D-pad in clockwise-from-up (Up, Right, Down, Left) order for HUD labels.</summary>
    private static int DPadUrdlIndex(GamepadButtons b) => b switch
    {
        GamepadButtons.DPadUp => 0,
        GamepadButtons.DPadRight => 1,
        GamepadButtons.DPadDown => 2,
        GamepadButtons.DPadLeft => 3,
        _ => -1
    };

    private static bool IsFaceButton(GamepadButtons b) => FaceYbaxIndex(b) >= 0;

    private static bool IsDpadButton(GamepadButtons b) => DPadUrdlIndex(b) >= 0;

    /// <summary>0 = other (shoulders, thumbs, etc.), 1 = D-pad, 2 = face — so title reads … + D-pad + face (YBAX).</summary>
    private static int HudTitleButtonGroup(GamepadButtons b)
    {
        if (IsFaceButton(b)) return 2;
        if (IsDpadButton(b)) return 1;
        return 0;
    }

    private static List<GamepadButtons> OrderHeldButtonsForTitle(IReadOnlyCollection<GamepadButtons> activeButtons)
    {
        return activeButtons
            .OrderBy(HudTitleButtonGroup)
            .ThenBy(b =>
            {
                var g = HudTitleButtonGroup(b);
                return g switch
                {
                    0 => $"0:{b}",
                    1 => $"1:{DPadUrdlIndex(b):D2}",
                    2 => $"2:{FaceYbaxIndex(b):D2}",
                    _ => "9:"
                };
            }, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Sort extension rows: additions that include a face button first (YBAX), then D-pad-only (↑→↓←), then others.</summary>
    private static (int SortTier, int FaceKey, int DpadKey) ExtensionLineSortKeys(List<GamepadButtons> addedButtons)
    {
        var faceRanks = addedButtons.Select(FaceYbaxIndex).Where(i => i >= 0).ToList();
        var dpadRanks = addedButtons.Select(DPadUrdlIndex).Where(i => i >= 0).ToList();
        var faceKey = faceRanks.Count > 0 ? faceRanks.Min() : 99;
        var dpadKey = dpadRanks.Count > 0 ? dpadRanks.Min() : 99;
        int sortTier;
        if (faceRanks.Count > 0)
            sortTier = 0;
        else if (dpadRanks.Count > 0)
            sortTier = 1;
        else
            sortTier = 2;
        return (sortTier, faceKey, dpadKey);
    }

    private static string FormatHudButton(GamepadButtons b) => b switch
    {
        GamepadButtons.LeftShoulder => "LB",
        GamepadButtons.RightShoulder => "RB",
        GamepadButtons.LeftThumb => "LS",
        GamepadButtons.RightThumb => "RS",
        // Directional arrows — familiar on game UIs (avoid single letters that clash with face / keyboard).
        GamepadButtons.DPadUp => "\u2191",
        GamepadButtons.DPadRight => "\u2192",
        GamepadButtons.DPadDown => "\u2193",
        GamepadButtons.DPadLeft => "\u2190",
        _ => b.ToString()
    };

    /// <summary>Compact chord text for HUD (<see cref="GamepadBinding.Value"/> or normalized chord tokens).</summary>
    private static string FormatHudChordLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return label ?? string.Empty;

        var parts = label.Split(['+', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" + ", parts.Select(FormatHudChordSegment));
    }

    private static string FormatHudChordSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return segment;

        var s = segment.Trim();

        if (s.Equals("LeftTrigger", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("LT", StringComparison.OrdinalIgnoreCase))
            return "LT";
        if (s.Equals("RightTrigger", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("RT", StringComparison.OrdinalIgnoreCase))
            return "RT";

        if (s.Equals("LeftShoulder", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("LB", StringComparison.OrdinalIgnoreCase))
            return "LB";
        if (s.Equals("RightShoulder", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("RB", StringComparison.OrdinalIgnoreCase))
            return "RB";

        if (s.Equals("LeftThumb", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("LS", StringComparison.OrdinalIgnoreCase))
            return "LS";
        if (s.Equals("RightThumb", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("RS", StringComparison.OrdinalIgnoreCase))
            return "RS";

        if (s.Equals("DPadUp", StringComparison.OrdinalIgnoreCase))
            return "\u2191";
        if (s.Equals("DPadRight", StringComparison.OrdinalIgnoreCase))
            return "\u2192";
        if (s.Equals("DPadDown", StringComparison.OrdinalIgnoreCase))
            return "\u2193";
        if (s.Equals("DPadLeft", StringComparison.OrdinalIgnoreCase))
            return "\u2190";

        if (Enum.TryParse<GamepadButtons>(s, true, out var btn) && btn != GamepadButtons.None)
            return FormatHudButton(btn);

        return s;
    }

    private static string FormatHudOutput(string? token)
    {
        var t = token?.Trim() ?? string.Empty;
        return string.IsNullOrEmpty(t) ? "—" : t;
    }

    private static string FormatHudMappingOutput(MappingEntry mapping)
    {
        if (mapping.ItemCycle is { } ic)
        {
            var n = Math.Clamp(ic.SlotCount, 1, 9);
            var mods = ic.WithKeys is { Count: > 0 } ? string.Join("+", ic.WithKeys) + "+" : string.Empty;
            var dir = ic.Direction == ItemCycleDirection.Previous ? "prev" : "next";
            var fwd = ic.LoopForwardKey?.Trim() ?? string.Empty;
            var back = ic.LoopBackwardKey?.Trim() ?? string.Empty;
            if (fwd.Length > 0 && back.Length > 0)
                return $"{mods}{fwd}/{back} ({dir}, {n})";
            return $"{mods}1–{n} ({dir})";
        }

        if (mapping.TemplateToggle is { } tt)
        {
            var id = tt.AlternateProfileId?.Trim() ?? string.Empty;
            return id.Length > 0 ? $"Toggle → {id}" : "Toggle profile";
        }

        return string.IsNullOrWhiteSpace(mapping.KeyboardKey) ? string.Empty : mapping.KeyboardKey.Trim();
    }
}
