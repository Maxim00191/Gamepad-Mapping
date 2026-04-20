using System;
using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Computes per–UI-culture display strings for template catalog rows from JSON baseline fields
/// (<c>description</c>, <c>label</c>, <c>displayName</c>), optional <c>descriptions</c> maps, and resource keys.
/// Does not overwrite serialized baseline fields so language switching stays accurate.
/// </summary>
public static class CatalogDescriptionLocalizer
{
    public static void ApplyKeyboardAction(KeyboardActionDefinition action, TranslationService translationService)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(translationService);

        var resolved = TemplateCatalogDisplayResolver.Resolve(
            action.Description ?? string.Empty,
            action.Descriptions,
            action.DescriptionKey,
            translationService);

        if (action.ResolvedCatalogDescription != resolved)
            action.ResolvedCatalogDescription = resolved;
    }

    public static void ApplyMappingDescription(MappingEntry mapping, TranslationService translationService)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(translationService);

        var resolved = TemplateCatalogDisplayResolver.Resolve(
            mapping.Description ?? string.Empty,
            mapping.Descriptions,
            mapping.DescriptionKey,
            translationService);

        if (mapping.ResolvedCatalogDescription != resolved)
            mapping.ResolvedCatalogDescription = resolved;
    }

    public static void ApplyRadialMenu(RadialMenuDefinition radialMenu, TranslationService translationService)
    {
        ArgumentNullException.ThrowIfNull(radialMenu);
        ArgumentNullException.ThrowIfNull(translationService);

        var resolved = TemplateCatalogDisplayResolver.Resolve(
            radialMenu.DisplayName ?? string.Empty,
            radialMenu.DisplayNames,
            resourceKey: null,
            translationService);

        if (radialMenu.ResolvedDisplayName != resolved)
            radialMenu.ResolvedDisplayName = resolved;

        if (radialMenu.Items is null)
            return;

        foreach (var item in radialMenu.Items)
            ApplyRadialMenuItem(item, translationService);
    }

    public static void ApplyRadialMenuItem(RadialMenuItem item, TranslationService translationService)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(translationService);

        var resolved = TemplateCatalogDisplayResolver.Resolve(
            item.Label ?? string.Empty,
            item.Labels,
            resourceKey: null,
            translationService);

        if (item.ResolvedLabel != resolved)
            item.ResolvedLabel = resolved;

        item.NotifyEditorLabelFieldsChanged();
    }

    /// <summary>
    /// Localizes all catalog-facing strings on a freshly deserialized template (before resolver passes).
    /// </summary>
    public static void ApplyLoadedTemplate(GameProfileTemplate template, TranslationService translationService)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(translationService);

        if (template.KeyboardActions is { Count: > 0 })
        {
            foreach (var action in template.KeyboardActions)
                ApplyKeyboardAction(action, translationService);
        }

        foreach (var mapping in template.Mappings)
            ApplyMappingDescription(mapping, translationService);

        if (template.RadialMenus is { Count: > 0 })
        {
            foreach (var rm in template.RadialMenus)
                ApplyRadialMenu(rm, translationService);
        }
    }

    /// <summary>
    /// Re-applies localization to in-memory catalog rows (e.g. after the UI language changes).
    /// </summary>
    public static void ApplyOpenTemplateCollections(
        IEnumerable<KeyboardActionDefinition> keyboardActions,
        IEnumerable<MappingEntry> mappings,
        IEnumerable<RadialMenuDefinition> radialMenus,
        TranslationService translationService)
    {
        ArgumentNullException.ThrowIfNull(translationService);

        foreach (var a in keyboardActions)
            ApplyKeyboardAction(a, translationService);

        foreach (var m in mappings)
            ApplyMappingDescription(m, translationService);

        foreach (var rm in radialMenus)
            ApplyRadialMenu(rm, translationService);
    }

    /// <summary>
    /// Resolves template picker labels from baseline <c>displayName</c>, <c>displayNames</c>, and optional folder maps.
    /// </summary>
    public static void ApplyTemplateOption(TemplateOption option, TranslationService translationService)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(translationService);

        var baselineName = (option.DisplayNameBaseline ?? string.Empty).Trim();
        if (baselineName.Length == 0)
            baselineName = (option.ProfileId ?? string.Empty).Trim();

        var resolvedName = TemplateCatalogDisplayResolver.Resolve(
            baselineName,
            option.DisplayNames,
            string.IsNullOrWhiteSpace(option.DisplayNameKey) ? null : option.DisplayNameKey,
            translationService);

        var folderBaseline = (option.CatalogSubfolder ?? string.Empty).Trim();
        var resolvedFolder = folderBaseline.Length == 0
            ? string.Empty
            : TemplateCatalogDisplayResolver.Resolve(
                folderBaseline,
                option.CatalogFolderDisplayNames,
                resourceKey: null,
                translationService);

        if (option.ResolvedDisplayName != resolvedName)
            option.ResolvedDisplayName = resolvedName;
        if (option.ResolvedCatalogFolderLabel != resolvedFolder)
            option.ResolvedCatalogFolderLabel = resolvedFolder;
    }

    /// <summary>Re-resolves every template picker row (e.g. after UI language change).</summary>
    public static void ApplyTemplateCatalogPicker(
        IEnumerable<TemplateOption> templates,
        TranslationService translationService)
    {
        ArgumentNullException.ThrowIfNull(translationService);
        foreach (var t in templates)
            ApplyTemplateOption(t, translationService);
    }
}
