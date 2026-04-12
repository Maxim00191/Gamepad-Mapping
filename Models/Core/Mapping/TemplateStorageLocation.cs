#nullable enable

namespace GamepadMapperGUI.Models;

/// <summary>Physical template file identity: optional one-level catalog folder under the templates root and JSON file stem.</summary>
public readonly record struct TemplateStorageLocation(string? CatalogSubfolder, string FileStem)
{
    public string StorageKey => TemplateStorageKey.Format(CatalogSubfolder, FileStem);

    public bool SameFileAs(TemplateStorageLocation other) =>
        string.Equals(FileStem, other.FileStem, StringComparison.OrdinalIgnoreCase)
        && string.Equals(CatalogSubfolder ?? string.Empty, other.CatalogSubfolder ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
}
