using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels.Strategies;

public partial class TemplateToggleActionEditorViewModel : ActionEditorViewModelBase
{
    private readonly IProfileService _profileService;
    private readonly string? _currentProfileId;

    public TemplateToggleActionEditorViewModel(IProfileService profileService, string? currentProfileId)
    {
        _profileService = profileService;
        _currentProfileId = currentProfileId;
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
        if (string.Equals(alt, _currentProfileId, StringComparison.OrdinalIgnoreCase))
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
