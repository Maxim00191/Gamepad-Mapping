using System;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels.Strategies;

public partial class TemplateToggleActionEditorViewModel : ActionEditorViewModelBase
{
    private readonly IProfileService _profileService;
    private readonly string? _currentTemplateStorageKey;

    public TemplateToggleActionEditorViewModel(IProfileService profileService, string? currentTemplateStorageKey)
    {
        _profileService = profileService;
        _currentTemplateStorageKey = currentTemplateStorageKey;
    }

    [ObservableProperty]
    private string _alternateProfileId = string.Empty;

    public override void SyncFrom(MappingEntry mapping)
    {
        AlternateProfileId = mapping.TemplateToggle?.AlternateProfileId ?? string.Empty;
    }

    public override bool ApplyTo(MappingEntry mapping)
    {
        var alt = (AlternateProfileId ?? string.Empty).Trim();
        if (alt.Length == 0 || !_profileService.TemplateExists(alt))
            return false;

        if (_currentTemplateStorageKey is not null
            && _profileService.TryResolveTemplateLocation(alt, out var altLoc)
            && _profileService.TryResolveTemplateLocation(_currentTemplateStorageKey, out var curLoc)
            && altLoc.SameFileAs(curLoc))
            return false;

        mapping.ItemCycle = null;
        mapping.TemplateToggle = new TemplateToggleBinding { AlternateProfileId = alt };
        mapping.RadialMenu = null;
        mapping.ActionId = null;
        mapping.KeyboardKey = string.Empty;
        mapping.HoldKeyboardKey = string.Empty;
        mapping.HoldThresholdMs = null;

        return true;
    }

    public override void Clear()
    {
        AlternateProfileId = string.Empty;
    }
}
