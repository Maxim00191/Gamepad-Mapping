using System;
using System.Collections.ObjectModel;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services;

public interface IProfileService
{
    string DefaultProfileId { get; }

    /// <summary>Last saved template profile id from settings; used to pre-select on startup.</summary>
    string? LastSelectedTemplateProfileId { get; }

    void PersistLastSelectedTemplateProfileId(string? profileId);
    int ModifierGraceMs { get; }

    int LeadKeyReleaseSuppressMs { get; }
    ObservableCollection<TemplateOption> AvailableTemplates { get; }
    event EventHandler? ProfilesLoaded;
    string LoadTemplateDirectory();
    GameProfileTemplate LoadTemplate(string profileId);
    GameProfileTemplate LoadDefaultTemplate();
    TemplateOption? ReloadTemplates(string? preferredProfileId = null);
    TemplateOption? SelectTemplate(string? preferredProfileId = null);
    GameProfileTemplate? LoadSelectedTemplate(TemplateOption? selectedTemplate);
    bool TemplateExists(string profileId);
    string CreateUniqueProfileId(string templateGroupId, string? displayName);
    void SaveTemplate(GameProfileTemplate template, bool allowOverwrite = true);
    GamepadMapperGUI.Interfaces.Core.IValidationResult ValidateTemplate(GameProfileTemplate template);
    void DeleteTemplate(string profileId);
}
