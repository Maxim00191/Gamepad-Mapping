using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;

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
    private bool _workspaceKeyboardSelectionSync;
    private bool _workspaceRadialSelectionSync;

    public ProfileCatalogPanelViewModel(MainViewModel mainViewModel)
    {
        _main = mainViewModel;
        _keyboardCaptureService = _main.KeyboardCaptureService;
        _keyboardCaptureService.PropertyChanged += KeyboardCaptureServiceOnPropertyChanged;
        _main.KeyboardActions.CollectionChanged += (_, _) => ValidateCurrentState();
        _main.RadialMenus.CollectionChanged += (_, _) => ValidateCurrentState();
        if (AppUiLocalization.TryTranslationService() is { } loc)
            loc.PropertyChanged += CatalogTranslationServiceOnPropertyChanged;
    }

    private void CatalogTranslationServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TranslationService.Culture))
            return;
        PullKeyboardCatalogDescriptionPair();
        PullRadialMenuDisplayNamePair();
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
            AppUiLocalization.GetString(AppUiLocalization.KeyboardCapturePromptKeys.CatalogActionOutput),
            key => SelectedKeyboardAction.KeyboardKey = key.ToString());
    }

    [RelayCommand]
    private void RecordItemCycleForwardKey()
    {
        if (SelectedKeyboardAction?.ItemCycle is null) return;
        _keyboardCaptureService.BeginCapture(
            AppUiLocalization.GetString(AppUiLocalization.KeyboardCapturePromptKeys.ItemCycleForward),
            key => SelectedKeyboardAction.ItemCycle.LoopForwardKey = key.ToString());
    }

    [RelayCommand]
    private void RecordItemCycleBackwardKey()
    {
        if (SelectedKeyboardAction?.ItemCycle is null) return;
        _keyboardCaptureService.BeginCapture(
            AppUiLocalization.GetString(AppUiLocalization.KeyboardCapturePromptKeys.ItemCycleBackward),
            key => SelectedKeyboardAction.ItemCycle.LoopBackwardKey = key.ToString());
    }

    private void ValidateCurrentState()
    {
        var profile = _main.GetWorkspaceTemplateSnapshot();
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

    /// <summary>Rows currently selected in the keyboard actions grid (multi-select); drives copy/paste.</summary>
    public ObservableCollection<KeyboardActionDefinition> WorkspaceSelectedKeyboardActions { get; } = [];

    /// <summary>Rows currently selected in the radial menus grid (multi-select); drives copy/paste.</summary>
    public ObservableCollection<RadialMenuDefinition> WorkspaceSelectedRadialMenus { get; } = [];

    public ObservableCollection<string> ValidationErrors { get; } = [];

    public ObservableCollection<string> ValidationWarnings { get; } = [];

    public bool HasErrors => ValidationErrors.Any();

    public bool HasWarnings => ValidationWarnings.Any();

    public void ResetSelection()
    {
        _workspaceKeyboardSelectionSync = true;
        _workspaceRadialSelectionSync = true;
        try
        {
            WorkspaceSelectedKeyboardActions.Clear();
            WorkspaceSelectedRadialMenus.Clear();
            SelectedKeyboardAction = null;
            SelectedRadialMenu = null;
            SelectedRadialSlot = null;
        }
        finally
        {
            _workspaceKeyboardSelectionSync = false;
            _workspaceRadialSelectionSync = false;
        }
    }

    public void NotifyWorkspaceKeyboardSelectionFromGrid(IReadOnlyList<object> items)
    {
        _workspaceKeyboardSelectionSync = true;
        try
        {
            WorkspaceSelectedKeyboardActions.Clear();
            foreach (var o in items)
            {
                if (o is KeyboardActionDefinition k)
                    WorkspaceSelectedKeyboardActions.Add(k);
            }

            SelectedKeyboardAction = WorkspaceSelectedKeyboardActions.Count > 0
                ? WorkspaceSelectedKeyboardActions[^1]
                : null;
        }
        finally
        {
            _workspaceKeyboardSelectionSync = false;
        }

        _main.RuleClipboard?.RefreshCommandStates();
    }

    public void NotifyWorkspaceRadialSelectionFromGrid(IReadOnlyList<object> items)
    {
        _workspaceRadialSelectionSync = true;
        try
        {
            WorkspaceSelectedRadialMenus.Clear();
            foreach (var o in items)
            {
                if (o is RadialMenuDefinition r)
                    WorkspaceSelectedRadialMenus.Add(r);
            }

            SelectedRadialMenu = WorkspaceSelectedRadialMenus.Count > 0
                ? WorkspaceSelectedRadialMenus[^1]
                : null;
        }
        finally
        {
            _workspaceRadialSelectionSync = false;
        }

        _main.RuleClipboard?.RefreshCommandStates();
    }

    public void SelectAllKeyboardActionsForWorkspace()
    {
        _workspaceKeyboardSelectionSync = true;
        try
        {
            WorkspaceSelectedKeyboardActions.Clear();
            foreach (var a in KeyboardActions)
                WorkspaceSelectedKeyboardActions.Add(a);
            SelectedKeyboardAction = KeyboardActions.Count > 0 ? KeyboardActions[^1] : null;
        }
        finally
        {
            _workspaceKeyboardSelectionSync = false;
        }

        _main.RuleClipboard?.RefreshCommandStates();
    }

    public void SelectAllRadialMenusForWorkspace()
    {
        _workspaceRadialSelectionSync = true;
        try
        {
            WorkspaceSelectedRadialMenus.Clear();
            foreach (var r in RadialMenus)
                WorkspaceSelectedRadialMenus.Add(r);
            SelectedRadialMenu = RadialMenus.Count > 0 ? RadialMenus[^1] : null;
        }
        finally
        {
            _workspaceRadialSelectionSync = false;
        }

        _main.RuleClipboard?.RefreshCommandStates();
    }

    public IReadOnlyList<string> JoystickStickOptions { get; } = new[] { "LeftStick", "RightStick" };

    [ObservableProperty]
    private KeyboardActionDefinition? selectedKeyboardAction;

    [ObservableProperty]
    private RadialMenuDefinition? selectedRadialMenu;

    [ObservableProperty]
    private RadialMenuItem? selectedRadialSlot;

    [ObservableProperty]
    private string keyboardCatalogDescriptionPrimary = string.Empty;

    [ObservableProperty]
    private string keyboardCatalogDescriptionSecondary = string.Empty;

    private bool _syncingKeyboardDescriptionPair;

    [ObservableProperty]
    private string radialMenuDisplayNamePrimary = string.Empty;

    [ObservableProperty]
    private string radialMenuDisplayNameSecondary = string.Empty;

    private bool _syncingRadialDisplayNamePair;

    partial void OnKeyboardCatalogDescriptionPrimaryChanged(string value) => PushKeyboardCatalogDescriptionPair();

    partial void OnKeyboardCatalogDescriptionSecondaryChanged(string value) => PushKeyboardCatalogDescriptionPair();

    partial void OnRadialMenuDisplayNamePrimaryChanged(string value) => PushRadialMenuDisplayNamePair();

    partial void OnRadialMenuDisplayNameSecondaryChanged(string value) => PushRadialMenuDisplayNamePair();

    private void PullKeyboardCatalogDescriptionPair()
    {
        _syncingKeyboardDescriptionPair = true;
        try
        {
            if (SelectedKeyboardAction is null)
            {
                KeyboardCatalogDescriptionPrimary = string.Empty;
                KeyboardCatalogDescriptionSecondary = string.Empty;
                return;
            }

            var ui = AppUiLocalization.EditorUiCulture();
            var a = SelectedKeyboardAction;
            KeyboardCatalogDescriptionPrimary = UiCultureDescriptionPair.ReadPrimary(a.Descriptions, a.Description, ui);
            KeyboardCatalogDescriptionSecondary = UiCultureDescriptionPair.ReadSecondary(a.Descriptions, a.Description, ui);
        }
        finally
        {
            _syncingKeyboardDescriptionPair = false;
        }
    }

    private void PushKeyboardCatalogDescriptionPair()
    {
        if (_syncingKeyboardDescriptionPair || SelectedKeyboardAction is null)
            return;

        var a = SelectedKeyboardAction;
        var d = a.Descriptions;
        var b = a.Description;
        UiCultureDescriptionPair.WritePair(ref d, ref b, AppUiLocalization.EditorUiCulture(), KeyboardCatalogDescriptionPrimary, KeyboardCatalogDescriptionSecondary);
        a.Descriptions = d;
        a.Description = b;
        if (AppUiLocalization.TryTranslationService() is { } ts)
            CatalogDescriptionLocalizer.ApplyKeyboardAction(a, ts);
    }

    private void PullRadialMenuDisplayNamePair()
    {
        _syncingRadialDisplayNamePair = true;
        try
        {
            if (SelectedRadialMenu is null)
            {
                RadialMenuDisplayNamePrimary = string.Empty;
                RadialMenuDisplayNameSecondary = string.Empty;
                return;
            }

            var ui = AppUiLocalization.EditorUiCulture();
            var rm = SelectedRadialMenu;
            RadialMenuDisplayNamePrimary = UiCultureDescriptionPair.ReadPrimary(rm.DisplayNames, rm.DisplayName, ui);
            RadialMenuDisplayNameSecondary = UiCultureDescriptionPair.ReadSecondary(rm.DisplayNames, rm.DisplayName, ui);
        }
        finally
        {
            _syncingRadialDisplayNamePair = false;
        }
    }

    private void PushRadialMenuDisplayNamePair()
    {
        if (_syncingRadialDisplayNamePair || SelectedRadialMenu is null)
            return;

        var rm = SelectedRadialMenu;
        var d = rm.DisplayNames;
        var b = rm.DisplayName;
        UiCultureDescriptionPair.WritePair(ref d, ref b, AppUiLocalization.EditorUiCulture(), RadialMenuDisplayNamePrimary, RadialMenuDisplayNameSecondary);
        rm.DisplayNames = d;
        rm.DisplayName = b;
        if (AppUiLocalization.TryTranslationService() is { } ts)
            CatalogDescriptionLocalizer.ApplyRadialMenu(rm, ts);
    }

    partial void OnSelectedKeyboardActionChanged(KeyboardActionDefinition? value)
    {
        if (!_workspaceKeyboardSelectionSync)
        {
            _workspaceKeyboardSelectionSync = true;
            try
            {
                WorkspaceSelectedKeyboardActions.Clear();
                if (value is not null)
                    WorkspaceSelectedKeyboardActions.Add(value);
            }
            finally
            {
                _workspaceKeyboardSelectionSync = false;
            }
        }

        SyncCatalogOutputKindFromSelection();
        PullKeyboardCatalogDescriptionPair();
        _main.RefreshRightPanelSurface();
        ValidateCurrentState();
        _main.RuleClipboard?.RefreshCommandStates();
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
        if (!_workspaceRadialSelectionSync)
        {
            _workspaceRadialSelectionSync = true;
            try
            {
                WorkspaceSelectedRadialMenus.Clear();
                if (value is not null)
                    WorkspaceSelectedRadialMenus.Add(value);
            }
            finally
            {
                _workspaceRadialSelectionSync = false;
            }
        }

        PullRadialMenuDisplayNamePair();
        _main.RefreshRightPanelSurface();
        ValidateCurrentState();
        _main.RuleClipboard?.RefreshCommandStates();
    }

    [RelayCommand]
    private void AddKeyboardAction()
    {
        _main.RecordTemplateWorkspaceCheckpoint();
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
        _main.RecordTemplateWorkspaceCheckpoint();
        _main.KeyboardActions.Remove(SelectedKeyboardAction);
    }

    [RelayCommand]
    private void AddRadialMenu()
    {
        _main.RecordTemplateWorkspaceCheckpoint();
        _main.RadialMenus.Add(new RadialMenuDefinition
        {
            Id = NextRadialMenuId(),
            DisplayName = AppUiLocalization.GetString("RadialMenu_DefaultDisplayName"),
            Joystick = "RightStick",
            Items = new ObservableCollection<RadialMenuItem>()
        });
    }

    [RelayCommand]
    private void RemoveRadialMenu()
    {
        if (SelectedRadialMenu is null)
            return;
        _main.RecordTemplateWorkspaceCheckpoint();
        _main.RadialMenus.Remove(SelectedRadialMenu);
    }

    [RelayCommand]
    private void AddRadialSlot()
    {
        if (SelectedRadialMenu is null)
            return;
        _main.RecordTemplateWorkspaceCheckpoint();
        SelectedRadialMenu.Items.Add(new RadialMenuItem { ActionId = string.Empty });
    }

    [RelayCommand]
    private void RemoveRadialSlot()
    {
        if (SelectedRadialMenu is null || SelectedRadialSlot is null)
            return;
        _main.RecordTemplateWorkspaceCheckpoint();
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

