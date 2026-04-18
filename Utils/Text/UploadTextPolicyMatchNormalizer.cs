#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GamepadMapperGUI.Utils.Text;

public static class UploadTextPolicyMatchNormalizer
{
    /// <summary>
    /// Policy comparable text: Unicode NFC, invariant lowercasing, a few fullwidth→ASCII punctuation maps,
    /// then removal of insertions that sit between word characters (evasion stripping).
    /// </summary>
    /// <remarks>
    /// Punctuation and symbols between two letters/digits/marks are removed so dotted or spaced evasions
    /// still match the same needles. Leading or trailing decoration (e.g. "+q", "q:", "q：") is kept so
    /// dictionary phrases are not reduced to letters alone. Iteration collapses runs like "b..a" to "ba"
    /// in multiple passes. Emoji between letters are removed the same way.
    /// </remarks>
    public static string NormalizeForPolicyMatch(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        var n = s.Normalize(NormalizationForm.FormC).ToLowerInvariant();
        n = UploadTextPunctuationCanonicalizer.Canonicalize(n);
        return StripToPolicyComparable(n);
    }

    public static bool IsWordContentRune(Rune rune) => IsLetterDigitOrMarkCategory(rune);

    private static string StripToPolicyComparable(string s)
    {
        var runes = new List<Rune>();
        for (var i = 0; i < s.Length;)
        {
            if (!Rune.TryGetRuneAt(s, i, out var rune))
            {
                i++;
                continue;
            }

            i += rune.Utf16SequenceLength;
            runes.Add(rune);
        }

        if (runes.Count == 0)
            return string.Empty;

        var changed = true;
        while (changed)
        {
            changed = false;
            for (var k = 0; k < runes.Count; k++)
            {
                var r = runes[k];
                if (IsWordContentRune(r) || IsPreservedSeparatorWhitespace(r))
                    continue;

                if (!HasWordContentLeft(runes, k) || !HasWordContentRight(runes, k))
                    continue;

                runes.RemoveAt(k);
                changed = true;
                break;
            }
        }

        if (runes.Count == 0)
            return string.Empty;

        var sb = new StringBuilder(s.Length);
        foreach (var r in runes)
            sb.Append(r.ToString());

        return sb.ToString();
    }

    private static bool HasWordContentLeft(IReadOnlyList<Rune> runes, int index)
    {
        for (var j = index - 1; j >= 0; j--)
        {
            if (IsWordContentRune(runes[j]))
                return true;
        }

        return false;
    }

    private static bool HasWordContentRight(IReadOnlyList<Rune> runes, int index)
    {
        for (var j = index + 1; j < runes.Count; j++)
        {
            if (IsWordContentRune(runes[j]))
                return true;
        }

        return false;
    }

    private static bool IsPreservedSeparatorWhitespace(Rune rune)
    {
        if (rune.Value is '\t' or '\n' or '\r' or '\f' or '\v')
            return true;

        var cat = Rune.GetUnicodeCategory(rune);
        return cat is UnicodeCategory.SpaceSeparator
            or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator;
    }

    private static bool IsLetterDigitOrMarkCategory(Rune rune)
    {
        var cat = Rune.GetUnicodeCategory(rune);
        return cat is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.DecimalDigitNumber
            or UnicodeCategory.LetterNumber
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;
    }
}
