#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Bilingual template text editing: primary field follows the app UI language (Chinese or English);
/// the other language is optional. JSON baseline (<c>description</c>, <c>displayName</c>) stores the default English
/// string; <c>zh-CN</c> lives in the map when present.
/// </summary>
public static class UiCultureDescriptionPair
{
    public static bool IsChinesePrimaryUi(CultureInfo uiCulture) =>
        uiCulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    /// <summary>Culture key for the field that matches the current UI language.</summary>
    public static string PrimaryLocaleKey(CultureInfo uiCulture) =>
        IsChinesePrimaryUi(uiCulture) ? TemplateLocaleKeys.ZhCn : TemplateLocaleKeys.EnUs;

    /// <summary>Culture key for the optional secondary field (the other supported language).</summary>
    public static string SecondaryLocaleKey(CultureInfo uiCulture) =>
        IsChinesePrimaryUi(uiCulture) ? TemplateLocaleKeys.EnUs : TemplateLocaleKeys.ZhCn;

    public static string ReadPrimary(
        IReadOnlyDictionary<string, string>? descriptions,
        string baseline,
        CultureInfo uiCulture)
    {
        var key = PrimaryLocaleKey(uiCulture);
        var fromMap = LocalizedCultureStringMap.Get(descriptions, key);
        if (!string.IsNullOrWhiteSpace(fromMap))
            return fromMap.Trim();
        return (baseline ?? string.Empty).Trim();
    }

    public static string ReadSecondary(
        IReadOnlyDictionary<string, string>? descriptions,
        string baseline,
        CultureInfo uiCulture)
    {
        var key = SecondaryLocaleKey(uiCulture);
        var fromMap = LocalizedCultureStringMap.Get(descriptions, key);
        if (!string.IsNullOrWhiteSpace(fromMap))
            return fromMap.Trim();
        if (IsChinesePrimaryUi(uiCulture))
            return (baseline ?? string.Empty).Trim();
        return string.Empty;
    }

    /// <summary>
    /// Writes <c>zh-CN</c> from the Chinese line and sets <paramref name="baseline"/> to the English default.
    /// </summary>
    public static void WritePair(
        ref Dictionary<string, string>? descriptions,
        ref string baseline,
        CultureInfo uiCulture,
        string primaryText,
        string secondaryText)
    {
        var p = (primaryText ?? string.Empty).Trim();
        var s = (secondaryText ?? string.Empty).Trim();

        var english = IsChinesePrimaryUi(uiCulture) ? s : p;
        var chinese = IsChinesePrimaryUi(uiCulture) ? p : s;

        baseline = english;

        descriptions = LocalizedCultureStringMap.WithCulture(descriptions, TemplateLocaleKeys.ZhCn,
            string.IsNullOrEmpty(chinese) ? null : chinese);
        descriptions = LocalizedCultureStringMap.WithCulture(descriptions, TemplateLocaleKeys.EnUs, null);
    }
}
