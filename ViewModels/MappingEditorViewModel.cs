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
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels;

public partial class MappingEditorViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private bool _resolvingActionId;
    private readonly HashSet<MappingEntry> _mappingActionIdListeners = [];

    public MappingEditorViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
        _mainViewModel.KeyboardCaptureService.PropertyChanged += KeyboardCaptureServiceOnPropertyChanged;
        _mainViewModel.Mappings.CollectionChanged += OnMappingsCollectionChanged;
        _mainViewModel.KeyboardActions.CollectionChanged += (_, _) => RebuildKeyboardActionsPicker();
        foreach (var m in _mainViewModel.Mappings)
            AttachMappingActionIdListener(m);
        RebuildKeyboardActionsPicker();
    }

    /// <summary>Keyboard catalog for the current profile (same as <see cref="MainViewModel.KeyboardActions"/>).</summary>
    public ObservableCollection<KeyboardActionDefinition> KeyboardActions => _mainViewModel.KeyboardActions;

    /// <summary>Catalog entries plus a synthetic row with empty <see cref="KeyboardActionDefinition.Id"/> for manual key output.</summary>
    public ObservableCollection<KeyboardActionDefinition> KeyboardActionsForPicker { get; } = [];

    public bool EditBindingKeyboardKeyIsReadOnly => !string.IsNullOrWhiteSpace(EditBindingActionId);

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
    private string editBindingFromButton = "A";

    [ObservableProperty]
    private bool editSourceIsCombination;

    [ObservableProperty]
    private string editBindingComboText = string.Empty;

    [ObservableProperty]
    private string editBindingComboButton1 = "A";

    [ObservableProperty]
    private string editBindingComboButton2 = "B";

    [ObservableProperty]
    private TriggerMoment editBindingTrigger = TriggerMoment.Tap;

    [ObservableProperty]
    private string editBindingKeyboardKey = string.Empty;

    [ObservableProperty]
    private string editBindingDescription = string.Empty;

    [ObservableProperty]
    private string editBindingHoldKeyboardKey = string.Empty;

    [ObservableProperty]
    private string editBindingHoldThresholdText = string.Empty;

    [ObservableProperty]
    private bool editItemCycleEnabled;

    [ObservableProperty]
    private ItemCycleDirection editItemCycleDirection = ItemCycleDirection.Next;

    [ObservableProperty]
    private string editItemCycleSlotText = "9";

    [ObservableProperty]
    private string editItemCycleWithKeys = string.Empty;

    [ObservableProperty]
    private string editItemCycleForwardKey = string.Empty;

    [ObservableProperty]
    private string editItemCycleBackwardKey = string.Empty;

    [ObservableProperty]
    private bool editTemplateToggleEnabled;

    [ObservableProperty]
    private string editTemplateToggleAlternateProfileId = string.Empty;

    [ObservableProperty]
    private bool editRadialMenuEnabled;

    [ObservableProperty]
    private string editRadialMenuId = string.Empty;

    /// <summary>When set with Update, binds the mapping to <see cref="KeyboardActions"/> (fills key and description from the catalog).</summary>
    [ObservableProperty]
    private string editBindingActionId = string.Empty;

    [ObservableProperty]
    private bool isCreatingNewMapping;

    partial void OnIsCreatingNewMappingChanged(bool value) => _mainViewModel.RefreshRightPanelSurface();

    partial void OnEditBindingActionIdChanged(string value) => OnPropertyChanged(nameof(EditBindingKeyboardKeyIsReadOnly));

    /// <summary>When false, KB/M output and hold bind fields apply; item cycle / template toggle / radial menu use their own outputs.</summary>
    public bool EditKeyboardAndHoldSectionsEnabled => !EditItemCycleEnabled && !EditTemplateToggleEnabled && !EditRadialMenuEnabled;

    public IReadOnlyList<ItemCycleDirection> AvailableItemCycleDirections { get; } =
        new[] { ItemCycleDirection.Next, ItemCycleDirection.Previous };

    [ObservableProperty]
    private bool isMappingDetailsExpanderExpanded = true;

    public string KeyboardKeyCapturePrompt => _mainViewModel.KeyboardCaptureService.KeyboardKeyCapturePrompt;

    private ICommand? _recordKeyboardKeyCommand;
    public ICommand RecordKeyboardKeyCommand => _recordKeyboardKeyCommand ??= new RelayCommand(RecordKeyboardKey);

    private ICommand? _recordHoldKeyboardKeyCommand;
    public ICommand RecordHoldKeyboardKeyCommand => _recordHoldKeyboardKeyCommand ??= new RelayCommand(RecordHoldKeyboardKey);

    private ICommand? _recordItemCycleForwardKeyCommand;
    public ICommand RecordItemCycleForwardKeyCommand =>
        _recordItemCycleForwardKeyCommand ??= new RelayCommand(RecordItemCycleForwardKey);

    private ICommand? _recordItemCycleBackwardKeyCommand;
    public ICommand RecordItemCycleBackwardKeyCommand =>
        _recordItemCycleBackwardKeyCommand ??= new RelayCommand(RecordItemCycleBackwardKey);

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

        EditSourceIsCombination = false;
        EditBindingComboText = string.Empty;
        EditBindingComboButton1 = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditBindingComboButton2 = AvailableGamepadButtons.Skip(1).FirstOrDefault() ?? "B";

        if (value?.From is not null && value.From.Type == GamepadBindingType.Button)
        {
            var raw = value.From.Value ?? string.Empty;
            if (ChordResolver.TryParseButtonChord(raw, out var chordButtons, out var reqRt, out var reqLt, out _)
                && (chordButtons.Count > 1 || reqRt || reqLt))
            {
                EditSourceIsCombination = true;
                EditBindingComboText = raw;

                if (chordButtons.Count >= 2)
                {
                    EditBindingComboButton1 = chordButtons[0].ToString();
                    EditBindingComboButton2 = chordButtons[1].ToString();
                }
                else if (chordButtons.Count == 1)
                {
                    EditBindingComboButton1 = chordButtons[0].ToString();
                }

                EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
            }
            else
            {
                var mappedButton = raw;
                EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault(
                        b => string.Equals(b, mappedButton, StringComparison.OrdinalIgnoreCase))
                    ?? (AvailableGamepadButtons.FirstOrDefault() ?? "A");
            }
        }
        else
        {
            EditBindingFromButton = value?.From?.Value ?? string.Empty;
        }

        EditBindingTrigger = value?.Trigger ?? TriggerMoment.Tap;
        if (value?.ItemCycle is { } ic)
        {
            EditItemCycleEnabled = true;
            EditTemplateToggleEnabled = false;
            EditTemplateToggleAlternateProfileId = string.Empty;
            EditItemCycleDirection = ic.Direction;
            EditItemCycleSlotText = Math.Clamp(ic.SlotCount, 1, 9).ToString(CultureInfo.InvariantCulture);
            EditItemCycleWithKeys = ic.WithKeys is { Count: > 0 } ? string.Join('+', ic.WithKeys) : string.Empty;
            EditItemCycleForwardKey = ic.LoopForwardKey ?? string.Empty;
            EditItemCycleBackwardKey = ic.LoopBackwardKey ?? string.Empty;
            EditBindingKeyboardKey = string.Empty;
            EditBindingHoldKeyboardKey = string.Empty;
            EditBindingHoldThresholdText = string.Empty;
        }
        else if (value?.TemplateToggle is { } tt)
        {
            EditItemCycleEnabled = false;
            EditItemCycleDirection = ItemCycleDirection.Next;
            EditItemCycleSlotText = "9";
            EditItemCycleWithKeys = string.Empty;
            EditItemCycleForwardKey = string.Empty;
            EditItemCycleBackwardKey = string.Empty;
            EditTemplateToggleEnabled = true;
            EditTemplateToggleAlternateProfileId = tt.AlternateProfileId ?? string.Empty;
            EditRadialMenuEnabled = false;
            EditRadialMenuId = string.Empty;
            EditBindingKeyboardKey = string.Empty;
            EditBindingHoldKeyboardKey = string.Empty;
            EditBindingHoldThresholdText = string.Empty;
        }
        else if (value?.RadialMenu is { } rm)
        {
            EditItemCycleEnabled = false;
            EditItemCycleDirection = ItemCycleDirection.Next;
            EditItemCycleSlotText = "9";
            EditItemCycleWithKeys = string.Empty;
            EditItemCycleForwardKey = string.Empty;
            EditItemCycleBackwardKey = string.Empty;
            EditTemplateToggleEnabled = false;
            EditTemplateToggleAlternateProfileId = string.Empty;
            EditRadialMenuEnabled = true;
            EditRadialMenuId = rm.RadialMenuId ?? string.Empty;
            EditBindingKeyboardKey = string.Empty;
            EditBindingHoldKeyboardKey = string.Empty;
            EditBindingHoldThresholdText = string.Empty;
        }
        else
        {
            EditItemCycleEnabled = false;
            EditTemplateToggleEnabled = false;
            EditTemplateToggleAlternateProfileId = string.Empty;
            EditRadialMenuEnabled = false;
            EditRadialMenuId = string.Empty;
            EditItemCycleDirection = ItemCycleDirection.Next;
            EditItemCycleSlotText = "9";
            EditItemCycleWithKeys = string.Empty;
            EditItemCycleForwardKey = string.Empty;
            EditItemCycleBackwardKey = string.Empty;
            EditBindingKeyboardKey = value?.KeyboardKey ?? string.Empty;
            EditBindingHoldKeyboardKey = value?.HoldKeyboardKey ?? string.Empty;
            EditBindingHoldThresholdText = value?.HoldThresholdMs?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }

        EditBindingDescription = value?.Description ?? string.Empty;
        EditBindingActionId = value?.ActionId ?? string.Empty;
        OnPropertyChanged(nameof(EditKeyboardAndHoldSectionsEnabled));
        OnPropertyChanged(nameof(EditBindingKeyboardKeyIsReadOnly));
    }

    private void RecordKeyboardKey()
    {
        if (IsCreatingNewMapping)
        {
            _mainViewModel.KeyboardCaptureService.BeginCapture(
                "Press a key for the new mapping output (Esc to cancel).",
                key =>
                {
                    EditBindingKeyboardKey = key.ToString();
                    EditBindingActionId = string.Empty;
                    OnPropertyChanged(nameof(EditBindingKeyboardKeyIsReadOnly));
                    ConfigurationChanged?.Invoke(this, EventArgs.Empty);
                });
            return;
        }

        if (SelectedMapping is null)
            return;

        _mainViewModel.KeyboardCaptureService.BeginCapture(
            "Press a key to assign to the selected mapping (Esc to cancel).",
            key =>
                {
                    EditBindingKeyboardKey = key.ToString();
                    EditBindingActionId = string.Empty;
                    if (SelectedMapping is not null)
                    {
                        SelectedMapping.ActionId = null;
                        SelectedMapping.KeyboardKey = EditBindingKeyboardKey;
                    }
                    OnPropertyChanged(nameof(EditBindingKeyboardKeyIsReadOnly));
                    ConfigurationChanged?.Invoke(this, EventArgs.Empty);
                });
    }

    private void RecordItemCycleForwardKey()
    {
        _mainViewModel.KeyboardCaptureService.BeginCapture(
            "Press the loop-forward output key (Esc to cancel).",
            key => EditItemCycleForwardKey = key.ToString());
    }

    private void RecordItemCycleBackwardKey()
    {
        _mainViewModel.KeyboardCaptureService.BeginCapture(
            "Press the loop-back output key (Esc to cancel).",
            key => EditItemCycleBackwardKey = key.ToString());
    }

    private void RecordHoldKeyboardKey()
    {
        if (IsCreatingNewMapping)
        {
            _mainViewModel.KeyboardCaptureService.BeginCapture(
                "Press the HOLD output key (Esc to cancel).",
                key =>
                {
                    EditBindingHoldKeyboardKey = key.ToString();
                    ConfigurationChanged?.Invoke(this, EventArgs.Empty);
                });
            return;
        }

        if (SelectedMapping is null)
            return;

        _mainViewModel.KeyboardCaptureService.BeginCapture(
            "Press the HOLD output key (Esc to cancel).",
            key =>
            {
                EditBindingHoldKeyboardKey = key.ToString();
                if (SelectedMapping is not null)
                    SelectedMapping.HoldKeyboardKey = EditBindingHoldKeyboardKey;
                ConfigurationChanged?.Invoke(this, EventArgs.Empty);
            });
    }

    private void UpdateSelectedBinding()
    {
        if (SelectedMapping is null)
            return;

        var button = (EditBindingFromButton ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(button))
            return;

        var sourceType = SelectedMapping.From?.Type ?? GamepadBindingType.Button;
        if (sourceType == GamepadBindingType.Button)
        {
            if (EditSourceIsCombination)
            {
                var b1 = EditBindingComboButton1;
                var b2 = EditBindingComboButton2;
                if (string.Equals(b1, b2, StringComparison.OrdinalIgnoreCase)) return;
                var combo = $"{b1}+{b2}";
                SelectedMapping.From = new GamepadBinding { Type = sourceType, Value = combo };
            }
            else
            {
                var isKnownSingleButton = AvailableGamepadButtons.Any(
                    b => string.Equals(b, button, StringComparison.OrdinalIgnoreCase));
                if (!isKnownSingleButton)
                    return;
                SelectedMapping.From = new GamepadBinding { Type = sourceType, Value = button };
            }
        }
        else
        {
            SelectedMapping.From = new GamepadBinding { Type = sourceType, Value = button };
        }
        SelectedMapping.Trigger = EditBindingTrigger;
        SelectedMapping.Description = (EditBindingDescription ?? string.Empty).Trim();
        SelectedMapping.AnalogThreshold = null;

        if (EditItemCycleEnabled)
        {
            if (!TryBuildItemCycleBindingFromEditor(out var icBinding))
                return;

            SelectedMapping.ItemCycle = icBinding;
            SelectedMapping.TemplateToggle = null;
            SelectedMapping.ActionId = null;
            SelectedMapping.KeyboardKey = string.Empty;
            SelectedMapping.HoldKeyboardKey = string.Empty;
            SelectedMapping.HoldThresholdMs = null;
        }
        else if (EditTemplateToggleEnabled)
        {
            var alt = (EditTemplateToggleAlternateProfileId ?? string.Empty).Trim();
            if (alt.Length == 0 || !_mainViewModel.GetProfileService().TemplateExists(alt))
                return;
            if (string.Equals(alt, _mainViewModel.SelectedTemplate?.ProfileId, StringComparison.OrdinalIgnoreCase))
                return;

            SelectedMapping.ItemCycle = null;
            SelectedMapping.TemplateToggle = new TemplateToggleBinding { AlternateProfileId = alt };
            SelectedMapping.RadialMenu = null;
            SelectedMapping.ActionId = null;
            SelectedMapping.KeyboardKey = string.Empty;
            SelectedMapping.HoldKeyboardKey = string.Empty;
            SelectedMapping.HoldThresholdMs = null;
        }
        else if (EditRadialMenuEnabled)
        {
            var rmId = (EditRadialMenuId ?? string.Empty).Trim();
            SelectedMapping.ItemCycle = null;
            SelectedMapping.TemplateToggle = null;
            SelectedMapping.RadialMenu = new RadialMenuBinding { RadialMenuId = rmId };
            SelectedMapping.ActionId = null;
            SelectedMapping.KeyboardKey = string.Empty;
            SelectedMapping.HoldKeyboardKey = string.Empty;
            SelectedMapping.HoldThresholdMs = null;
        }
        else
        {
            SelectedMapping.ItemCycle = null;
            SelectedMapping.TemplateToggle = null;
            SelectedMapping.RadialMenu = null;

            var actionIdFromEditor = (EditBindingActionId ?? string.Empty).Trim();
            if (actionIdFromEditor.Length > 0)
            {
                var def = _mainViewModel.KeyboardActions.FirstOrDefault(a =>
                    string.Equals((a.Id ?? string.Empty).Trim(), actionIdFromEditor, StringComparison.OrdinalIgnoreCase));
                if (def is null)
                    return;

                SelectedMapping.ActionId = actionIdFromEditor;
                SelectedMapping.ApplyKeyboardCatalogDefinition(def);
                SelectedMapping.Description = (EditBindingDescription ?? string.Empty).Trim();
                if (!TryApplyHoldFieldsToSelectedMapping())
                    return;
            }
            else
            {
                var keyToken = (EditBindingKeyboardKey ?? string.Empty).Trim();
                var key = MappingEngine.ParseKey(keyToken);
                var isMouseLookOutput = MappingEngine.IsMouseLookOutput(keyToken);
                if (key == Key.None && !isMouseLookOutput)
                    return;

                SelectedMapping.ActionId = null;
                SelectedMapping.KeyboardKey = isMouseLookOutput ? MappingEngine.NormalizeKeyboardKeyToken(keyToken) : key.ToString();

                if (!TryApplyHoldFieldsToSelectedMapping())
                    return;
            }
        }

        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool TryApplyHoldFieldsToSelectedMapping()
    {
        if (SelectedMapping is null)
            return false;

        var holdToken = (EditBindingHoldKeyboardKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(holdToken))
        {
            SelectedMapping.HoldKeyboardKey = string.Empty;
            SelectedMapping.HoldThresholdMs = null;
            return true;
        }

        var holdKey = MappingEngine.ParseKey(holdToken);
        var holdMouseLook = MappingEngine.IsMouseLookOutput(holdToken);
        if (holdKey == Key.None && !holdMouseLook)
            return false;

        SelectedMapping.HoldKeyboardKey = holdMouseLook ? MappingEngine.NormalizeKeyboardKeyToken(holdToken) : holdKey.ToString();
        int? holdMs = null;
        var t = (EditBindingHoldThresholdText ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(t) &&
            int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
            parsed > 0)
            holdMs = parsed;
        SelectedMapping.HoldThresholdMs = holdMs;
        return true;
    }

    private void BeginCreateNewMapping()
    {
        IsMappingDetailsExpanderExpanded = true;
        IsCreatingNewMapping = true;
        _mainViewModel.SelectedMapping = null;

        EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditSourceIsCombination = false;
        EditBindingComboText = string.Empty;
        EditBindingComboButton1 = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditBindingComboButton2 = AvailableGamepadButtons.Skip(1).FirstOrDefault() ?? "B";
        EditBindingTrigger = TriggerMoment.Tap;
        EditBindingKeyboardKey = string.Empty;
        EditBindingDescription = string.Empty;
        EditBindingActionId = string.Empty;
        EditBindingHoldKeyboardKey = string.Empty;
        EditBindingHoldThresholdText = string.Empty;
        EditItemCycleEnabled = false;
        EditItemCycleDirection = ItemCycleDirection.Next;
        EditItemCycleSlotText = "9";
        EditItemCycleWithKeys = string.Empty;
        EditItemCycleForwardKey = string.Empty;
        EditItemCycleBackwardKey = string.Empty;
        EditTemplateToggleEnabled = false;
        EditTemplateToggleAlternateProfileId = string.Empty;
        EditRadialMenuEnabled = false;
        EditRadialMenuId = string.Empty;
        OnPropertyChanged(nameof(EditKeyboardAndHoldSectionsEnabled));
    }

    private void SaveNewMapping()
    {
        if (!TryBuildMappingFromEditorFields(out var entry))
        {
            MessageBox.Show(
                "Choose a gamepad button, then either enable hotbar cycling (valid slot count 1–9; optional loop forward/back keys together, or both empty for digits 1–n; optional modifiers), or toggle profile (pick another saved template), or enter a valid keyboard / mouse-look output.",
                "Cannot save new mapping",
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

    private bool TryBuildMappingFromEditorFields(out MappingEntry entry)
    {
        entry = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        };

        if (EditSourceIsCombination)
        {
            var b1 = EditBindingComboButton1;
            var b2 = EditBindingComboButton2;
            if (string.Equals(b1, b2, StringComparison.OrdinalIgnoreCase)) return false;
            var combo = $"{b1}+{b2}";
            entry.From = new GamepadBinding { Type = GamepadBindingType.Button, Value = combo };
        }
        else
        {
            var button = (EditBindingFromButton ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(button))
                return false;
            var isKnownSingleButton = AvailableGamepadButtons.Any(
                b => string.Equals(b, button, StringComparison.OrdinalIgnoreCase));
            if (!isKnownSingleButton)
                return false;
            entry.From = new GamepadBinding { Type = GamepadBindingType.Button, Value = button };
        }
        entry.Trigger = EditBindingTrigger;
        entry.Description = (EditBindingDescription ?? string.Empty).Trim();
        entry.AnalogThreshold = null;

        if (EditItemCycleEnabled)
        {
            if (!TryBuildItemCycleBindingFromEditor(out var ic))
                return false;

            entry.ItemCycle = ic;
            entry.TemplateToggle = null;
            entry.ActionId = null;
            entry.KeyboardKey = string.Empty;
            entry.HoldKeyboardKey = string.Empty;
            entry.HoldThresholdMs = null;
            return true;
        }

        if (EditTemplateToggleEnabled)
        {
            var alt = (EditTemplateToggleAlternateProfileId ?? string.Empty).Trim();
            if (alt.Length == 0 || !_mainViewModel.GetProfileService().TemplateExists(alt))
                return false;
            if (string.Equals(alt, _mainViewModel.SelectedTemplate?.ProfileId, StringComparison.OrdinalIgnoreCase))
                return false;

            entry.ItemCycle = null;
            entry.TemplateToggle = new TemplateToggleBinding { AlternateProfileId = alt };
            entry.RadialMenu = null;
            entry.ActionId = null;
            entry.KeyboardKey = string.Empty;
            entry.HoldKeyboardKey = string.Empty;
            entry.HoldThresholdMs = null;
            return true;
        }

        if (EditRadialMenuEnabled)
        {
            var rmId = (EditRadialMenuId ?? string.Empty).Trim();
            entry.ItemCycle = null;
            entry.TemplateToggle = null;
            entry.RadialMenu = new RadialMenuBinding { RadialMenuId = rmId };
            entry.ActionId = null;
            entry.KeyboardKey = string.Empty;
            entry.HoldKeyboardKey = string.Empty;
            entry.HoldThresholdMs = null;
            return true;
        }

        entry.ItemCycle = null;
        entry.TemplateToggle = null;
        entry.RadialMenu = null;

        var actionIdFromEditor = (EditBindingActionId ?? string.Empty).Trim();
        if (actionIdFromEditor.Length > 0)
        {
            var def = _mainViewModel.KeyboardActions.FirstOrDefault(a =>
                string.Equals((a.Id ?? string.Empty).Trim(), actionIdFromEditor, StringComparison.OrdinalIgnoreCase));
            if (def is null)
                return false;

            entry.ActionId = actionIdFromEditor;
            entry.ApplyKeyboardCatalogDefinition(def);
            entry.Description = (EditBindingDescription ?? string.Empty).Trim();
        }
        else
        {
            var keyToken = (EditBindingKeyboardKey ?? string.Empty).Trim();
            var key = MappingEngine.ParseKey(keyToken);
            var isMouseLookOutput = MappingEngine.IsMouseLookOutput(keyToken);
            if (key == Key.None && !isMouseLookOutput)
                return false;

            entry.ActionId = null;
            entry.KeyboardKey = isMouseLookOutput ? MappingEngine.NormalizeKeyboardKeyToken(keyToken) : key.ToString();
        }

        var holdToken = (EditBindingHoldKeyboardKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(holdToken))
        {
            entry.HoldKeyboardKey = string.Empty;
            entry.HoldThresholdMs = null;
            return true;
        }

        var holdKey = MappingEngine.ParseKey(holdToken);
        var holdMouseLook = MappingEngine.IsMouseLookOutput(holdToken);
        if (holdKey == Key.None && !holdMouseLook)
            return false;

        entry.HoldKeyboardKey = holdMouseLook ? MappingEngine.NormalizeKeyboardKeyToken(holdToken) : holdKey.ToString();
        int? holdMs = null;
        var t = (EditBindingHoldThresholdText ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(t) &&
            int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
            parsed > 0)
            holdMs = parsed;
        entry.HoldThresholdMs = holdMs;
        return true;
    }

    private bool TryBuildItemCycleBindingFromEditor([NotNullWhen(true)] out ItemCycleBinding? binding)
    {
        binding = null;
        var slotText = (EditItemCycleSlotText ?? string.Empty).Trim();
        if (!int.TryParse(slotText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < 1 || n > 9)
            return false;

        if (!TryParseWithKeysTokens(EditItemCycleWithKeys, out var withKeys))
            return false;

        var fwdRaw = (EditItemCycleForwardKey ?? string.Empty).Trim();
        var backRaw = (EditItemCycleBackwardKey ?? string.Empty).Trim();
        var hasFwd = fwdRaw.Length > 0;
        var hasBack = backRaw.Length > 0;
        if (hasFwd != hasBack)
            return false;

        if (hasFwd)
        {
            if (!MappingEngine.TryNormalizeMappedOutputStorage(fwdRaw, out var fSt) ||
                !MappingEngine.TryNormalizeMappedOutputStorage(backRaw, out var bSt))
                return false;

            binding = new ItemCycleBinding
            {
                Direction = EditItemCycleDirection,
                SlotCount = n,
                LoopForwardKey = fSt,
                LoopBackwardKey = bSt,
                WithKeys = withKeys
            };
            return true;
        }

        binding = new ItemCycleBinding
        {
            Direction = EditItemCycleDirection,
            SlotCount = n,
            WithKeys = withKeys
        };
        return true;
    }

    private static bool TryParseWithKeysTokens(string? line, out List<string>? tokens)
    {
        tokens = null;
        if (string.IsNullOrWhiteSpace(line))
            return true;

        var parts = line.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return true;

        tokens = [];
        foreach (var p in parts)
        {
            if (MappingEngine.ParseKey(p) == Key.None)
                return false;
            tokens.Add(p);
        }

        return true;
    }

    partial void OnEditItemCycleEnabledChanged(bool value)
    {
        if (value)
        {
            EditTemplateToggleEnabled = false;
            EditRadialMenuEnabled = false;
        }
        OnPropertyChanged(nameof(EditKeyboardAndHoldSectionsEnabled));
    }

    partial void OnEditTemplateToggleEnabledChanged(bool value)
    {
        if (value)
        {
            EditItemCycleEnabled = false;
            EditRadialMenuEnabled = false;
        }
        OnPropertyChanged(nameof(EditKeyboardAndHoldSectionsEnabled));
    }

    partial void OnEditRadialMenuEnabledChanged(bool value)
    {
        if (value)
        {
            EditItemCycleEnabled = false;
            EditTemplateToggleEnabled = false;
        }
        OnPropertyChanged(nameof(EditKeyboardAndHoldSectionsEnabled));
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
            case nameof(MainViewModel.Mappings):
                OnPropertyChanged(nameof(Mappings));
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
                break;
        }
    }
}
