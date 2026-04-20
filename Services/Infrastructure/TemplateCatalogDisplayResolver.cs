using System;
using System.Collections.Generic;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Resolves template JSON catalog text for the current UI language: per-culture maps and optional
/// resource keys (<c>descriptionKey</c>, <c>displayNameKey</c>), then the canonical baseline
/// string (<c>description</c>, <c>label</c>, <c>displayName</c>) which is the default (English) text.
/// Does not use arbitrary map entries as fallback.
/// </summary>
public static class TemplateCatalogDisplayResolver
{
    private static bool IsMissingLocalization(string value)
        => value.Length >= 2 && value[0] == '[' && value[^1] == ']';

    /// <summary>
    /// Resolves the string to show for the current <paramref name="translationService"/> culture.
    /// </summary>
    /// <param name="baselinePlain">Canonical default text from JSON (e.g. <c>description</c>).</param>
    /// <param name="perCultureMap">Optional <c>descriptions</c> / <c>labels</c> / <c>displayNames</c> map.</param>
    /// <param name="resourceKey">Optional <c>descriptionKey</c> / <c>displayNameKey</c> for .resx lookup.</param>
    public static string Resolve(
        string baselinePlain,
        IReadOnlyDictionary<string, string>? perCultureMap,
        string? resourceKey,
        TranslationService translationService)
    {
        ArgumentNullException.ThrowIfNull(translationService);

        if (CultureKeyedTextResolver.TryPickForUiCulture(perCultureMap, translationService.Culture, out var fromMap)
            && !string.IsNullOrWhiteSpace(fromMap))
            return fromMap.Trim();

        if (!string.IsNullOrWhiteSpace(resourceKey))
        {
            var localized = translationService[resourceKey];
            if (!IsMissingLocalization(localized))
                return localized.Trim();
        }

        return (baselinePlain ?? string.Empty).Trim();
    }
}
