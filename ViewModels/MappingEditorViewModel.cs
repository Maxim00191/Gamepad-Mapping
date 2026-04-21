using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Core;
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
using Gamepad_Mapping.ViewModels.Strategies;

namespace Gamepad_Mapping.ViewModels;

public partial class MappingEditorViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly IActionEditorFactory _actionEditorFactory;
    private bool _resolvingActionId;
    private bool _syncingActionEditorFromSelection;
    private readonly HashSet<MappingEntry> _mappingActionIdListeners = [];
    private bool _workspaceMappingSelectionSync;

    [ObservableProperty]
    private ActionEditorViewModelBase? _currentActionEditor;

    [ObservableProperty]
    private MappingActionType _selectedActionType = MappingActionType.Keyboard;

    [ObservableProperty]
    private InputTriggerViewModel _inputTrigger;

    partial void OnSelectedActionTypeChanged(MappingActionType value)
    {
        if (_syncingActionEditorFromSelection)
            return;

        CurrentActionEditor = _actionEditorFactory.Create(value);
        OnPropertyChanged(nameof(EditKeyboardAndHoldSectionsEnabled));
        OnPropertyChanged(nameof(EditBindingKeyboardKeyIsReadOnly));
    }

    public MappingEditorViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _actionEditorFactory = new ActionEditorFactory(_mainViewModel);
        _inputTrigger = new InputTriggerViewModel(_mainViewModel);

        _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
        if (AppUiLocalization.TryTranslationService() is { } translationService)
            translationService.PropertyChanged += TranslationServiceOnPropertyChanged;
        _mainViewModel.KeyboardCaptureService.PropertyChanged += KeyboardCaptureServiceOnPropertyChanged;
        _mainViewModel.Mappings.CollectionChanged += OnMappingsCollectionChanged;
        _mainViewModel.KeyboardActions.CollectionChanged += (_, _) =>
        {
            RebuildKeyboardActionsPicker();
            RefreshStatusDiagnostics();
        };
        _mainViewModel.RadialMenus.CollectionChanged += (_, _) => RefreshStatusDiagnostics();
        foreach (var m in _mainViewModel.Mappings)
            AttachMappingActionIdListener(m);
        RebuildKeyboardActionsPicker();
        RefreshStatusDiagnostics();

        // Initialize default editor
        OnSelectedActionTypeChanged(SelectedActionType);
    }

    private void ValidateCurrentState()
    {
        var profile = _mainViewModel.GetWorkspaceTemplateSnapshot();
        if (profile == null)
        {
            HasValidationError = false;
            ValidationError = string.Empty;
            HasValidationWarning = false;
            ValidationWarning = string.Empty;
            return;
        }

        var result = _mainViewModel.GetProfileService().ValidateTemplate(profile);
        if (result is null)
        {
            HasValidationError = false;
            ValidationError = string.Empty;
            HasValidationWarning = false;
            ValidationWarning = string.Empty;
            return;
        }

        var errors = result.Errors ?? [];
        var warnings = result.Warnings ?? [];
        HasValidationError = !result.IsValid;
        ValidationError = string.Join(Environment.NewLine, errors);
        HasValidationWarning = warnings.Any();
        ValidationWarning = string.Join(Environment.NewLine, warnings);
    }

    public void RefreshStatusDiagnostics()
    {
        UpdateUnusedActionIds();
        ValidateCurrentState();
    }

    [ObservableProperty]
    private string unusedActionIdsHint = string.Empty;

    [ObservableProperty]
    private string unusedActionIdsTooltip = string.Empty;

    [ObservableProperty]
    private bool hasUnusedActionIds;

    private void UpdateUnusedActionIds()
    {
        var profile = _mainViewModel.GetWorkspaceTemplateSnapshot();
        if (profile is null)
        {
            HasUnusedActionIds = false;
            return;
        }

        var usedInMappings = _mainViewModel.Mappings
            .Select(m => m.ActionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var usedInRadialMenus = _mainViewModel.RadialMenus
            .SelectMany(rm => rm.Items)
            .Select(item => item.ActionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unused = profile.GetAllActions()
            .Where(a => !string.IsNullOrWhiteSpace(a.Id) && 
                        !usedInMappings.Contains(a.Id) && 
                        !usedInRadialMenus.Contains(a.Id))
            .ToList();

        HasUnusedActionIds = unused.Count > 0;
        if (HasUnusedActionIds)
        {
            UnusedActionIdsHint = string.Format(CultureInfo.CurrentUICulture,
                AppUiLocalization.GetString("ProfileMappingUnusedCatalogActionIdsHint"), unused.Count);
            UnusedActionIdsTooltip = string.Join(Environment.NewLine,
                unused.Select(a => $"{a.Id}: {a.Description}"));
        }
        else
        {
            UnusedActionIdsHint = string.Empty;
            UnusedActionIdsTooltip = string.Empty;
        }
    }

    /// <summary>Keyboard catalog for the current profile (same as <see cref="MainViewModel.KeyboardActions"/>).</summary>
    public ObservableCollection<KeyboardActionDefinition> KeyboardActions => _mainViewModel.KeyboardActions;

    /// <summary>Catalog entries plus a synthetic row with empty <see cref="KeyboardActionDefinition.Id"/> for manual key output.</summary>
    public ObservableCollection<KeyboardActionDefinition> KeyboardActionsForPicker { get; } = [];

    public bool EditBindingKeyboardKeyIsReadOnly => (CurrentActionEditor as KeyboardActionEditorViewModel)?.IsKeyboardKeyReadOnly ?? false;

    public event EventHandler? ConfigurationChanged;

    public ObservableCollection<MappingEntry> Mappings => _mainViewModel.Mappings;

    /// <summary>Rows selected in the mappings grid (multi-select); drives workspace copy/paste.</summary>
    public ObservableCollection<MappingEntry> WorkspaceSelectedMappings { get; } = [];

    public MappingEntry? SelectedMapping
    {
        get => _mainViewModel.SelectedMapping;
        set => _mainViewModel.SelectedMapping = value;
    }

    public void NotifyWorkspaceMappingSelectionFromGrid(IReadOnlyList<object> items)
    {
        _workspaceMappingSelectionSync = true;
        try
        {
            WorkspaceSelectedMappings.Clear();
            foreach (var o in items)
            {
                if (o is MappingEntry m)
                    WorkspaceSelectedMappings.Add(m);
            }

            if (WorkspaceSelectedMappings.Count <= 1)
                SelectedMapping = WorkspaceSelectedMappings.Count == 1 ? WorkspaceSelectedMappings[0] : null;
        }
        finally
        {
            _workspaceMappingSelectionSync = false;
        }

        _mainViewModel.RuleClipboard?.RefreshCommandStates();
    }

    public void SelectAllMappingsForWorkspace()
    {
        _workspaceMappingSelectionSync = true;
        try
        {
            WorkspaceSelectedMappings.Clear();
            foreach (var m in Mappings)
                WorkspaceSelectedMappings.Add(m);
        }
        finally
        {
            _workspaceMappingSelectionSync = false;
        }

        _mainViewModel.RuleClipboard?.RefreshCommandStates();
    }

    private void SyncWorkspaceMappingsFromPrimary()
    {
        _workspaceMappingSelectionSync = true;
        try
        {
            WorkspaceSelectedMappings.Clear();
            if (SelectedMapping is not null)
                WorkspaceSelectedMappings.Add(SelectedMapping);
        }
        finally
        {
            _workspaceMappingSelectionSync = false;
        }
    }

    public ObservableCollection<string> AvailableGamepadButtons => _mainViewModel.AvailableGamepadButtons;

    public ObservableCollection<string> AvailableThumbstickFromValues => InputTrigger.AvailableThumbstickFromValues;

    public IReadOnlyList<GamepadBindingType> AvailableGamepadBindingTypes { get; } =
        Enum.GetValues<GamepadBindingType>().ToArray();

    public ObservableCollection<TriggerMoment> AvailableTriggerModes => _mainViewModel.AvailableTriggerModes;

    public ObservableCollection<TemplateOption> AvailableProfileTemplates => _mainViewModel.AvailableTemplates;

    public ObservableCollection<RadialMenuDefinition> AvailableRadialMenus => _mainViewModel.RadialMenus;

    [ObservableProperty]
    private TriggerMoment editBindingTrigger = TriggerMoment.Tap;

    [ObservableProperty]
    private string editBindingDescriptionPrimary = string.Empty;

    [ObservableProperty]
    private string editBindingDescriptionSecondary = string.Empty;

    /// <summary>Trigger match threshold for LT/RT chords (0–1, exclusive of 0); shown when <see cref="InputTriggerViewModel.SourceInvolvesTrigger"/> is true.</summary>
    [ObservableProperty]
    private string editAnalogThresholdText = string.Empty;

    [ObservableProperty]
    private string validationError = string.Empty;

    [ObservableProperty]
    private string validationWarning = string.Empty;

    [ObservableProperty]
    private bool hasValidationError;

    [ObservableProperty]
    private bool hasValidationWarning;

    [ObservableProperty]
    private bool isCreatingNewMapping;

    partial void OnIsCreatingNewMappingChanged(bool value)
    {
        _mainViewModel.RefreshRightPanelSurface();
    }

    public bool EditKeyboardAndHoldSectionsEnabled => SelectedActionType == MappingActionType.Keyboard;

    public IReadOnlyList<ItemCycleDirection> AvailableItemCycleDirections { get; } =
        new[] { ItemCycleDirection.Next, ItemCycleDirection.Previous };

    public IEnumerable<MappingActionType> AvailableActionTypes { get; } = Enum.GetValues<MappingActionType>();

    [ObservableProperty]
    private bool isMappingDetailsExpanderExpanded = true;

    public string KeyboardKeyCapturePrompt => _mainViewModel.KeyboardCaptureService.KeyboardKeyCapturePrompt;

    private ICommand? _recordKeyboardKeyCommand;
    public ICommand RecordKeyboardKeyCommand => _recordKeyboardKeyCommand ??= new RelayCommand(() => (CurrentActionEditor as KeyboardActionEditorViewModel)?.RecordKeyboardKeyCommand.Execute(null));

    private ICommand? _recordHoldKeyboardKeyCommand;
    public ICommand RecordHoldKeyboardKeyCommand => _recordHoldKeyboardKeyCommand ??= new RelayCommand(() => (CurrentActionEditor as KeyboardActionEditorViewModel)?.RecordHoldKeyboardKeyCommand.Execute(null));

    private ICommand? _recordItemCycleForwardKeyCommand;
    public ICommand RecordItemCycleForwardKeyCommand =>
        _recordItemCycleForwardKeyCommand ??= new RelayCommand(() => (CurrentActionEditor as ItemCycleActionEditorViewModel)?.RecordForwardKeyCommand.Execute(null));

    private ICommand? _recordItemCycleBackwardKeyCommand;
    public ICommand RecordItemCycleBackwardKeyCommand =>
        _recordItemCycleBackwardKeyCommand ??= new RelayCommand(() => (CurrentActionEditor as ItemCycleActionEditorViewModel)?.RecordBackwardKeyCommand.Execute(null));

    private ICommand? _updateSelectedBindingCommand;
    public ICommand UpdateSelectedBindingCommand => _updateSelectedBindingCommand ??= new RelayCommand(UpdateSelectedBinding);

    private ICommand? _addMappingCommand;
    public ICommand AddMappingCommand => _addMappingCommand ??= new RelayCommand(BeginCreateNewMapping);

    private ICommand? _removeSelectedMappingCommand;
    public ICommand RemoveSelectedMappingCommand => _removeSelectedMappingCommand ??= new RelayCommand(RemoveSelectedMapping);

    private ICommand? _saveNewMappingCommand;
    public ICommand SaveNewMappingCommand => _saveNewMappingCommand ??= new RelayCommand(SaveNewMapping);

    private ICommand? _cancelCreateNewMappingCommand;
    public ICommand CancelCreateNewMappingCommand => _cancelCreateNewMappingCommand ??= new RelayCommand(CancelCreateNewMapping);

    public void SyncFromSelection(MappingEntry? value)
    {
        if (value is not null)
            IsCreatingNewMapping = false;

        // Avoid wiping the "new mapping" draft when we clear table selection for create mode.
        if (value is null && IsCreatingNewMapping)
            return;

        InputTrigger.SyncFrom(value ?? new MappingEntry());
        EditBindingTrigger = value?.Trigger ?? TriggerMoment.Tap;
        if (value is not null)
        {
            var ui = AppUiLocalization.EditorUiCulture();
            EditBindingDescriptionPrimary = UiCultureDescriptionPair.ReadPrimary(value.Descriptions, value.Description, ui);
            EditBindingDescriptionSecondary = UiCultureDescriptionPair.ReadSecondary(value.Descriptions, value.Description, ui);
        }
        else
        {
            EditBindingDescriptionPrimary = string.Empty;
            EditBindingDescriptionSecondary = string.Empty;
        }
        EditAnalogThresholdText = value?.AnalogThreshold is { } t
            ? t.ToString("G", CultureInfo.InvariantCulture)
            : string.Empty;

        if (value is not null)
        {
            _syncingActionEditorFromSelection = true;
            try
            {
                if (!string.IsNullOrWhiteSpace(value.ActionId))
                {
                    SelectedActionType = MappingActionType.Keyboard;
                    var keyboardEditor = _actionEditorFactory.Create(MappingActionType.Keyboard);
                    keyboardEditor.SyncFrom(value);
                    CurrentActionEditor = keyboardEditor;
                }
                else
                {
                    SelectedActionType = value.ActionType;
                    CurrentActionEditor = _actionEditorFactory.CreateForMapping(value);
                }
            }
            finally
            {
                _syncingActionEditorFromSelection = false;
            }
        }
        else
        {
            SelectedActionType = MappingActionType.Keyboard;
            CurrentActionEditor = _actionEditorFactory.Create(SelectedActionType);
            CurrentActionEditor.Clear();
        }

        OnPropertyChanged(nameof(EditKeyboardAndHoldSectionsEnabled));
        OnPropertyChanged(nameof(EditBindingKeyboardKeyIsReadOnly));
    }

    private void RebuildKeyboardActionsPicker()
    {
        KeyboardActionsForPicker.Clear();
        KeyboardActionsForPicker.Add(new KeyboardActionDefinition { Id = string.Empty });
        foreach (var a in _mainViewModel.KeyboardActions)
            KeyboardActionsForPicker.Add(a);
    }

    private void OnMappingsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems is not null)
                {
                    foreach (MappingEntry m in e.NewItems)
                        AttachMappingActionIdListener(m);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems is not null)
                {
                    foreach (MappingEntry m in e.OldItems)
                        DetachMappingActionIdListener(m);
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                foreach (var m in _mappingActionIdListeners.ToList())
                    DetachMappingActionIdListener(m);
                foreach (var m in _mainViewModel.Mappings)
                    AttachMappingActionIdListener(m);
                break;
        }

        RefreshStatusDiagnostics();
    }

    private void AttachMappingActionIdListener(MappingEntry m)
    {
        if (_mappingActionIdListeners.Add(m))
            m.PropertyChanged += OnMappingEntryPropertyChanged;
    }

    private void DetachMappingActionIdListener(MappingEntry m)
    {
        if (_mappingActionIdListeners.Remove(m))
            m.PropertyChanged -= OnMappingEntryPropertyChanged;
    }

    private void OnMappingEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MappingEntry.ActionId))
            UpdateUnusedActionIds();

        if (e.PropertyName != nameof(MappingEntry.ActionId) || sender is not MappingEntry m)
            return;
        if (_resolvingActionId)
            return;

        var raw = m.ActionId;
        if (string.IsNullOrWhiteSpace(raw))
        {
            _resolvingActionId = true;
            try
            {
                m.ActionId = null;
            }
            finally
            {
                _resolvingActionId = false;
            }

            if (ReferenceEquals(m, SelectedMapping))
                SyncFromSelection(m);
            return;
        }

        var id = raw.Trim();
        var def = _mainViewModel.KeyboardActions.FirstOrDefault(a =>
            string.Equals((a.Id ?? string.Empty).Trim(), id, StringComparison.OrdinalIgnoreCase));
        if (def is null)
        {
            MessageBox.Show(
                string.Format(AppUiLocalization.GetString("MappingEditorUnknownKeyboardActionId"), id),
                AppUiLocalization.GetString("MappingEditorCatalogDialogTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            _resolvingActionId = true;
            try
            {
                m.ActionId = null;
            }
            finally
            {
                _resolvingActionId = false;
            }

            if (ReferenceEquals(m, SelectedMapping))
                SyncFromSelection(m);
            return;
        }

        _resolvingActionId = true;
        try
        {
            m.ItemCycle = null;
            m.TemplateToggle = null;
            m.RadialMenu = null;
            m.ApplyKeyboardCatalogDefinition(def);
            if (AppUiLocalization.TryTranslationService() is { } ts)
                CatalogDescriptionLocalizer.ApplyMappingDescription(m, ts);
            if (ReferenceEquals(m, SelectedMapping))
                SyncFromSelection(m);
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _resolvingActionId = false;
        }
    }

    private void BeginCreateNewMapping()
    {
        IsMappingDetailsExpanderExpanded = true;
        IsCreatingNewMapping = true;
        _mainViewModel.SelectedMapping = null;

        InputTrigger.Clear();
        EditBindingTrigger = TriggerMoment.Tap;
        EditBindingDescriptionPrimary = string.Empty;
        EditBindingDescriptionSecondary = string.Empty;
        EditAnalogThresholdText = string.Empty;

        SelectedActionType = MappingActionType.Keyboard;
        CurrentActionEditor?.Clear();

        OnPropertyChanged(nameof(EditKeyboardAndHoldSectionsEnabled));
    }

    private void SaveNewMapping()
    {
        if (!TryBuildMappingFromEditorFields(out var entry, out var messageKey))
        {
            MessageBox.Show(
                AppUiLocalization.GetString(messageKey ?? "MappingEditorSaveFailedGeneric"),
                AppUiLocalization.GetString("MappingEditorSaveFailedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (AppUiLocalization.TryTranslationService() is { } ts)
            CatalogDescriptionLocalizer.ApplyMappingDescription(entry, ts);

        _mainViewModel.RecordTemplateWorkspaceCheckpoint();
        _mainViewModel.Mappings.Add(entry);
        _mainViewModel.SelectedMapping = entry;
        IsCreatingNewMapping = false;
        InputTrigger.ShowSourceKindChangedHint = false;
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CancelCreateNewMapping()
    {
        IsCreatingNewMapping = false;
        SyncFromSelection(SelectedMapping);
    }

    private bool TryBuildMappingFromEditorFields(out MappingEntry entry, out string? messageKey)
    {
        messageKey = null;
        entry = new MappingEntry();

        if (!InputTrigger.ApplyTo(entry))
        {
            messageKey = "MappingEditorInvalidSource";
            return false;
        }

        entry.Trigger = EditBindingTrigger;
        ApplyDescriptionPairToMapping(entry);

        if (!TryApplyAnalogThreshold(entry))
        {
            messageKey = "TriggerChordThresholdRequiredMessage";
            return false;
        }

        if (CurrentActionEditor?.ApplyTo(entry) != true)
        {
            messageKey = "MappingEditorInvalidAction";
            return false;
        }

        return true;
    }

    private bool TryApplyAnalogThreshold(MappingEntry entry)
    {
        var fromType = entry.From?.Type;

        if (fromType is GamepadBindingType.LeftTrigger or GamepadBindingType.RightTrigger)
        {
            if (!GamepadChordInput.TryParseTriggerMatchThreshold(EditAnalogThresholdText, out var nativeTh))
                return false;
            entry.AnalogThreshold = nativeTh;
            return true;
        }

        if (fromType is GamepadBindingType.LeftThumbstick or GamepadBindingType.RightThumbstick)
        {
            var raw = (EditAnalogThresholdText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                entry.AnalogThreshold = null;
                return true;
            }

            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ||
                !float.IsFinite(f) ||
                f <= 0f ||
                f > 1f)
                return false;

            entry.AnalogThreshold = f;
            return true;
        }

        if (fromType != GamepadBindingType.Button)
        {
            entry.AnalogThreshold = null;
            return true;
        }

        var chord = entry.From!.Value ?? string.Empty;
        if (!GamepadChordInput.ExpressionInvolvesTrigger(chord))
        {
            entry.AnalogThreshold = null;
            return true;
        }

        if (!GamepadChordInput.TryParseTriggerMatchThreshold(EditAnalogThresholdText, out var threshold))
            return false;

        entry.AnalogThreshold = threshold;
        return true;
    }

    private void UpdateSelectedBinding()
    {
        if (SelectedMapping is null)
            return;

        _mainViewModel.RecordTemplateWorkspaceCheckpoint();

        if (!InputTrigger.ApplyTo(SelectedMapping))
            return;

        if (!TryApplyAnalogThreshold(SelectedMapping))
        {
            MessageBox.Show(
                AppUiLocalization.GetString("TriggerChordThresholdRequiredMessage"),
                AppUiLocalization.GetString("MappingEditorSaveFailedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SelectedMapping.Trigger = EditBindingTrigger;
        ApplyDescriptionPairToMapping(SelectedMapping);

        if (CurrentActionEditor?.ApplyTo(SelectedMapping) != true)
            return;

        InputTrigger.ShowSourceKindChangedHint = false;
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveSelectedMapping()
    {
        if (SelectedMapping is null)
            return;

        _mainViewModel.RecordTemplateWorkspaceCheckpoint();
        _mainViewModel.Mappings.Remove(SelectedMapping);
        _mainViewModel.SelectedMapping = _mainViewModel.Mappings.FirstOrDefault();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void KeyboardCaptureServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IKeyboardCaptureService.KeyboardKeyCapturePrompt))
            OnPropertyChanged(nameof(KeyboardKeyCapturePrompt));
    }

    private void TranslationServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TranslationService.Culture))
        {
            UpdateUnusedActionIds();
            RefreshMappingDescriptionEditFields();
        }
    }

    private void RefreshMappingDescriptionEditFields()
    {
        if (IsCreatingNewMapping || SelectedMapping is null)
            return;
        var ui = AppUiLocalization.EditorUiCulture();
        EditBindingDescriptionPrimary = UiCultureDescriptionPair.ReadPrimary(SelectedMapping.Descriptions, SelectedMapping.Description, ui);
        EditBindingDescriptionSecondary = UiCultureDescriptionPair.ReadSecondary(SelectedMapping.Descriptions, SelectedMapping.Description, ui);
    }

    private void ApplyDescriptionPairToMapping(MappingEntry entry)
    {
        var ui = AppUiLocalization.EditorUiCulture();
        var d = entry.Descriptions;
        var b = entry.Description;
        UiCultureDescriptionPair.WritePair(ref d, ref b, ui, EditBindingDescriptionPrimary, EditBindingDescriptionSecondary);
        entry.Descriptions = d;
        entry.Description = b;
        if (AppUiLocalization.TryTranslationService() is { } ts)
            CatalogDescriptionLocalizer.ApplyMappingDescription(entry, ts);
    }

    private void MainViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.RadialMenus):
                RefreshStatusDiagnostics();
                break;
            case nameof(MainViewModel.Mappings):
                OnPropertyChanged(nameof(Mappings));
                RefreshStatusDiagnostics();
                break;
            case nameof(MainViewModel.SelectedMapping):
                OnPropertyChanged(nameof(SelectedMapping));
                if (!_workspaceMappingSelectionSync)
                    SyncWorkspaceMappingsFromPrimary();
                SyncFromSelection(SelectedMapping);
                _mainViewModel.RuleClipboard?.RefreshCommandStates();
                break;
            case nameof(MainViewModel.AvailableGamepadButtons):
                OnPropertyChanged(nameof(AvailableGamepadButtons));
                break;
            case nameof(MainViewModel.AvailableTriggerModes):
                OnPropertyChanged(nameof(AvailableTriggerModes));
                break;
            case nameof(MainViewModel.AvailableTemplates):
                OnPropertyChanged(nameof(AvailableProfileTemplates));
                break;
            case nameof(MainViewModel.SelectedTemplate):
                OnPropertyChanged(nameof(AvailableRadialMenus));
                RefreshStatusDiagnostics();
                break;
        }
    }
}


