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
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;

namespace Gamepad_Mapping.ViewModels;

public partial class ProfileCatalogPanelViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly IKeyboardCaptureService _keyboardCaptureService;
    private bool _syncingCatalogOutputKind;

    public ProfileCatalogPanelViewModel(MainViewModel mainViewModel)
    {
        _main = mainViewModel;
        _keyboardCaptureService = _main.KeyboardCaptureService;
        _keyboardCaptureService.PropertyChanged += KeyboardCaptureServiceOnPropertyChanged;
        _main.KeyboardActions.CollectionChanged += (_, _) => ValidateCurrentState();
        _main.RadialMenus.CollectionChanged += (_, _) => ValidateCurrentState();
    }

    public ObservableCollection<TemplateOption> AvailableProfileTemplates => _main.AvailableTemplates;

    public ObservableCollection<RadialMenuDefinition> AvailableRadialMenus => _main.RadialMenus;

    public KeyboardCatalogOutputKind[] CatalogOutputKindOptions { get; } = Enum.GetValues<KeyboardCatalogOutputKind>();

    [ObservableProperty]
    private KeyboardCatalogOutputKind _catalogOutputKind = KeyboardCatalogOutputKind.Keyboard;

    partial void OnCatalogOutputKindChanged(KeyboardCatalogOutputKind value)
    {
        NotifyCatalogOutputKindSectionVisibility();
        if (_syncingCatalogOutputKind || SelectedKeyboardAction is null)
            return;

        SelectedKeyboardAction.ApplyCatalogOutputKind(value);
        ValidateCurrentState();
    }

    private void NotifyCatalogOutputKindSectionVisibility()
    {
        OnPropertyChanged(nameof(IsCatalogKeyboardSectionVisible));
        OnPropertyChanged(nameof(IsCatalogTemplateToggleSectionVisible));
        OnPropertyChanged(nameof(IsCatalogRadialMenuSectionVisible));
        OnPropertyChanged(nameof(IsCatalogItemCycleSectionVisible));
    }

    public bool IsCatalogKeyboardSectionVisible => CatalogOutputKind == KeyboardCatalogOutputKind.Keyboard;

    public bool IsCatalogTemplateToggleSectionVisible => CatalogOutputKind == KeyboardCatalogOutputKind.TemplateToggle;

    public bool IsCatalogRadialMenuSectionVisible => CatalogOutputKind == KeyboardCatalogOutputKind.RadialMenu;

    public bool IsCatalogItemCycleSectionVisible => CatalogOutputKind == KeyboardCatalogOutputKind.ItemCycle;

    private void KeyboardCaptureServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IKeyboardCaptureService.KeyboardKeyCapturePrompt))
            OnPropertyChanged(nameof(KeyboardKeyCapturePrompt));
    }

    public IEnumerable<string> AvailableKeys { get; } = Enum.GetNames<Key>()
        .Where(k => !string.Equals(k, "None", StringComparison.OrdinalIgnoreCase))
        .OrderBy(k => k);

    public ItemCycleDirection[] AvailableItemCycleDirections { get; } = Enum.GetValues<ItemCycleDirection>();

    public string KeyboardKeyCapturePrompt => _keyboardCaptureService.KeyboardKeyCapturePrompt;

    [RelayCommand]
    private void RecordKeyboardKey()
    {
        if (SelectedKeyboardAction is null) return;

        _keyboardCaptureService.BeginCapture(
            "Press a key for the action output (Esc to cancel).",
            key => SelectedKeyboardAction.KeyboardKey = key.ToString());
    }

    [RelayCommand]
    private void RecordItemCycleForwardKey()
    {
        if (SelectedKeyboardAction?.ItemCycle is null) return;
        _keyboardCaptureService.BeginCapture(
            "Press the loop-forward output key (Esc to cancel).",
            key => SelectedKeyboardAction.ItemCycle.LoopForwardKey = key.ToString());
    }

    [RelayCommand]
    private void RecordItemCycleBackwardKey()
    {
        if (SelectedKeyboardAction?.ItemCycle is null) return;
        _keyboardCaptureService.BeginCapture(
            "Press the loop-back output key (Esc to cancel).",
            key => SelectedKeyboardAction.ItemCycle.LoopBackwardKey = key.ToString());
    }

    private void ValidateCurrentState()
    {
        var profile = _main.GetProfileService().LoadSelectedTemplate(_main.SelectedTemplate);
        if (profile == null)
        {
            ValidationErrors.Clear();
            ValidationWarnings.Clear();
            NotifyValidationPresenceChanged();
            return;
        }

        var result = _main.GetProfileService().ValidateTemplate(profile);
        ValidationErrors.Clear();
        foreach (var e in result.Errors)
            ValidationErrors.Add(e);
        ValidationWarnings.Clear();
        foreach (var w in result.Warnings)
            ValidationWarnings.Add(w);
        NotifyValidationPresenceChanged();
    }

    private void NotifyValidationPresenceChanged()
    {
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(HasWarnings));
    }

    public ObservableCollection<KeyboardActionDefinition> KeyboardActions => _main.KeyboardActions;

    public ObservableCollection<RadialMenuDefinition> RadialMenus => _main.RadialMenus;

    public ObservableCollection<string> ValidationErrors { get; } = [];

    public ObservableCollection<string> ValidationWarnings { get; } = [];

    public bool HasErrors => ValidationErrors.Any();

    public bool HasWarnings => ValidationWarnings.Any();

    public void ResetSelection()
    {
        SelectedKeyboardAction = null;
        SelectedRadialMenu = null;
        SelectedRadialSlot = null;
    }

    public IReadOnlyList<string> JoystickStickOptions { get; } = new[] { "LeftStick", "RightStick" };

    [ObservableProperty]
    private KeyboardActionDefinition? selectedKeyboardAction;

    [ObservableProperty]
    private RadialMenuDefinition? selectedRadialMenu;

    [ObservableProperty]
    private RadialMenuItem? selectedRadialSlot;

    partial void OnSelectedKeyboardActionChanged(KeyboardActionDefinition? value)
    {
        SyncCatalogOutputKindFromSelection();
        _main.RefreshRightPanelSurface();
        ValidateCurrentState();
    }

    private void SyncCatalogOutputKindFromSelection()
    {
        _syncingCatalogOutputKind = true;
        try
        {
            CatalogOutputKind = SelectedKeyboardAction?.ResolveCatalogOutputKind() ?? KeyboardCatalogOutputKind.Keyboard;
        }
        finally
        {
            _syncingCatalogOutputKind = false;
        }
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

