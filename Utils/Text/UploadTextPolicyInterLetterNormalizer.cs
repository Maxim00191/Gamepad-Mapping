#nullable enable

using System;
using System.Globalization;
using System.Text;

namespace GamepadMapperGUI.Utils.Text;

/// <summary>
/// Collapses inter-letter obfuscation (dots, slashes, stars, etc.) on policy-normalized text so that
/// Aho–Corasick needles and haystacks stay aligned without changing the automaton implementation.
/// </summary>
public static class UploadTextPolicyInterLetterNormalizer
{
    /// <summary>
    /// Symbols removed (in addition to <c>.</c> when enabled) between two Unicode letters.
    /// Commas, colons, and similar punctuation stay so phrases like <c>x,y</c> continue to work.
    /// </summary>
    private static bool IsAlwaysCollapsedBetweenLetters(Rune r) =>
        r.Value is '*' or '_' or '-' or '/' or '\\'
        || r.Value is '\u00B7' or '\u2022' or '\u2026'; // middle dot, bullet, ellipsis

    /// <summary>
    /// Returns true when the pattern should not be indexed: <c>*</c> between letters (e.g. <c>f*ck</c>)
    /// does not collapse to a trustworthy needle.
    /// </summary>
    public static bool ShouldDiscardPatternDueToInterLetterAsterisk(string normalizedForPolicyMatch)
    {
        if (string.IsNullOrEmpty(normalizedForPolicyMatch))
            return false;

        Rune? prevLetter = null;
        var i = 0;
        while (i < normalizedForPolicyMatch.Length)
        {
            if (!Rune.TryGetRuneAt(normalizedForPolicyMatch, i, out var rune))
                break;
            i += rune.Utf16SequenceLength;

            if (IsUnicodeLetter(rune))
            {
                prevLetter = rune;
                continue;
            }

            if (rune.Value == '*' && prevLetter is not null)
            {
                var j = i;
                while (j < normalizedForPolicyMatch.Length)
                {
                    if (!Rune.TryGetRuneAt(normalizedForPolicyMatch, j, out var next))
                        break;
                    if (IsWhitespaceRune(next))
                        break;
                    if (IsUnicodeLetter(next))
                        return true;
                    j += next.Utf16SequenceLength;
                }
            }

            if (!IsWhitespaceRune(rune))
                prevLetter = null;
        }

        return false;
    }

    /// <summary>
    /// Removes obfuscation characters between letters. Input must already be
    /// <see cref="UploadTextPolicyMatchNormalizer.NormalizeForPolicyMatch"/> output.
    /// </summary>
    public static string CollapseInterLetterObfuscation(string normalizedForPolicyMatch)
    {
        if (string.IsNullOrEmpty(normalizedForPolicyMatch))
            return normalizedForPolicyMatch;

        var sb = new StringBuilder(normalizedForPolicyMatch.Length);
        var tokenStart = 0;
        for (var idx = 0; idx <= normalizedForPolicyMatch.Length; idx++)
        {
            var atEnd = idx == normalizedForPolicyMatch.Length;
            var isWs = !atEnd && char.IsWhiteSpace(normalizedForPolicyMatch[idx]);
            if (!atEnd && !isWs)
                continue;

            if (idx > tokenStart)
            {
                var token = normalizedForPolicyMatch.AsSpan(tokenStart, idx - tokenStart);
                AppendCollapsedToken(sb, token);
            }

            if (!atEnd)
                sb.Append(normalizedForPolicyMatch[idx]);

            tokenStart = idx + 1;
        }

        return sb.ToString();
    }

    private static void AppendCollapsedToken(StringBuilder sb, ReadOnlySpan<char> token)
    {
        if (token.Length == 0)
            return;

        var t = new string(token);
        var dotGapCount = CountLetterToLetterDotGaps(t);
        var collapseDotsBetweenLetters = dotGapCount >= 2 || HasDenseDotGapBetweenLetters(t);

        var i = 0;
        var lastAppendedWasLetter = false;
        while (i < t.Length)
        {
            if (!Rune.TryGetRuneAt(t, i, out var rune))
                break;
            var len = rune.Utf16SequenceLength;

            var hasNextLetter = NextUnicodeLetterIndex(t, i + len, out _) >= 0;

            var skip = false;
            if (lastAppendedWasLetter && hasNextLetter)
            {
                if (collapseDotsBetweenLetters && rune.Value == '.')
                    skip = true;
                else if (IsAlwaysCollapsedBetweenLetters(rune))
                    skip = true;
            }

            if (!skip)
            {
                sb.Append(t, i, len);
                lastAppendedWasLetter = IsUnicodeLetter(rune);
            }

            i += len;
        }
    }

    /// <summary>
    /// Counts overlapping letter — [one or more dots] — letter chains in a token.
    /// One chain (<c>file.txt</c>) → do not collapse dots; two or more (<c>f.u.c.k</c>) → collapse.
    /// </summary>
    private static int CountLetterToLetterDotGaps(string token)
    {
        var count = 0;
        var pos = 0;
        while (pos < token.Length)
        {
            var letterStart = NextUnicodeLetterIndex(token, pos, out var letterLen);
            if (letterStart < 0)
                break;

            var afterLetter = letterStart + letterLen;
            var p = afterLetter;
            var dots = 0;
            while (p < token.Length && Rune.TryGetRuneAt(token, p, out var r) && r.Value == '.')
            {
                dots++;
                p += r.Utf16SequenceLength;
            }

            if (dots > 0 && NextUnicodeLetterIndex(token, p, out _) >= 0)
                count++;

            pos = afterLetter;
        }

        return count;
    }

    /// <summary>
    /// True when some letter-to-letter gap uses two or more consecutive periods (e.g. <c>f...u</c>),
    /// so single-period file extensions can stay intact while heavy dot obfuscation still collapses.
    /// </summary>
    private static bool HasDenseDotGapBetweenLetters(string token)
    {
        var pos = 0;
        while (pos < token.Length)
        {
            var letterStart = NextUnicodeLetterIndex(token, pos, out var letterLen);
            if (letterStart < 0)
                break;

            var p = letterStart + letterLen;
            var dots = 0;
            while (p < token.Length && Rune.TryGetRuneAt(token, p, out var r) && r.Value == '.')
            {
                dots++;
                p += r.Utf16SequenceLength;
            }

            if (dots >= 2 && NextUnicodeLetterIndex(token, p, out _) >= 0)
                return true;

            pos = letterStart + letterLen;
        }

        return false;
    }

    /// <summary>Returns UTF-16 index of next Unicode letter at or after <paramref name="startUtf16"/>; otherwise -1.</summary>
    private static int NextUnicodeLetterIndex(string token, int startUtf16, out int letterUtf16Length)
    {
        letterUtf16Length = 0;
        var j = startUtf16;
        while (j < token.Length)
        {
            if (!Rune.TryGetRuneAt(token, j, out var r))
                break;
            if (IsUnicodeLetter(r))
            {
                letterUtf16Length = r.Utf16SequenceLength;
                return j;
            }

            j += r.Utf16SequenceLength;
        }

        return -1;
    }

    private static bool IsWhitespaceRune(Rune r)
    {
        if (r.IsAscii && r.Value < 0x100)
            return char.IsWhiteSpace((char)r.Value);

        return Rune.GetUnicodeCategory(r) is UnicodeCategory.SpaceSeparator
            or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator;
    }

    private static bool IsUnicodeLetter(Rune r)
    {
        var cat = Rune.GetUnicodeCategory(r);
        return cat is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter;
    }
}
