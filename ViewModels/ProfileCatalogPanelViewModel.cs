using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels;

public partial class ProfileCatalogPanelViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public ProfileCatalogPanelViewModel(MainViewModel mainViewModel)
    {
        _main = mainViewModel;
        _main.KeyboardActions.CollectionChanged += (_, _) => ValidateCurrentState();
        _main.RadialMenus.CollectionChanged += (_, _) => ValidateCurrentState();
    }

    private void ValidateCurrentState()
    {
        var profile = _main.GetProfileService().LoadSelectedTemplate(_main.SelectedTemplate);
        if (profile == null) return;

        var result = _main.GetProfileService().ValidateTemplate(profile);
        HasValidationError = !result.IsValid;
        ValidationError = string.Join(Environment.NewLine, result.Errors);
        HasValidationWarning = result.Warnings.Any();
        ValidationWarning = string.Join(Environment.NewLine, result.Warnings);
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

    private bool _loadingRadialMenuLocalizationFields;

    [ObservableProperty]
    private string radialMenuDisplayNameKey = string.Empty;

    [ObservableProperty]
    private string radialMenuDisplayNameZhCn = string.Empty;

    [ObservableProperty]
    private string radialMenuDisplayNameEnUs = string.Empty;

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
        _loadingRadialMenuLocalizationFields = true;
        try
        {
            if (value is null)
            {
                RadialMenuDisplayNameKey = string.Empty;
                RadialMenuDisplayNameZhCn = string.Empty;
                RadialMenuDisplayNameEnUs = string.Empty;
            }
            else
            {
                RadialMenuDisplayNameKey = value.DisplayNameKey ?? string.Empty;
                RadialMenuDisplayNameZhCn =
                    value.DisplayNames != null && value.DisplayNames.TryGetValue("zh-CN", out var z) ? z : string.Empty;
                RadialMenuDisplayNameEnUs =
                    value.DisplayNames != null && value.DisplayNames.TryGetValue("en-US", out var e) ? e : string.Empty;
            }
        }
        finally
        {
            _loadingRadialMenuLocalizationFields = false;
        }

        _main.RefreshRightPanelSurface();
    }

    partial void OnRadialMenuDisplayNameKeyChanged(string value)
    {
        if (_loadingRadialMenuLocalizationFields || SelectedRadialMenu is null)
            return;

        var t = (value ?? string.Empty).Trim();
        SelectedRadialMenu.DisplayNameKey = string.IsNullOrEmpty(t) ? null : t;
    }

    partial void OnRadialMenuDisplayNameZhCnChanged(string value)
    {
        if (_loadingRadialMenuLocalizationFields || SelectedRadialMenu is null)
            return;

        SelectedRadialMenu.DisplayNames ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
            SelectedRadialMenu.DisplayNames.Remove("zh-CN");
        else
            SelectedRadialMenu.DisplayNames["zh-CN"] = value.Trim();

        if (SelectedRadialMenu.DisplayNames.Count == 0)
            SelectedRadialMenu.DisplayNames = null;
    }

    partial void OnRadialMenuDisplayNameEnUsChanged(string value)
    {
        if (_loadingRadialMenuLocalizationFields || SelectedRadialMenu is null)
            return;

        SelectedRadialMenu.DisplayNames ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
            SelectedRadialMenu.DisplayNames.Remove("en-US");
        else
            SelectedRadialMenu.DisplayNames["en-US"] = value.Trim();

        if (SelectedRadialMenu.DisplayNames.Count == 0)
            SelectedRadialMenu.DisplayNames = null;
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
