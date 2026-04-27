#nullable enable

using System;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Resolves community <c>index.json</c> template titles for the current UI language using the same rules as
/// built-in catalog rows (<see cref="TemplateCatalogDisplayResolver"/>).
/// </summary>
public static class CommunityTemplateDisplayLabels
{
    /// <summary>
    /// Baseline <see cref="CommunityTemplateInfo.DisplayName"/> from the index; falls back to <see cref="CommunityTemplateInfo.Id"/> when empty.
    /// </summary>
    public static string ResolveDisplayName(CommunityTemplateInfo info, TranslationService? translationService)
    {
        ArgumentNullException.ThrowIfNull(info);

        var baseline = string.IsNullOrWhiteSpace(info.DisplayName)
            ? (info.Id ?? string.Empty).Trim()
            : info.DisplayName.Trim();

        if (translationService is null)
            return baseline;

        return TemplateCatalogDisplayResolver.Resolve(
            baseline,
            info.DisplayNames,
            string.IsNullOrWhiteSpace(info.DisplayNameKey) ? null : info.DisplayNameKey,
            translationService);
    }

    /// <summary>
    /// Resolves a <see cref="GameProfileTemplate"/> title for UI (upload bundle rows, etc.) given a precomputed
    /// baseline label (e.g. display name, or profile id / storage key fallback).
    /// </summary>
    public static string ResolveGameProfileTemplateTitle(
        GameProfileTemplate template,
        string baselineTitle,
        TranslationService? translationService)
    {
        ArgumentNullException.ThrowIfNull(template);

        var baseline = (baselineTitle ?? string.Empty).Trim();
        if (translationService is null)
            return baseline;

        return TemplateCatalogDisplayResolver.Resolve(
            baseline,
            template.DisplayNames,
            string.IsNullOrWhiteSpace(template.DisplayNameKey) ? null : template.DisplayNameKey,
            translationService);
    }
}
