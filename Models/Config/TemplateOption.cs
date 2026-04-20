#nullable enable

using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GamepadMapperGUI.Models;

/// <summary>
/// One row in the profile template picker. Carries baseline JSON fields plus
/// <see cref="ResolvedDisplayName"/> / folder labels resolved for the current UI language (hot-swappable).
/// </summary>
public partial class TemplateOption : ObservableObject
{
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Relative catalog folder under the templates root (<c>/</c>-separated), or null/empty for JSON files in the templates root.</summary>
    public string? CatalogSubfolder { get; set; }

    /// <summary>Effective game-group id for this row (see <see cref="GameProfileTemplate.EffectiveTemplateGroupId"/>).</summary>
    public string TemplateGroupId { get; set; } = string.Empty;

    /// <summary>Canonical <c>displayName</c> from template JSON (sorting / save semantics).</summary>
    public string DisplayNameBaseline { get; set; } = string.Empty;

    /// <summary>Optional <c>displayNames</c> map from template JSON.</summary>
    public Dictionary<string, string>? DisplayNames { get; set; }

    /// <summary>Optional <c>displayNameKey</c> from template JSON.</summary>
    public string DisplayNameKey { get; set; } = string.Empty;

    /// <summary>Optional <c>templateCatalogFolderNames</c> map from template JSON.</summary>
    public Dictionary<string, string>? CatalogFolderDisplayNames { get; set; }

    [ObservableProperty]
    private string _resolvedDisplayName = string.Empty;

    [ObservableProperty]
    private string _resolvedCatalogFolderLabel = string.Empty;

    /// <summary>Localized title for picker, toggles, and dialogs; mirrors legacy use of a single <c>DisplayName</c> field.</summary>
    public string DisplayName
    {
        get => ResolvedDisplayName;
        set
        {
            var v = value ?? string.Empty;
            DisplayNameBaseline = v;
            ResolvedDisplayName = v;
        }
    }

    partial void OnResolvedDisplayNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(TemplatePickerLabel));
    }

    partial void OnResolvedCatalogFolderLabelChanged(string value) =>
        OnPropertyChanged(nameof(TemplatePickerLabel));

    public string Author { get; set; } = string.Empty;

    public List<RadialMenuDefinition>? RadialMenus { get; set; }

    public string StorageKey => TemplateStorageKey.Format(CatalogSubfolder, ProfileId);

    public string TemplatePickerLabel =>
        string.IsNullOrEmpty(CatalogSubfolder)
            ? ResolvedDisplayName
            : $"{ResolvedDisplayName}  ({ResolvedCatalogFolderLabel})";

    public bool MatchesLocation(TemplateStorageLocation location) =>
        new TemplateStorageLocation(CatalogSubfolder, ProfileId).SameFileAs(location);

    public override string ToString() => ResolvedDisplayName;
}
