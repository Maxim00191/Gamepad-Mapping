#nullable enable

using System.Globalization;
using System.Text;

namespace GamepadMapperGUI.Utils.Text;

public static class UploadTextPolicyMatchNormalizer
{
    /// <summary>
    /// Policy-comparable text: Unicode NFC, invariant lowercasing, and a few fullwidth→ASCII punctuation maps.
    /// The upload policy evaluator applies <see cref="UploadTextPolicyInterLetterNormalizer"/> afterward so needles
    /// and haystacks share the same inter-letter obfuscation rules without changing the substring matcher itself.
    /// </summary>
    public static string NormalizeForPolicyMatch(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        var n = s.Normalize(NormalizationForm.FormC).ToLowerInvariant();
        return UploadTextPunctuationCanonicalizer.Canonicalize(n);
    }

    public static bool IsWordContentRune(Rune rune) => IsLetterDigitOrMarkCategory(rune);

    private static bool IsLetterDigitOrMarkCategory(Rune rune)
    {
        var cat = Rune.GetUnicodeCategory(rune);
        return cat is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.DecimalDigitNumber
            or UnicodeCategory.LetterNumber
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;
    }
}
