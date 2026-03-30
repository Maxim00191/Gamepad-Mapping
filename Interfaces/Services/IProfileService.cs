using System;
using System.Collections.ObjectModel;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services;

public interface IProfileService
{
    string DefaultGameId { get; }
    ObservableCollection<TemplateOption> AvailableTemplates { get; }
    event EventHandler? ProfilesLoaded;
    string LoadTemplateDirectory();
    GameProfileTemplate LoadTemplate(string profileId);
    GameProfileTemplate LoadDefaultTemplate();
    TemplateOption? ReloadTemplates(string? preferredProfileId = null);
    TemplateOption? SelectTemplate(string? preferredProfileId = null);
    GameProfileTemplate? LoadSelectedTemplate(TemplateOption? selectedTemplate);
    bool TemplateExists(string profileId);
    string CreateUniqueProfileId(string gameId, string? displayName);
    void SaveTemplate(GameProfileTemplate template, bool allowOverwrite = true);
    void DeleteTemplate(string profileId);
}
