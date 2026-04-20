using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

public partial class ProfileTemplatePanelViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly IProfileService _profileService;

    public ProfileTemplatePanelViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _profileService = _mainViewModel.GetProfileService();
        _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
    }

    public event EventHandler? ConfigurationChanged;

    public ObservableCollection<TemplateOption> AvailableTemplates => _mainViewModel.AvailableTemplates;

    public TemplateOption? SelectedTemplate
    {
        get => _mainViewModel.SelectedTemplate;
        set => _mainViewModel.SelectedTemplate = value;
    }

    public string CurrentTemplateDisplayName
    {
        get => _mainViewModel.CurrentTemplateDisplayName;
        set => _mainViewModel.CurrentTemplateDisplayName = value;
    }

    public string CurrentTemplateProfileId
    {
        get => _mainViewModel.CurrentTemplateProfileId;
        set => _mainViewModel.CurrentTemplateProfileId = value;
    }

    public string CurrentTemplateTemplateGroupId
    {
        get => _mainViewModel.CurrentTemplateTemplateGroupId;
        set => _mainViewModel.CurrentTemplateTemplateGroupId = value;
    }

    public string CurrentTemplateAuthor
    {
        get => _mainViewModel.CurrentTemplateAuthor;
        set => _mainViewModel.CurrentTemplateAuthor = value;
    }

    public string CurrentTemplateCatalogFolder
    {
        get => _mainViewModel.CurrentTemplateCatalogFolder;
        set => _mainViewModel.CurrentTemplateCatalogFolder = value;
    }

    public string CurrentTemplateCommunityListingDescription
    {
        get => _mainViewModel.CurrentTemplateCommunityListingDescription;
        set => _mainViewModel.CurrentTemplateCommunityListingDescription = value;
    }

    public string TemplateTargetProcessName
    {
        get => _mainViewModel.TemplateTargetProcessName;
        set => _mainViewModel.TemplateTargetProcessName = value;
    }

    public int MappingCount => _mainViewModel.MappingCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProfileCommand))]
    private string newProfileTemplateGroupId = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProfileCommand))]
    private string newProfileDisplayName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProfileCommand))]
    private string newProfileId = string.Empty;

    [ObservableProperty]
    private string newProfileAuthor = string.Empty;

    [ObservableProperty]
    private string newProfileCatalogFolder = string.Empty;

    public bool CanCreateProfile => ProfileService.IsValidId(NewProfileTemplateGroupId) 
        && (string.IsNullOrWhiteSpace(NewProfileId) || ProfileService.IsValidId(NewProfileId));

    [RelayCommand]
    private void SaveProfile()
    {
        if (SelectedTemplate is null)
            return;

        if (!_mainViewModel.TryPersistWorkspaceTemplateToDisk(out var err))
        {
            var title = AppUiLocalization.GetString("WorkspaceSave_ErrorTitle");
            MessageBox.Show(
                string.Format(AppUiLocalization.GetString("WorkspaceSave_FailedMessage"), err ?? string.Empty),
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void DeleteSelectedProfile()
    {
        if (SelectedTemplate is null)
            return;

        if (string.Equals(SelectedTemplate.ProfileId, _profileService.DefaultProfileId, StringComparison.OrdinalIgnoreCase))
            return;

        var ok = MessageBox.Show(
            $"Delete profile '{SelectedTemplate.DisplayName}' ({SelectedTemplate.TemplateGroupId})?",
            "Confirm delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (ok != MessageBoxResult.Yes)
            return;

        try
        {
            _profileService.DeleteTemplate(SelectedTemplate.StorageKey);
            _mainViewModel.RefreshTemplates();
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to delete profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RefreshTemplatesFromDisk()
    {
        if (!_mainViewModel.ConfirmDiscardOrSaveBeforeTemplateMetadataRefresh())
            return;

        try
        {
            var keepId = SelectedTemplate?.StorageKey;
            _mainViewModel.RefreshTemplates(keepId);
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to reload templates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateProfile))]
    private void CreateProfile()
    {
        try
        {
            var templateGroupId = ProfileService.EnsureValidTemplateGroupId((NewProfileTemplateGroupId ?? string.Empty).Trim());
            var displayName = (NewProfileDisplayName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = templateGroupId;

            var catFolder = (NewProfileCatalogFolder ?? string.Empty).Trim();
            var catalogForCreate = string.IsNullOrEmpty(catFolder) ? null : catFolder;

            var profileId = string.IsNullOrWhiteSpace(NewProfileId)
                ? _profileService.CreateUniqueProfileId(templateGroupId, displayName, catalogForCreate)
                : ProfileService.EnsureValidProfileId(NewProfileId.Trim());

            var template = new GameProfileTemplate
            {
                SchemaVersion = 1,
                ProfileId = profileId,
                TemplateGroupId = templateGroupId,
                TemplateCatalogFolder = catalogForCreate,
                DisplayName = displayName,
                Author = NormalizeOptionalAuthor(NewProfileAuthor),
                Mappings = new List<MappingEntry>()
            };

            _profileService.SaveTemplate(template, allowOverwrite: false);
            var createdKey = TemplateStorageKey.Format(template.TemplateCatalogFolder, profileId);
            _mainViewModel.RefreshTemplates(createdKey);

            NewProfileTemplateGroupId = string.Empty;
            NewProfileDisplayName = string.Empty;
            NewProfileId = string.Empty;
            NewProfileAuthor = string.Empty;
            NewProfileCatalogFolder = string.Empty;
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string? NormalizeOptionalAuthor(string? value)
    {
        var t = (value ?? string.Empty).Trim();
        return t.Length == 0 ? null : t;
    }

    [RelayCommand]
    private void ReloadTemplate()
    {
        if (SelectedTemplate is null)
            return;

        if (!_mainViewModel.ConfirmDiscardOrSaveBeforeReloadFromDisk())
            return;

        _mainViewModel.ReloadSelectedTemplate();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MainViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.AvailableTemplates):
                OnPropertyChanged(nameof(AvailableTemplates));
                break;
            case nameof(MainViewModel.SelectedTemplate):
                OnPropertyChanged(nameof(SelectedTemplate));
                break;
            case nameof(MainViewModel.CurrentTemplateDisplayName):
                OnPropertyChanged(nameof(CurrentTemplateDisplayName));
                break;
            case nameof(MainViewModel.CurrentTemplateProfileId):
                OnPropertyChanged(nameof(CurrentTemplateProfileId));
                break;
            case nameof(MainViewModel.CurrentTemplateTemplateGroupId):
                OnPropertyChanged(nameof(CurrentTemplateTemplateGroupId));
                break;
            case nameof(MainViewModel.CurrentTemplateAuthor):
                OnPropertyChanged(nameof(CurrentTemplateAuthor));
                break;
            case nameof(MainViewModel.CurrentTemplateCatalogFolder):
                OnPropertyChanged(nameof(CurrentTemplateCatalogFolder));
                break;
            case nameof(MainViewModel.CurrentTemplateCommunityListingDescription):
                OnPropertyChanged(nameof(CurrentTemplateCommunityListingDescription));
                break;
            case nameof(MainViewModel.TemplateTargetProcessName):
                OnPropertyChanged(nameof(TemplateTargetProcessName));
                break;
            case nameof(MainViewModel.MappingCount):
                OnPropertyChanged(nameof(MappingCount));
                break;
        }
    }
}


