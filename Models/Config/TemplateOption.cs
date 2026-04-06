namespace GamepadMapperGUI.Models;

public class TemplateOption
{
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Single-level catalog folder under the templates root, or null/empty for templates in the root directory.</summary>
    public string? CatalogSubfolder { get; set; }

    public string TemplateGroupId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public List<RadialMenuDefinition>? RadialMenus { get; set; }

    public string StorageKey => TemplateStorageKey.Format(CatalogSubfolder, ProfileId);

    public string TemplatePickerLabel =>
        string.IsNullOrEmpty(CatalogSubfolder)
            ? DisplayName
            : $"{DisplayName}  ({CatalogSubfolder})";

    public bool MatchesLocation(TemplateStorageLocation location) =>
        new TemplateStorageLocation(CatalogSubfolder, ProfileId).SameFileAs(location);

    public override string ToString() => DisplayName;
}
