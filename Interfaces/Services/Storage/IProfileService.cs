using System;
using System.Collections.ObjectModel;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Storage;

public interface IProfileService
{
    string DefaultProfileId { get; }

    /// <summary>Last saved template reference from settings (filename stem at root, or <c>CatalogFolder/stem</c>).</summary>
    string? LastSelectedTemplateProfileId { get; }

    void PersistLastSelectedTemplateProfileId(string? storageKey);
    int ModifierGraceMs { get; }

    int LeadKeyReleaseSuppressMs { get; }
    ObservableCollection<TemplateOption> AvailableTemplates { get; }
    event EventHandler? ProfilesLoaded;
    string LoadTemplateDirectory();
    GameProfileTemplate LoadTemplate(string profileIdOrStorageKey);
    GameProfileTemplate LoadDefaultTemplate();
    TemplateOption? ReloadTemplates(string? preferredProfileIdOrStorageKey = null);
    TemplateOption? SelectTemplate(string? preferredProfileIdOrStorageKey = null);
    GameProfileTemplate? LoadSelectedTemplate(TemplateOption? selectedTemplate);
    bool TemplateExists(string profileIdOrStorageKey);

    /// <summary>
    /// Resolves a template reference to its storage location. Accepts a storage key, filename stem when unique,
    /// or <see cref="GameProfileTemplate.ProfileId"/> when it matches a single file.
    /// </summary>
    bool TryResolveTemplateLocation(string requestedId, out TemplateStorageLocation location);

    string CreateUniqueProfileId(string templateGroupId, string? displayName, string? catalogFolder = null);
    void SaveTemplate(GameProfileTemplate template, bool allowOverwrite = true);
    GamepadMapperGUI.Interfaces.Core.IValidationResult ValidateTemplate(GameProfileTemplate template);
    void DeleteTemplate(string profileIdOrStorageKey);
}

