#nullable enable

using System;
using System.Globalization;
using System.Text;

namespace GamepadMapperGUI.Utils.Text;

public static class UploadTextPolicyMatchNormalizer
{
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
        var anyRemoved = false;
        for (var i = 0; i < s.Length;)
        {
            if (!Rune.TryGetRuneAt(s, i, out var rune))
            {
                anyRemoved = true;
                i++;
                continue;
            }

            if (!IsRetainedRune(rune))
                anyRemoved = true;

            i += rune.Utf16SequenceLength;
        }

        if (!anyRemoved)
            return s;

        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length;)
        {
            if (!Rune.TryGetRuneAt(s, i, out var rune))
            {
                i++;
                continue;
            }

            i += rune.Utf16SequenceLength;
            if (IsRetainedRune(rune))
                sb.Append(rune);
        }

        return sb.ToString();
    }

    private static bool IsRetainedRune(Rune rune) =>
        IsLetterDigitOrMarkCategory(rune) || IsPreservedSeparatorWhitespace(rune);

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
