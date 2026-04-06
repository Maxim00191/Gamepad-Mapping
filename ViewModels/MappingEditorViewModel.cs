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
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services;
using Gamepad_Mapping.ViewModels.Strategies;

namespace Gamepad_Mapping.ViewModels;

public partial class MappingEditorViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly IActionEditorFactory _actionEditorFactory;
    private bool _resolvingActionId;
    private bool _syncingActionEditorFromSelection;
    private readonly HashSet<MappingEntry> _mappingActionIdListeners = [];

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
        _mainViewModel.KeyboardCaptureService.PropertyChanged += KeyboardCaptureServiceOnPropertyChanged;
        _mainViewModel.Mappings.CollectionChanged += OnMappingsCollectionChanged;
        _mainViewModel.KeyboardActions.CollectionChanged += (_, _) =>
        {
            RebuildKeyboardActionsPicker();
            UpdateUnusedActionIds();
            ValidateCurrentState();
        };
        foreach (var m in _mainViewModel.Mappings)
            AttachMappingActionIdListener(m);
        RebuildKeyboardActionsPicker();
        UpdateUnusedActionIds();
        ValidateCurrentState();

        // Initialize default editor
        OnSelectedActionTypeChanged(SelectedActionType);
    }

    private void ValidateCurrentState()
    {
        var profile = _mainViewModel.GetProfileService().LoadSelectedTemplate(_mainViewModel.SelectedTemplate);
        if (profile == null) return;

        // Update profile with current UI state if we are editing
        if (SelectedMapping != null)
        {
            // This is a bit tricky since we want real-time feedback.
            // For now, let's validate the whole profile.
        }

        var result = _mainViewModel.GetProfileService().ValidateTemplate(profile);
        HasValidationError = !result.IsValid;
        ValidationError = string.Join(Environment.NewLine, result.Errors);
        HasValidationWarning = result.Warnings.Any();
        ValidationWarning = string.Join(Environment.NewLine, result.Warnings);
    }

    [ObservableProperty]
    private string unusedActionIdsHint = string.Empty;

    [ObservableProperty]
    private string unusedActionIdsTooltip = string.Empty;

    [ObservableProperty]
    private bool hasUnusedActionIds;

    private void UpdateUnusedActionIds()
    {
        IKeyboardActionCatalog? catalog = null;
        if (_mainViewModel.SelectedTemplate != null)
        {
            catalog = _mainViewModel.GetProfileService().LoadSelectedTemplate(_mainViewModel.SelectedTemplate);
        }

        if (catalog == null)
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

        var unused = catalog.GetAllActions()
            .Where(a => !string.IsNullOrWhiteSpace(a.Id) && 
                        !usedInMappings.Contains(a.Id) && 
                        !usedInRadialMenus.Contains(a.Id))
            .ToList();

        HasUnusedActionIds = unused.Count > 0;
        if (HasUnusedActionIds)
        {
            UnusedActionIdsHint = $"Notice: {unused.Count} actionId(s) in catalog are not used in any mapping or radial menu.";
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

    public MappingEntry? SelectedMapping
    {
        get => _mainViewModel.SelectedMapping;
        set => _mainViewModel.SelectedMapping = value;
    }

    public ObservableCollection<string> AvailableGamepadButtons => _mainViewModel.AvailableGamepadButtons;

    public ObservableCollection<TriggerMoment> AvailableTriggerModes => _mainViewModel.AvailableTriggerModes;

    public ObservableCollection<TemplateOption> AvailableProfileTemplates => _mainViewModel.AvailableTemplates;

    public ObservableCollection<RadialMenuDefinition> AvailableRadialMenus => _mainViewModel.RadialMenus;

    [ObservableProperty]
    private TriggerMoment editBindingTrigger = TriggerMoment.Tap;

    [ObservableProperty]
    private string editBindingDescription = string.Empty;

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

    partial void OnIsCreatingNewMappingChanged(bool value) => _mainViewModel.RefreshRightPanelSurface();

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
        EditBindingDescription = value?.Description ?? string.Empty;
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
        UpdateUnusedActionIds();
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
                $"Unknown keyboardActions id '{id}'.",
                "Catalog",
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
        EditBindingDescription = string.Empty;
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
                Loc(messageKey ?? "MappingEditorSaveFailedGeneric"),
                Loc("MappingEditorSaveFailedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _mainViewModel.Mappings.Add(entry);
        _mainViewModel.SelectedMapping = entry;
        IsCreatingNewMapping = false;
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
        entry.Description = (EditBindingDescription ?? string.Empty).Trim();

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
        if (entry.From?.Type is GamepadBindingType.LeftTrigger or GamepadBindingType.RightTrigger)
        {
            if (!GamepadChordInput.TryParseTriggerMatchThreshold(EditAnalogThresholdText, out var nativeTh))
                return false;
            entry.AnalogThreshold = nativeTh;
            return true;
        }

        if (entry.From?.Type != GamepadBindingType.Button)
        {
            entry.AnalogThreshold = null;
            return true;
        }

        var raw = entry.From.Value ?? string.Empty;
        if (!GamepadChordInput.ExpressionInvolvesTrigger(raw))
        {
            entry.AnalogThreshold = null;
            return true;
        }

        if (!GamepadChordInput.TryParseTriggerMatchThreshold(EditAnalogThresholdText, out var threshold))
            return false;

        entry.AnalogThreshold = threshold;
        return true;
    }

    private static string Loc(string key)
    {
        if (Application.Current?.Resources["Loc"] is TranslationService loc)
            return loc[key];
        return key;
    }

    private void UpdateSelectedBinding()
    {
        if (SelectedMapping is null)
            return;

        if (!InputTrigger.ApplyTo(SelectedMapping))
            return;

        if (!TryApplyAnalogThreshold(SelectedMapping))
        {
            MessageBox.Show(
                Loc("TriggerChordThresholdRequiredMessage"),
                Loc("MappingEditorSaveFailedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SelectedMapping.Trigger = EditBindingTrigger;
        SelectedMapping.Description = (EditBindingDescription ?? string.Empty).Trim();

        if (CurrentActionEditor?.ApplyTo(SelectedMapping) != true)
            return;

        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveSelectedMapping()
    {
        if (SelectedMapping is null)
            return;

        _mainViewModel.Mappings.Remove(SelectedMapping);
        _mainViewModel.SelectedMapping = _mainViewModel.Mappings.FirstOrDefault();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void KeyboardCaptureServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IKeyboardCaptureService.KeyboardKeyCapturePrompt))
            OnPropertyChanged(nameof(KeyboardKeyCapturePrompt));
    }

    private void MainViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.RadialMenus):
                UpdateUnusedActionIds();
                break;
            case nameof(MainViewModel.Mappings):
                OnPropertyChanged(nameof(Mappings));
                UpdateUnusedActionIds();
                break;
            case nameof(MainViewModel.SelectedMapping):
                OnPropertyChanged(nameof(SelectedMapping));
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
                UpdateUnusedActionIds();
                break;
        }
    }
}
