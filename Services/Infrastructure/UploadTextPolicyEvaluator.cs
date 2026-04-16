#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models.Core.Community;
using GamepadMapperGUI.Utils.Text;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class UploadTextPolicyEvaluator : ITextContentViolationEvaluator
{
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
            });

            for (var i = 0; i < matched.Length; i++)
            {
                if (!matched[i])
                    continue;

                results.Add(new TextContentViolationMatch(
                    field.ContextLabel,
                    field.FieldCaption,
                    _patterns[i].Id));
                break;
            }
        }

        return results;
    }

    private static IReadOnlyList<UploadTextPolicyPattern> LoadEmbeddedPatterns()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var keyStream = asm.GetManifestResourceStream(UploadTextPolicyPayloadCodec.XorKeyResourceName);
            using var payloadStream = asm.GetManifestResourceStream(UploadTextPolicyPayloadCodec.ObfuscatedGzipResourceName);
            if (keyStream is null || payloadStream is null)
                return [];

            var key = ReadAllBytes(keyStream);
            if (key.Length == 0)
                return [];

            var obfuscated = ReadAllBytes(payloadStream);
            UploadTextPolicyPayloadCodec.ApplyXor(obfuscated, key);

            using var ms = new MemoryStream(obfuscated, writable: false);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var reader = new StreamReader(gz, Encoding.UTF8);
            var text = reader.ReadToEnd();
            return NormalizePatterns(UploadTextPolicyTextParser.Parse(text));
        }
        catch
        {
            return [];
        }
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
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
