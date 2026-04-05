using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Models;

using System.Windows.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services;

namespace Gamepad_Mapping.ViewModels;

public partial class ProfileCatalogPanelViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly IKeyboardCaptureService _keyboardCaptureService;

    public ProfileCatalogPanelViewModel(MainViewModel mainViewModel)
    {
        _main = mainViewModel;
        _keyboardCaptureService = _main.KeyboardCaptureService;
        _keyboardCaptureService.PropertyChanged += KeyboardCaptureServiceOnPropertyChanged;
        _main.KeyboardActions.CollectionChanged += (_, _) => ValidateCurrentState();
        _main.RadialMenus.CollectionChanged += (_, _) => ValidateCurrentState();
    }

    private void KeyboardCaptureServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IKeyboardCaptureService.KeyboardKeyCapturePrompt))
            OnPropertyChanged(nameof(KeyboardKeyCapturePrompt));
    }

    public IEnumerable<string> AvailableKeys { get; } = Enum.GetNames<Key>()
        .Where(k => !string.Equals(k, "None", StringComparison.OrdinalIgnoreCase))
        .OrderBy(k => k);

    public string KeyboardKeyCapturePrompt => _keyboardCaptureService.KeyboardKeyCapturePrompt;

    [RelayCommand]
    private void RecordKeyboardKey()
    {
        if (SelectedKeyboardAction is null) return;

        _keyboardCaptureService.BeginCapture(
            "Press a key for the action output (Esc to cancel).",
            key => SelectedKeyboardAction.KeyboardKey = key.ToString());
    }

    private void ValidateCurrentState()
    {
        var profile = _main.GetProfileService().LoadSelectedTemplate(_main.SelectedTemplate);
        if (profile == null)
        {
            HasValidationError = false;
            ValidationError = string.Empty;
            HasValidationWarning = false;
            ValidationWarning = string.Empty;
            return;
        }

        var result = _main.GetProfileService().ValidateTemplate(profile);
        ValidationError = result.Errors.Any() ? string.Join(Environment.NewLine, result.Errors) : string.Empty;
        HasValidationError = !string.IsNullOrEmpty(ValidationError);
        
        ValidationWarning = result.Warnings.Any() ? string.Join(Environment.NewLine, result.Warnings) : string.Empty;
        HasValidationWarning = !string.IsNullOrEmpty(ValidationWarning);
    }

    public ObservableCollection<KeyboardActionDefinition> KeyboardActions => _main.KeyboardActions;

    public ObservableCollection<RadialMenuDefinition> RadialMenus => _main.RadialMenus;

    public IReadOnlyList<string> JoystickStickOptions { get; } = new[] { "LeftStick", "RightStick" };

    [ObservableProperty]
    private KeyboardActionDefinition? selectedKeyboardAction;

    [ObservableProperty]
    private RadialMenuDefinition? selectedRadialMenu;

    [ObservableProperty]
    private RadialMenuItem? selectedRadialSlot;

    [ObservableProperty]
    private string validationError = string.Empty;

    [ObservableProperty]
    private string validationWarning = string.Empty;

    [ObservableProperty]
    private bool hasValidationError;

    [ObservableProperty]
    private bool hasValidationWarning;

    partial void OnSelectedKeyboardActionChanged(KeyboardActionDefinition? value)
    {
        _main.RefreshRightPanelSurface();
        ValidateCurrentState();
    }

    partial void OnSelectedRadialMenuChanged(RadialMenuDefinition? value)
    {
        _main.RefreshRightPanelSurface();
        ValidateCurrentState();
    }

    [RelayCommand]
    private void AddKeyboardAction()
    {
        _main.KeyboardActions.Add(new KeyboardActionDefinition
        {
            Id = NextKeyboardActionId(),
            KeyboardKey = string.Empty,
            Description = string.Empty
        });
    }

    [RelayCommand]
    private void RemoveKeyboardAction()
    {
        if (SelectedKeyboardAction is null)
            return;
        _main.KeyboardActions.Remove(SelectedKeyboardAction);
    }

    [RelayCommand]
    private void AddRadialMenu()
    {
        _main.RadialMenus.Add(new RadialMenuDefinition
        {
            Id = NextRadialMenuId(),
            DisplayName = "Radial",
            Joystick = "RightStick",
            Items = new ObservableCollection<RadialMenuItem>()
        });
    }

    [RelayCommand]
    private void RemoveRadialMenu()
    {
        if (SelectedRadialMenu is null)
            return;
        _main.RadialMenus.Remove(SelectedRadialMenu);
    }

    [RelayCommand]
    private void AddRadialSlot()
    {
        if (SelectedRadialMenu is null)
            return;
        SelectedRadialMenu.Items.Add(new RadialMenuItem { ActionId = string.Empty });
    }

    [RelayCommand]
    private void RemoveRadialSlot()
    {
        if (SelectedRadialMenu is null || SelectedRadialSlot is null)
            return;
        SelectedRadialMenu.Items.Remove(SelectedRadialSlot);
    }

    private string NextKeyboardActionId()
    {
        for (var n = 1; n < 10_000; n++)
        {
            var id = $"action{n}";
            if (_main.KeyboardActions.All(a => !string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase)))
                return id;
        }

        return $"action{Guid.NewGuid():N}"[..12];
    }

    private string NextRadialMenuId()
    {
        for (var n = 1; n < 10_000; n++)
        {
            var id = $"radial{n}";
            if (_main.RadialMenus.All(r => !string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)))
                return id;
        }

        return $"radial{Guid.NewGuid():N}"[..12];
    }
}
