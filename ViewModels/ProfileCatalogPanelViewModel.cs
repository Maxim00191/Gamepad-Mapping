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

    partial void OnSelectedKeyboardActionChanged(KeyboardActionDefinition? value) => _main.RefreshRightPanelSurface();

    partial void OnSelectedRadialMenuChanged(RadialMenuDefinition? value) => _main.RefreshRightPanelSurface();

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
