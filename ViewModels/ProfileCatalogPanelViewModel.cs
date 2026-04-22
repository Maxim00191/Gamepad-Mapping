using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Services.Infrastructure;

using System.Windows.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using Gamepad_Mapping.Utils;

namespace Gamepad_Mapping.ViewModels;

public partial class ProfileCatalogPanelViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly IKeyboardCaptureService _keyboardCaptureService;
    private bool _syncingCatalogOutputKind;
    private int _validationSuspendDepth;
    private bool _validationRefreshPending;

    public ProfileCatalogPanelViewModel(MainViewModel mainViewModel)
    {
        _main = mainViewModel;
        _keyboardCaptureService = _main.KeyboardCaptureService;
        _keyboardCaptureService.PropertyChanged += KeyboardCaptureServiceOnPropertyChanged;
        _main.KeyboardActions.CollectionChanged += (_, _) => PullKeyboardCatalogDescriptionPair();
        _main.RadialMenus.CollectionChanged += (_, _) => PullRadialMenuDisplayNamePair();
        if (AppUiLocalization.TryTranslationService() is { } loc)
            loc.PropertyChanged += CatalogTranslationServiceOnPropertyChanged;
        _main.KeyboardActionSelection.SelectionChanged += (_, _) => RefreshWorkspaceSelectionMirrors();
        _main.RadialMenuSelection.SelectionChanged += (_, _) => RefreshWorkspaceSelectionMirrors();
    }

    private void CatalogTranslationServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TranslationService.Culture))
            return;
        PullKeyboardCatalogDescriptionPair();
        PullRadialMenuDisplayNamePair();
    }

    public void RefreshWorkspaceSelectionMirrors()
    {
        WorkspaceSelectedKeyboardActions.Clear();
        foreach (var item in _main.KeyboardActionSelection.SelectedItems)
            WorkspaceSelectedKeyboardActions.Add(item);

        WorkspaceSelectedRadialMenus.Clear();
        foreach (var item in _main.RadialMenuSelection.SelectedItems)
            WorkspaceSelectedRadialMenus.Add(item);

        OnPropertyChanged(nameof(SelectedKeyboardAction));
        OnPropertyChanged(nameof(SelectedRadialMenu));
        var keyboard = _main.SelectedKeyboardAction;
        var radial = _main.SelectedRadialMenu;
        WorkspaceDebugTrace.Log("selection", $"keyboard catalog selection → {(keyboard?.Id ?? "(null)")}");
        WorkspaceDebugTrace.Log("selection", $"radial menu selection → {(radial?.Id ?? "(null)")}");
        OnSelectedKeyboardActionChanged(keyboard);
        OnSelectedRadialMenuChanged(radial);
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

        _main.KeyboardActionsWorkspace.History.ExecuteTransaction(() =>
        {
            SelectedKeyboardAction.ApplyCatalogOutputKind(value);
            RequestValidationRefresh();
        });
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
            key =>
            {
                _main.KeyboardActionsWorkspace.History.ExecuteTransaction(() =>
                {
                    SelectedKeyboardAction.KeyboardKey = key.ToString();
                });
            });
    }

    [RelayCommand]
    private void RecordItemCycleForwardKey()
    {
        if (SelectedKeyboardAction?.ItemCycle is null) return;
        _keyboardCaptureService.BeginCapture(
            AppUiLocalization.GetString(AppUiLocalization.KeyboardCapturePromptKeys.ItemCycleForward),
            key =>
            {
                _main.KeyboardActionsWorkspace.History.ExecuteTransaction(() =>
                {
                    SelectedKeyboardAction.ItemCycle.LoopForwardKey = key.ToString();
                });
            });
    }

    [RelayCommand]
    private void RecordItemCycleBackwardKey()
    {
        if (SelectedKeyboardAction?.ItemCycle is null) return;
        _keyboardCaptureService.BeginCapture(
            AppUiLocalization.GetString(AppUiLocalization.KeyboardCapturePromptKeys.ItemCycleBackward),
            key =>
            {
                _main.KeyboardActionsWorkspace.History.ExecuteTransaction(() =>
                {
                    SelectedKeyboardAction.ItemCycle.LoopBackwardKey = key.ToString();
                });
            });
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
        if (result is null)
        {
            ValidationErrors.Clear();
            ValidationWarnings.Clear();
            NotifyValidationPresenceChanged();
            return;
        }

        ValidationErrors.Clear();
        foreach (var e in result.Errors)
            ValidationErrors.Add(e);
        ValidationWarnings.Clear();
        foreach (var w in result.Warnings)
            ValidationWarnings.Add(w);
        NotifyValidationPresenceChanged();
    }

    public IDisposable SuspendValidationRefresh()
    {
        _validationSuspendDepth++;
        return new ValidationRefreshScope(this);
    }

    private void ResumeValidationRefresh()
    {
        if (_validationSuspendDepth == 0)
            return;

        _validationSuspendDepth--;
        if (_validationSuspendDepth == 0 && _validationRefreshPending)
        {
            _validationRefreshPending = false;
            ValidateCurrentState();
        }
    }

    public void NotifyWorkspaceStateChanged() => RequestValidationRefresh();

    private void RequestValidationRefresh()
    {
        if (_validationSuspendDepth > 0)
        {
            _validationRefreshPending = true;
            return;
        }

        ValidateCurrentState();
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
        _main.KeyboardActionSelection.ResetTo(null);
        _main.RadialMenuSelection.ResetTo(null);
        SelectedRadialSlot = null;
    }

    public void NotifyWorkspaceKeyboardSelectionFromGrid(IReadOnlyList<object> items)
    {
        _main.KeyboardActionSelection.UpdateSelection(items);
    }

    public void NotifyWorkspaceRadialSelectionFromGrid(IReadOnlyList<object> items)
    {
        _main.RadialMenuSelection.UpdateSelection(items);
    }

    public void SelectAllKeyboardActionsForWorkspace()
    {
        _main.KeyboardActionSelection.SelectAll(_main.KeyboardActions);
    }

    public void SelectAllRadialMenusForWorkspace()
    {
        _main.RadialMenuSelection.SelectAll(_main.RadialMenus);
    }

    public IReadOnlyList<string> JoystickStickOptions { get; } = new[] { "LeftStick", "RightStick" };

    public KeyboardActionDefinition? SelectedKeyboardAction
    {
        get => _main.SelectedKeyboardAction;
        set => _main.KeyboardActionSelection.SelectedItem = value;
    }

    public RadialMenuDefinition? SelectedRadialMenu
    {
        get => _main.SelectedRadialMenu;
        set => _main.RadialMenuSelection.SelectedItem = value;
    }

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

    [ObservableProperty]
    private string _radialMenuJoystick = "RightStick";

    partial void OnRadialMenuJoystickChanged(string value)
    {
        if (SelectedRadialMenu is null) return;
        _main.RadialMenusWorkspace.History.ExecuteTransaction(() =>
        {
            SelectedRadialMenu.Joystick = value;
            _main.RadialMenusWorkspace.UpdateSelectedFromCatalog();
        });
    }

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

        _main.KeyboardActionsWorkspace.UpdateSelectedFromCatalog();
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

        _main.RadialMenusWorkspace.UpdateSelectedFromCatalog();
    }

    private void OnSelectedKeyboardActionChanged(KeyboardActionDefinition? value)
    {
        SyncCatalogOutputKindFromSelection();
        PullKeyboardCatalogDescriptionPair();
        _main.RefreshRightPanelSurface();
        _main.RuleClipboard?.RefreshCommandStates();
    }

    public void SyncCatalogOutputKindFromSelection()
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

    private void OnSelectedRadialMenuChanged(RadialMenuDefinition? value)
    {
        PullRadialMenuDisplayNamePair();
        _main.RefreshRightPanelSurface();
        _main.RuleClipboard?.RefreshCommandStates();
    }

    [RelayCommand]
    private void AddKeyboardAction()
    {
        _main.KeyboardActionsWorkspace.AddNewAction();
    }

    [RelayCommand]
    private void RemoveKeyboardAction()
    {
        _main.KeyboardActionsWorkspace.Delete();
    }

    [RelayCommand]
    private void AddRadialMenu()
    {
        _main.RadialMenusWorkspace.AddNewMenu();
    }

    [RelayCommand]
    private void RemoveRadialMenu()
    {
        _main.RadialMenusWorkspace.Delete();
    }

    [RelayCommand]
    private void AddRadialSlot()
    {
        if (SelectedRadialMenu is null)
            return;
        
        _main.RadialMenusWorkspace.History.ExecuteTransaction(() =>
        {
            SelectedRadialMenu.Items.Add(new RadialMenuItem { ActionId = string.Empty });
            _main.RadialMenusWorkspace.UpdateSelectedFromCatalog();
        });
    }

    [RelayCommand]
    private void RemoveRadialSlot()
    {
        if (SelectedRadialMenu is null || SelectedRadialSlot is null)
            return;
        
        _main.RadialMenusWorkspace.History.ExecuteTransaction(() =>
        {
            SelectedRadialMenu.Items.Remove(SelectedRadialSlot);
            _main.RadialMenusWorkspace.UpdateSelectedFromCatalog();
        });
    }

    private sealed class ValidationRefreshScope : IDisposable
    {
        private ProfileCatalogPanelViewModel? _owner;

        public ValidationRefreshScope(ProfileCatalogPanelViewModel owner) => _owner = owner;

        public void Dispose()
        {
            var owner = _owner;
            if (owner is null)
                return;

            _owner = null;
            owner.ResumeValidationRefresh();
        }
    }
}

