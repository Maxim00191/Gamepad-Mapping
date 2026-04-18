#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models.Core.Community;
using GamepadMapperGUI.UploadTextPolicy;
using GamepadMapperGUI.Utils.Text;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class UploadTextPolicyEvaluator : ITextContentViolationEvaluator
{
    /// <summary>
    /// Caps UI-facing excerpts so long phrase matches do not overwhelm the compliance list.
    /// </summary>
    private const int MaxMatchedSegmentHintChars = 160;

    private readonly IReadOnlyList<UploadTextPolicyPattern> _patterns;
    private readonly string[] _normalizedNeedles;
    private readonly AhoCorasickMatcher? _aho;

    public UploadTextPolicyEvaluator()
    {
        _patterns = FinalizePatterns(LoadEmbeddedPatterns());
        _normalizedNeedles = BuildNormalizedNeedles(_patterns);
        _aho = _normalizedNeedles.Length > 0 ? new AhoCorasickMatcher(_normalizedNeedles) : null;
    }

    public UploadTextPolicyEvaluator(IEnumerable<UploadTextPolicyPattern> explicitPatterns)
    {
        ArgumentNullException.ThrowIfNull(explicitPatterns);
        _patterns = FinalizePatterns(NormalizePatterns(explicitPatterns));
        _normalizedNeedles = BuildNormalizedNeedles(_patterns);
        _aho = _normalizedNeedles.Length > 0 ? new AhoCorasickMatcher(_normalizedNeedles) : null;
    }

    public IReadOnlyList<TextContentViolationMatch> Evaluate(IReadOnlyList<TextContentInspectionField> fields)
    {
        if (fields.Count == 0 || _patterns.Count == 0 || _aho is null)
            return [];

        var results = new List<TextContentViolationMatch>();
        foreach (var field in fields)
        {
            var value = field.Value ?? string.Empty;
            if (value.Length == 0)
                continue;

            var haystack = Normalize(value);
            if (haystack.Length == 0)
                continue;

            var matched = new bool[_patterns.Count];
            var segStart = new int[_patterns.Count];
            var segEndExclusive = new int[_patterns.Count];
            for (var z = 0; z < segStart.Length; z++)
                segStart[z] = -1;

            _aho.Search(haystack, (patternIndex, endExclusive) =>
            {
                var len = _normalizedNeedles[patternIndex].Length;
                var start = endExclusive - len;
                if (start < 0)
                    return;

                var p = _patterns[patternIndex];
                if (IsWholeWordMode(p))
                {
                    if (!IsWholeWordAt(haystack, start, endExclusive))
                        return;
                }

                matched[patternIndex] = true;
                if (segStart[patternIndex] < 0 || start < segStart[patternIndex])
                {
                    segStart[patternIndex] = start;
                    segEndExclusive[patternIndex] = endExclusive;
                }
            });

            for (var i = 0; i < matched.Length; i++)
            {
                if (!matched[i])
                    continue;

                var hint = segStart[i] >= 0
                    ? TruncateMatchedSegmentHint(haystack.AsSpan(segStart[i], segEndExclusive[i] - segStart[i]))
                    : string.Empty;

                results.Add(new TextContentViolationMatch(
                    field.ContextLabel,
                    field.FieldCaption,
                    _patterns[i].Id,
                    MatchedSegmentHint: hint.Length > 0 ? hint : null,
                    ViolatingFieldText: UploadComplianceViolatingFieldText.PrepareForDisplay(value)));
                break;
            }
        }

        return results;
    }

    private static string TruncateMatchedSegmentHint(ReadOnlySpan<char> segment)
    {
        if (segment.Length <= MaxMatchedSegmentHintChars)
            return segment.ToString();

        return string.Concat(segment.Slice(0, MaxMatchedSegmentHintChars).ToString(), "\u2026");
    }

    private static IReadOnlyList<UploadTextPolicyPattern> LoadEmbeddedPatterns()
    {
        if (!UploadTextPolicyEmbeddedReader.TryReadPlaintextPolicyUtf8(out var text) || string.IsNullOrEmpty(text))
            return [];

        try
        {
            return NormalizePatterns(UploadTextPolicyTextParser.Parse(text));
        }
        catch
        {
            return [];
        }
    }

    private static string[] BuildNormalizedNeedles(IReadOnlyList<UploadTextPolicyPattern> patterns) =>
        patterns.Select(p => Normalize(p.Match)).ToArray();

    private static IReadOnlyList<UploadTextPolicyPattern> FinalizePatterns(IReadOnlyList<UploadTextPolicyPattern> raw)
    {
        if (raw.Count == 0)
            return raw;

        var filtered = raw.Where(p => Normalize(p.Match).Length > 0).ToList();
        return filtered.Count == raw.Count ? raw : filtered;
    }

    private static List<UploadTextPolicyPattern> NormalizePatterns(IEnumerable<UploadTextPolicyPattern> raw)
    {
        var list = new List<UploadTextPolicyPattern>();
        foreach (var p in raw)
        {
            var id = (p.Id ?? string.Empty).Trim();
            var match = (p.Match ?? string.Empty).Trim();
            if (id.Length == 0 || match.Length == 0)
                continue;

            var mode = (p.Mode ?? "contains").Trim();
            if (mode.Length == 0)
                mode = "contains";

            list.Add(new UploadTextPolicyPattern { Id = id, Match = match, Mode = mode });
        }

        return list;
    }

    private static string Normalize(string s) =>
        UploadTextPolicyMatchNormalizer.NormalizeForPolicyMatch(s);

    private static bool IsWholeWordMode(UploadTextPolicyPattern pattern) =>
        (pattern.Mode ?? string.Empty).Trim().Equals("wholeWord", StringComparison.OrdinalIgnoreCase);

    private static bool IsWholeWordAt(string haystack, int start, int endExclusive)
    {
        if (start < 0 || endExclusive > haystack.Length || endExclusive <= start)
            return false;

        return IsBoundaryBefore(haystack, start) && IsBoundaryAfter(haystack, endExclusive);
    }

    private static bool IsBoundaryBefore(string s, int index)
    {
        if (index <= 0)
            return true;
        return !TryGetLastRuneEndingBefore(s, index, out var r)
            || !UploadTextPolicyMatchNormalizer.IsWordContentRune(r);
    }

    private static bool IsBoundaryAfter(string s, int index)
    {
        if (index >= s.Length)
            return true;
        return !Rune.TryGetRuneAt(s, index, out var r) || !UploadTextPolicyMatchNormalizer.IsWordContentRune(r);
    }

    private static bool TryGetLastRuneEndingBefore(string s, int index, out Rune rune)
    {
        rune = default;
        if (index <= 0)
            return false;
        var i = index - 1;
        if (char.IsLowSurrogate(s[i]))
        {
            if (i < 1)
                return false;
            i--;
        }

        return Rune.TryGetRuneAt(s, i, out rune);
    }
}
