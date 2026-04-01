using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services;

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

    public string TemplateTargetProcessName
    {
        get => _mainViewModel.TemplateTargetProcessName;
        set => _mainViewModel.TemplateTargetProcessName = value;
    }

    public int MappingCount => _mainViewModel.MappingCount;

    [ObservableProperty]
    private string newProfileTemplateGroupId = string.Empty;

    [ObservableProperty]
    private string newProfileDisplayName = string.Empty;

    private ICommand? _saveProfileCommand;
    public ICommand SaveProfileCommand => _saveProfileCommand ??= new RelayCommand(SaveProfile);

    private ICommand? _deleteSelectedProfileCommand;
    public ICommand DeleteSelectedProfileCommand => _deleteSelectedProfileCommand ??= new RelayCommand(DeleteSelectedProfile);

    private ICommand? _createProfileCommand;
    public ICommand CreateProfileCommand => _createProfileCommand ??= new RelayCommand(CreateProfile);

    private ICommand? _reloadTemplateCommand;
    public ICommand ReloadTemplateCommand => _reloadTemplateCommand ??= new RelayCommand(ReloadTemplate);

    private void SaveProfile()
    {
        if (SelectedTemplate is null)
            return;

        List<string>? comboLeads = null;
        if (_mainViewModel.ComboLeadButtonsPersist is not null)
            comboLeads = new List<string>(_mainViewModel.ComboLeadButtonsPersist);

        var targetProc = (_mainViewModel.TemplateTargetProcessName ?? string.Empty).Trim();
        var template = new GameProfileTemplate
        {
            SchemaVersion = 1,
            ProfileId = SelectedTemplate.ProfileId,
            TemplateGroupId = SelectedTemplate.TemplateGroupId,
            DisplayName = CurrentTemplateDisplayName,
            TargetProcessName = string.IsNullOrEmpty(targetProc) ? null : targetProc,
            ComboLeadButtons = comboLeads,
            Mappings = _mainViewModel.Mappings.ToList()
        };

        _profileService.SaveTemplate(template);
        _mainViewModel.RefreshTemplates(template.ProfileId);
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CreateProfile()
    {
        var templateGroupId = ProfileService.EnsureValidTemplateGroupId((NewProfileTemplateGroupId ?? string.Empty).Trim());
        var displayName = (NewProfileDisplayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = templateGroupId;

        var profileId = _profileService.CreateUniqueProfileId(templateGroupId, displayName);
        var template = new GameProfileTemplate
        {
            SchemaVersion = 1,
            ProfileId = profileId,
            TemplateGroupId = templateGroupId,
            DisplayName = displayName,
            Mappings = new List<MappingEntry>()
        };

        _profileService.SaveTemplate(template, allowOverwrite: false);
        _mainViewModel.RefreshTemplates(profileId);
        NewProfileTemplateGroupId = string.Empty;
        NewProfileDisplayName = string.Empty;
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

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

        _profileService.DeleteTemplate(SelectedTemplate.ProfileId);
        _mainViewModel.RefreshTemplates();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ReloadTemplate()
    {
        if (SelectedTemplate is null)
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
            case nameof(MainViewModel.TemplateTargetProcessName):
                OnPropertyChanged(nameof(TemplateTargetProcessName));
                break;
            case nameof(MainViewModel.MappingCount):
                OnPropertyChanged(nameof(MappingCount));
                break;
        }
    }
}
