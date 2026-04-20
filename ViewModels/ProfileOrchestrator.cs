using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;

namespace Gamepad_Mapping.ViewModels;

/// <summary>
/// Orchestrates the lifecycle of profiles and templates.
/// Merges loading, saving, template switching, and inheritance logic.
/// </summary>
public partial class ProfileOrchestrator : ObservableObject
{
    private readonly IProfileService _profileService;
    private readonly IProcessTargetService _processTargetService;
    private string? _lastLoadedTemplateGroupIdForTargetInherit;

    /// <summary>Optional gate for template switches (unsaved workspace prompts).</summary>
    public IProfileSelectionInterlock? SelectionInterlock { get; set; }

    [ObservableProperty]
    private ObservableCollection<TemplateOption> _availableTemplates;

    private TemplateOption? _selectedTemplate;

    public TemplateOption? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (TemplateOption.MatchesStorageKey(_selectedTemplate, value))
            {
                if (!ReferenceEquals(_selectedTemplate, value) && value is not null)
                    SetProperty(ref _selectedTemplate, value);
                return;
            }

            if (SelectionInterlock?.AllowSelectTemplate(_selectedTemplate, value) == false)
            {
                SelectionInterlock.NotifySelectedTemplateBindingRefresh();
                return;
            }

            if (!SetProperty(ref _selectedTemplate, value))
                return;

            try
            {
                App.Logger.Info($"Switching to template: {value?.DisplayName} ({value?.StorageKey})");
                LoadSelectedTemplate();
                _profileService.PersistLastSelectedTemplateProfileId(_selectedTemplate?.StorageKey);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to load template '{value?.ProfileId ?? value?.TemplateGroupId}'", ex);
            }
        }
    }

    [ObservableProperty]
    private string _currentTemplateDisplayName = string.Empty;

    [ObservableProperty]
    private string _currentTemplateProfileId = string.Empty;

    [ObservableProperty]
    private string _currentTemplateTemplateGroupId = string.Empty;

    [ObservableProperty]
    private string _currentTemplateAuthor = string.Empty;

    [ObservableProperty]
    private string _currentTemplateCatalogFolder = string.Empty;

    [ObservableProperty]
    private string _currentTemplateCommunityListingDescription = string.Empty;

    [ObservableProperty]
    private string _templateTargetProcessName = string.Empty;

    [ObservableProperty]
    private List<string>? _comboLeadButtonsPersist;

    public event Action<GameProfileTemplate?>? TemplateLoaded;
    public event Action<string>? TemplateSwitchRequested;

    public ProfileOrchestrator(IProfileService profileService, IProcessTargetService processTargetService)
    {
        _profileService = profileService;
        _processTargetService = processTargetService;
        AvailableTemplates = _profileService.AvailableTemplates;
        
        SelectedTemplate = _profileService.ReloadTemplates(_profileService.LastSelectedTemplateProfileId);
    }

    public void LoadSelectedTemplate()
    {
        if (SelectedTemplate is null)
        {
            TemplateLoaded?.Invoke(null);
            return;
        }

        var template = _profileService.LoadSelectedTemplate(SelectedTemplate);
        if (template is null)
        {
            TemplateLoaded?.Invoke(null);
            return;
        }

        CurrentTemplateDisplayName = ResolveWorkspaceTemplateDisplayName(template);
        CurrentTemplateProfileId = template.ProfileId;
        CurrentTemplateTemplateGroupId = template.TemplateGroupId ?? string.Empty;
        CurrentTemplateAuthor = template.Author ?? string.Empty;
        CurrentTemplateCatalogFolder = template.TemplateCatalogFolder ?? string.Empty;
        CurrentTemplateCommunityListingDescription = template.CommunityListingDescription ?? string.Empty;
        ComboLeadButtonsPersist = template.ComboLeadButtons?.ToList();

        var fromFile = (template.TargetProcessName ?? string.Empty).Trim();
        var uiBefore = (TemplateTargetProcessName ?? string.Empty).Trim();

        if (fromFile.Length > 0)
            TemplateTargetProcessName = fromFile;
        else if (uiBefore.Length > 0
                 && ProfileService.ProfilesLikelyShareGameExecutable(_lastLoadedTemplateGroupIdForTargetInherit, template.EffectiveTemplateGroupId))
        {
            TemplateTargetProcessName = uiBefore;
            template.TargetProcessName = uiBefore;
            _profileService.SaveTemplate(template);
        }
        else
            TemplateTargetProcessName = string.Empty;

        _lastLoadedTemplateGroupIdForTargetInherit = template.EffectiveTemplateGroupId;

        TemplateLoaded?.Invoke(template);
    }

    public void RefreshTemplates(string? preferredProfileId = null)
    {
        SelectedTemplate = _profileService.ReloadTemplates(preferredProfileId);
    }

    public void RequestTemplateSwitch(string targetProfileId)
    {
        var id = (targetProfileId ?? string.Empty).Trim();
        if (id.Length == 0 || !_profileService.TryResolveTemplateLocation(id, out var loc))
            return;

        var opt = AvailableTemplates.FirstOrDefault(t => t.MatchesLocation(loc));
        if (opt is null || SelectedTemplate?.MatchesLocation(loc) == true)
            return;

        TemplateSwitchRequested?.Invoke(opt.DisplayName);
        SelectedTemplate = opt;
    }

    public void ReloadLocalizedContent()
    {
        var selectedProfileId = SelectedTemplate?.StorageKey;
        var reselected = _profileService.ReloadTemplates(selectedProfileId);
        OnPropertyChanged(nameof(AvailableTemplates));
        if (reselected is not null)
            SelectedTemplate = reselected;
    }

    /// <summary>Updates the identity header from the picker row and current UI language (after language change).</summary>
    public void RefreshCurrentIdentityDisplayNameForCulture(TranslationService ts)
    {
        if (SelectedTemplate is null)
            return;

        var baseline = (SelectedTemplate.DisplayNameBaseline ?? string.Empty).Trim();
        if (baseline.Length == 0)
            baseline = (SelectedTemplate.ProfileId ?? string.Empty).Trim();

        CurrentTemplateDisplayName = TemplateCatalogDisplayResolver.Resolve(
            baseline,
            SelectedTemplate.DisplayNames,
            string.IsNullOrWhiteSpace(SelectedTemplate.DisplayNameKey) ? null : SelectedTemplate.DisplayNameKey,
            ts);
    }

    private static string ResolveWorkspaceTemplateDisplayName(GameProfileTemplate template)
    {
        if (AppUiLocalization.TryTranslationService() is { } ts)
        {
            var baseline = string.IsNullOrWhiteSpace(template.DisplayName) ? template.ProfileId.Trim() : template.DisplayName.Trim();
            return TemplateCatalogDisplayResolver.Resolve(
                baseline,
                template.DisplayNames,
                string.IsNullOrWhiteSpace(template.DisplayNameKey) ? null : template.DisplayNameKey,
                ts);
        }

        return string.IsNullOrWhiteSpace(template.DisplayName) ? template.ProfileId : template.DisplayName.Trim();
    }
}


