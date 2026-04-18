#nullable enable

using System.Globalization;
using System.Text;

namespace GamepadMapperGUI.Utils.Text;

internal static class UploadLinkTextNormalizer
{
    /// <summary>
    /// Normalizes text for link detection:
    /// 1) Unicode compatibility composition (FormKC) for full-width/half-width unification.
    /// 2) Invariant lowercase.
    /// 3) Remove non-spacing and related combining marks to fold accented variants.
    /// </summary>
    public static string NormalizeForLinkDetection(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var compatibility = input.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var decomposed = compatibility.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        for (var i = 0; i < decomposed.Length;)
        {
            if (!Rune.TryGetRuneAt(decomposed, i, out var rune))
            {
                i++;
                continue;
            }

            i += rune.Utf16SequenceLength;
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            sb.Append(rune.ToString());
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
