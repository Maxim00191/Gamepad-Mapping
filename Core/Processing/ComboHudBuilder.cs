using System;
using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

internal static class ComboHudBuilder
{
    public static ComboHudContent? BuildModifierPrefixHud(
        Func<bool> canDispatchOutput,
        IReadOnlyCollection<GamepadButtons> activeButtons,
        IReadOnlyList<MappingEntry> mappingsSnapshot)
    {
        if (!canDispatchOutput())
            return null;

        if (activeButtons is null || activeButtons.Count == 0)
            return null;

        var aChord = activeButtons.OrderBy(b => b.ToString(), StringComparer.OrdinalIgnoreCase).ToList();
        const bool aReqRt = false;
        const bool aReqLt = false;

        var lines = CollectChordExtensionLines(aChord, aReqRt, aReqLt, mappingsSnapshot);
        if (lines.Count == 0)
            return null;

        var title = string.Join(" + ", aChord.Select(b => b.ToString()));
        return new ComboHudContent(title, lines);
    }

    public static ComboHudContent BuildComboHud(HoldSessionManager.HoldSession session, IReadOnlyList<MappingEntry> mappings)
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

        var lines = new List<ComboHudLine>
        {
            new($"Short press → {FormatHudOutput(session.ShortKeyToken)}", null),
            new($"Hold {session.HoldThresholdMs} ms → {FormatHudOutput(session.HoldKeyToken)}", null)
        };

        lines.AddRange(CollectChordExtensionLines(
            session.ChordButtons,
            session.RequiresRightTrigger,
            session.RequiresLeftTrigger,
            mappings));

        var title = session.SourceToken;
        if (!string.IsNullOrWhiteSpace(holdEntry?.Description))
            title = $"{session.SourceToken}  ·  {holdEntry.Description}";

        return new ComboHudContent(title, lines);
    }

    public static List<ComboHudLine> CollectChordExtensionLines(
        List<GamepadButtons> baseChord,
        bool baseReqRt,
        bool baseReqLt,
        IReadOnlyList<MappingEntry> mappings)
    {
        var acc = new List<(int Spec, ComboHudLine Line)>();
        foreach (var mapping in mappings)
        {
            if (mapping?.From is null || mapping.From.Type != GamepadBindingType.Button)
                continue;
            if (!ChordResolver.TryParseButtonChord(mapping.From.Value, out var chord, out var reqRt, out var reqLt, out var normTok))
                continue;
            if (!ChordResolver.IsOtherChordStrictlyMoreSpecific(baseChord, baseReqRt, baseReqLt, chord, reqRt, reqLt))
                continue;
            if (mapping.Trigger == TriggerMoment.Released)
                continue;
            if (HoldSessionManager.IsHoldDualMapping(mapping))
                continue;

            var spec = ChordResolver.ChordSpecificity(chord, reqRt, reqLt);
            var comboLabel = mapping.From.Value ?? normTok;
            var keyPart = string.IsNullOrWhiteSpace(mapping.KeyboardKey) ? string.Empty : mapping.KeyboardKey.Trim();
            var descPart = mapping.Description?.Trim();
            var detail = string.IsNullOrEmpty(descPart)
                ? keyPart
                : string.IsNullOrEmpty(keyPart)
                    ? descPart
                    : $"{descPart} → {keyPart}";
            acc.Add((spec, new ComboHudLine(comboLabel, string.IsNullOrWhiteSpace(detail) ? null : detail)));
        }

        return acc.OrderBy(e => e.Spec).ThenBy(e => e.Line.Primary, StringComparer.OrdinalIgnoreCase).Select(e => e.Line).ToList();
    }

    private static string FormatHudOutput(string? token)
    {
        var t = token?.Trim() ?? string.Empty;
        return string.IsNullOrEmpty(t) ? "—" : t;
    }
}
